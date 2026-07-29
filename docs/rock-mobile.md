# Rock Mobile Coding Guide (AI Context Document)

> **Purpose:** This file is written to be pasted into an AI coding assistant's context (CLAUDE.md, system prompt, or attached reference) so it can write correct Rock RMS **mobile** blocks on the first try. It is a rules-and-patterns document, not a human tutorial. Nearly every empirical rule here was learned by breaking something in production and fixing it â€” treat them as hard constraints, not suggestions.
>
> **How to use:** When asked to build or edit a Rock Mobile content block: (1) read the target file and its nearest sibling first, (2) mirror their structure, `Rock:` controls, and `StyleClass` idioms, (3) obey the comment/delimiter rules exactly, (4) run the pre-ship checklist at the bottom.
>
> **Note on IDs & versions:** Some values below (page GUIDs, group-type IDs, attribute keys, hex colors, workflow IDs, dataset keys) are shown as *examples* from one Rock install â€” your install's values differ. Treat the *technique* as universal and look up your own IDs. Items labeled "verified on-device" were confirmed by live testing on a specific Rock version/shell, not from documentation â€” they are empirical and may vary by version. Items sourced to docs are cited.
>
> **Shell version matters:** This guide assumes the **Rock Mobile shell V6+ (.NET MAUI)**. Rock Mobile V5 and earlier ran on **Xamarin.Forms** (Microsoft ended Xamarin support May 2024, which drove the migration). If an install is still on V5 or lower, some MAUI-specific control and animation behavior here will differ â€” confirm the shell version first.

---

## 0a. Empirical corrections - Vox prod (Rock 17.5.2 mobile, verified on-device 2026-07-01)

> These OVERRIDE the guidance below wherever they conflict. Each was confirmed by live on-device breakage + fix on the Vox prod install. When you hit `Xml_InvalidRootData 1,1`, check #1 and #2 first.

1. **`//-` line comments ARE fine in mobile Content blocks (Section 1.1 stands).**
   An earlier revision of this file claimed `//-` leaked as literal text and caused `Xml_InvalidRootData 1,1`. That was a
   misattribution — the real cause of that symptom is block config (NULL `AdditionalSettings` / Dynamic Content = No, see
   #2 and #4), not the comment style. Everett's live pathway pages use `//-` and render correctly. Use `//-` or
   `{% comment %} … {% endcomment %}` interchangeably; `<!-- -->` is fine in the XAML body. The only hard rule: no comment
   inside a single `{%- lava … -%}` shorthand block — split it.

2. **A cloned mobile Content block MUST carry `Block.AdditionalSettings` with `"ProcessLavaOnServer":true`.**
   Standing up a block via `INSERT INTO [Block] (...cols...) SELECT ... FROM [Block] WHERE Id=<template>` with a
   PARTIAL column list leaves `AdditionalSettings` NULL. The block then renders Lava on the **client**, so
   server-only tags (`{% sql %}`, `{% stagevideos %}`, `{% syncpersonjourney %}`, any custom/plugin Lava tag) can't
   run -> raw/incomplete output -> **`Xml_InvalidRootData 1,1`** (same on-device symptom as #1, different cause).
   Always copy `AdditionalSettings` from a known-good sibling block, or backfill:
   `UPDATE [Block] SET AdditionalSettings=(SELECT AdditionalSettings FROM [Block] WHERE Id=<good>) WHERE Id IN (...) AND (AdditionalSettings IS NULL OR AdditionalSettings='')`.
   Verify with `AdditionalSettings LIKE '%"ProcessLavaOnServer":true%'`.

3. **Cosmetic-change propagation: clearing the app cache is enough - a full mobile-app "Deploy" is NOT required.**
   Section 8 implies a site Deploy is the trigger. In practice `AdditionalSettings.CacheDuration` is an on-device,
   per-person cache of the rendered output (the Vox home block was `600` = 10 min). Once it expires - **or the app
   cache is cleared** - the edit shows on next navigation, no Deploy needed for content/cosmetic edits. Set
   `CacheDuration:0` on a block for fast iteration. (`OutputCacheDuration`, a Block column, is the legacy web cache -
   separate and irrelevant for mobile.)

4. **A Content block needs "Dynamic Content" = Yes to render server-side per-request with a live `CurrentPerson` - and that toggle is a BLOCK ATTRIBUTE, not in `AdditionalSettings`.** (Cost real hours on Vox prod, verified on-device 2026-07-01.)
   With **Dynamic Content = No**, the block renders **static** (baked/cached, no live person context), so **`CurrentPerson` is null even when the user is authenticated.** Every server-side Lava tag that needs the person - `{% stagevideos %}`, `{% syncpersonjourney %}`, `{% personjourneyprogress %}`, any custom plugin tag, and plain `{{ CurrentPerson.* }}` - then **silently returns empty**. No error, no `Xml_InvalidRootData`; the page renders fine but behaves as if **logged out** (progress empty, gated content locked, video/list tags blank). That "renders fine but personless" is exactly what makes it look like a data or auth bug instead of a block-config one.
   - **`ProcessLavaOnServer:true` is necessary but NOT sufficient.** A block can have `ProcessLavaOnServer:true` (in `AdditionalSettings`, see #2) and STILL render personless because Dynamic Content is off. They are two different switches.
   - **Where it lives:** Dynamic Content is stored as a **block `AttributeValue`** (surfaced via the `Block.AdditionalSettingsJson` column in Rock 17.5+), **NOT** in the `Block.AdditionalSettings` column where `ProcessLavaOnServer`/`CacheDuration`/`RequiresNetwork` sit. So (a) copying `AdditionalSettings` between blocks does **not** carry it, and (b) SQL/tooling that only inspects `AdditionalSettings` won't see it - check the block's Attributes / `AdditionalSettingsJson`.
   - **How it bites:** cloning a block (SQL `INSERT ... SELECT`, or the CMS copy) from a template that has Dynamic Content = No yields a **personless clone** even with correct `ProcessLavaOnServer:true` + Enabled Lava Commands. (Vox: 8 cloned engine pages all rendered `CurrentPerson = null` while the hand-built originals worked - the *only* config difference was this one attribute.)
   - **Diagnose:** drop a debug label into the **visible content stack** (inside the `ScrollView`'s content `VerticalStackLayout` - **not** as the first child of the root `<Grid>`, where z-order hides it behind later siblings): `<Label Text="DBG pid={{ CurrentPerson.Id }} has={{ hasPerson }}" TextColor="Red" />`. Blank/false while you're authenticated **and** other (non-cloned) blocks show your name → Dynamic Content = No.
   - **Fix:** toggle **Dynamic Content = Yes** in the block's settings (Admin > block edit), or set the underlying block attribute directly; then clear the app cache (#3).

---

## 0. The single most important mental model

A Rock Mobile "content block" is **two languages in one file, run at two different times, on two different machines:**

1. **Lava** runs **first, on the Rock server** (mostly). It queries the database, computes variables, and its job is to **emit a string of XAML text.**
2. **XAML** (.NET MAUI â€” *not* HTML, *not* WPF, and on V6+ no longer Xamarin.Forms) is what's left after Lava finishes. That string is shipped to the phone and rendered natively by the MAUI runtime inside the Rock mobile shell.

Consequences that drive everything else:
- **The phone never sees your server Lava.** If the app shows raw `{{ }}` or a config error, **Lava produced malformed XAML** â€” debug the server-side output, not the client.
- Anything that looks like a Lava delimiter (`{{`, `}}`, `{%`, `%}`, `-%}`) is **parsed by the Lava engine wherever it appears**, including inside text you *think* is an inert comment. This is the #1 cause of "it just breaks."
- **There is no browser, no CSS, no DOM, no JavaScript.** Do not reach for HTML/WebView for visual precision â€” it breaks native navigation, taps, and theming. Stay in native XAML.
- **XAML is XML: it is strict.** Every tag closes, attributes are quoted, and special chars must be escaped: `&` â†’ `&amp;`, `<` â†’ `&lt;`, and `--` cannot appear inside an `<!-- -->` comment. Use the Lava `| Escape` filter on any user/DB text you interpolate into an attribute (`Text="{{ Request.Text | Escape }}"`).
- **Mobile Lava â‰  Web Lava.** Things that work in a Rock website/HTML block frequently do *not* work in a mobile block (some comment forms, the `Groups` filter, capture-based XAML injection). Never assume web behavior carries over.
- **Some Lava runs on the device, not the server** (see Â§2.7 `{% raw %}` / `PageValues`). Know which context you're in.

---

## 1. Anatomy of a block â€” regions & comment rules

A mobile block has two regions, in order. **Comment syntax differs per region** â€” getting it wrong prints `invalid root data 1,1` and nothing renders.

```
â”Œâ”€ REGION A: PRE-ROOT LAVA (setup) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚  assign statements, {% sql %}, {% if %}, dataset reads, and an â”‚
â”‚  optional {% raw %}â€¦{% endraw %} device-context block.         â”‚
â”‚  Everything BEFORE the first real XAML element (<Grid> â€¦).     â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
â”Œâ”€ REGION B: XAML MARKUP BODY â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚  <Grid> â€¦ </Grid> â€” the actual UI, with {{ var }} interpola-   â”‚
â”‚  tion and {% if %} branching sprinkled in.                     â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

Within Region A there are two sub-contexts:
- **Tag context** â€” standalone `{%- assign -%}`, `{%- if -%}`, `{%- comment -%}` tags.
- **`{% lava %}` shorthand block** â€” a single `{%- lava â€¦ -%}` block containing many `assign`/`if` lines with no per-line delimiters.

### 1.1 Comment rules

> **Note (see 0a):** `//-` line comments are fine on Vox prod 17.5.2 — stripped server-side as documented here. (An earlier note wrongly blamed `//-` for `Xml_InvalidRootData 1,1`; that symptom is a block-config issue — NULL `AdditionalSettings` / Dynamic Content = No.)

| Where | Use | Avoid |
| --- | --- | --- |
| **Inside** a `{%- lava â€¦ -%}` shorthand block | **Nothing.** No comments of any kind. Split the block instead. | `//-`, `{% comment %}`, `<!-- -->` â€” every one prints `invalid root data 1,1`. |
| Region A, **tag context** (standalone lines) | `//-` on its own line **or** `{%- comment -%}â€¦{%- endcomment -%}`. | â€” |
| Region B, **XAML markup body** | `<!-- â€¦ -->` is the safe, portable choice. `{%- comment -%}` also works. | Do not put `//-` *inline within* an element's attributes/markup. |

**Nuance on `//-` in the body (codebase is split â€” pick the safe option):** A standalone `//-` line is a *Lava* comment; Lava strips it before XAML parsing, so `//- MARK: Section` on its own line renders fine and several shipping files use it that way (e.g. `Sermons.xaml`). Other files (`mobile_homepage.xaml`, the Stage pages) use `<!-- -->` exclusively in the body. Both work when the `//-` is a clean standalone line. **Recommendation:** preserve existing `//- MARK:` markers where you find them (they're the house convention), but when *adding* comments to markup, prefer `<!-- -->` â€” it is unambiguous and never at risk of being read as literal text mid-element.

To comment *inside* what would be a `{% lava %}` block: **close the block, comment, reopen.** Lava variables persist across multiple `{%- lava -%}` blocks, so splitting the preamble is free. You **cannot split a lava block mid-`if`/`for`/`case`**, so a loop body stays whole â€” you can't comment inside a loop.

```lava
{%- comment -%} Permissions: one SQL instead of many per-load DB filters {%- endcomment -%}
{%- lava
    assign canViewPersonSearch = false
-%}
```

### 1.2 THE DELIMITER RULE (highest-severity gotcha)

> **A `{%- lava â€¦ -%}` block ends at the *first* `-%}` it sees â€” even one buried in literal text inside the block.** Never place `{%`, `%}`, `-%}`, `{{`, or `}}` in literal text anywhere inside a `{% lava %}` block, comments included.

Real failure that took an A/B isolation session to find: a comment inside the preamble contained the literal text `{%- comment -%}` (to *describe* the syntax). Its `-%}` silently terminated the lava block early; the rest of the preamble spilled out as text and Rock showed **`Invalid configuration data`**. Removing the delimiters from the comment text fixed it. **Rule:** when a comment must mention Lava syntax, describe it in words â€” never write the literal braces.

---

## 2. Lava rules for mobile

### 2.1 Dialect & enabled commands
- **Fluid only.** No DotLiquid-only or legacy direct-attribute patterns.
- **Enabled Lava Commands are per-block.** `{% sql %}`, `{% workflowactivate %}`, `{% mediaelement %}`, `{% steptype %}`, `{% cache %}`, RockEntity filters, etc. each require the matching command checked in that block's **Enabled Lava Commands** setting. If a tag "does nothing" or prints raw, that setting is usually off.

### 2.2 Attributes: display value vs `'RawValue'`
`{{ Person | Attribute:'Key' }}` returns the **formatted display** value. For integers, dates, and anything you'll compute on, request the **raw** value:
```lava
assign streak = CurrentPerson | Attribute:'AppCurrentStreak','RawValue' | AsInteger | Default:0
assign firstUsed = CurrentPerson | Attribute:'FirstUsedtheApp','RawValue' | Date:'MMM yyyy'
```
Use `'RawValue'` for precomputed numeric/date person attributes (see Â§7.2); the plain form for display text.

### 2.3 `CurrentPerson | Groups` is unreliable in mobile â€” prefer `{% sql %}`
> The `Groups` filter has **returned 0 in mobile blocks** for some group-type IDs even when the person is clearly a member (verified on-device for group types 25 & 23). Yet several shipping blocks *do* rely on `CurrentPerson | Groups:'<id>','Active'` and gate visible UI on it â€” so behavior is **inconsistent across group types / versions**, not uniformly broken.

**Guidance:** for anything load-bearing, read membership with a `{% sql %}` command (requires `Sql` enabled) â€” it's the reliable path and the house style on the homepage:
```lava
{% sql return:'myGroupRows' personid:'{{ CurrentPerson.Id }}' %}
    SELECT G.[Name] AS GroupName,
           LOWER(CAST(G.[Guid] AS NVARCHAR(50))) AS GroupGuid,
           CASE WHEN GR.[IsLeader]=1 THEN 'True' ELSE 'False' END AS IsLeader
    FROM [GroupMember] GM
    INNER JOIN [Group] G ON G.[Id]=GM.[GroupId]
    LEFT JOIN [GroupTypeRole] GR ON GR.[Id]=GM.[GroupRoleId]
    WHERE GM.[PersonId]=@personid AND GM.[GroupMemberStatus]=1
      AND GM.[IsArchived]=0 AND G.[IsActive]=1
      AND G.[GroupTypeId] IN (25,23)   -- example ids; yours differ
    ORDER BY G.[Name]
{% endsql %}
```
`Person.Id` and `Person.PrimaryFamily.Members` work fine in mobile.

### 2.4 `{% capture %}`: banned for XAML, fine for JSON/strings
- **Do NOT assemble XAML into a variable and inject it** with `{{ frag }}` â€” it breaks. **Loop and emit markup directly** in the body.
- **Capturing a JSON/data string is fine and common.** Building a JSON payload with `capture` + `echo â€¦ | ToJSON`, then parsing it with `Rock:FromJson`, is a legitimate, widely-used pattern:
  ```lava
  {%- capture eventsJson -%}{% for e in events %}â€¦{% endfor %}{%- endcapture -%}
  {%- assign events = eventsJson | FromJson -%}
  ```
  The ban is specifically about building **markup** with capture â€” not about building data.

### 2.5 SQL-injection safety (inside mobile too)
Never drop a page parameter straight into SQL text. Declare it as a named parameter and cast:
```lava
{% sql campusid:'{{ PageParameter.CampusId | AsInteger }}' %}
    SELECT ... WHERE CampusId = @campusid
{% endsql %}
```
Cast with `| AsInteger`, `| FromIdHash`, or `| Date:'yyyy-MM-dd'`. If the query text contains `{{ PageParameter }}`, `{{ QueryString }}`, or `{{ Request }}`, it's wrong â€” fix it.

### 2.6 Persisted datasets (the shared "rigging" pattern)
The standard way to share config (colors, page GUIDs, labels, styles, stage metadata) across many blocks without per-load queries:
```lava
{%- assign mar = 'mobileapprigging' | PersistedDataset -%}
{%- assign colors = mar.Colors.Pathway -%}
{%- assign border = mar.Styles.Border -%}   {%- comment -%} e.g. StrokeShape / StrokeThickness {%- endcomment -%}
```
- Exposes nested objects: `mar.Stages.Begin.StagePageGuid`, `mar.Colors.Primary.Strong`, `mar.Pages.Home`, `mar.Styles.Border.StrokeShape`, `mar.Resources.Placeholder`.
- Dictionary access with a variable key: `mar.Stages[stageKey]`.
- Iterating a dataset dictionary yields `[key, value]` pairs â€” index the value with `[1]`:
  ```lava
  {%- for item in mar.Stages -%}
      {%- if item[1].StageNumber == nextNum -%}{%- assign next = item[1] -%}{%- endif -%}
  {%- endfor -%}
  ```
- Guard possibly-missing values with `| Default:`.
- **After editing a persisted dataset's definition, Rock must rebuild/refresh it** before changes render on-device. Editing the source file alone does nothing.
- **Custom JSON keys inside the dataset are safe to rename**; Rock entity/property names are not (Â§2.11).
- Datasets support `| Where:'Field', value` and `| OrderBy:'Field desc'` for client-side filtering/sorting.

> **Disproven theory, don't cargo-cult it:** reading `mar.*` inside a body `for`/`case` loop was once blamed for `invalid root data 1,1`. That was wrong â€” the culprit was always the comment/`-%}` delimiter bug (Â§1.2). Dataset reads in the body are fine. Preamble-precompute + plain vars in the body is still the cleaner house style, but not required for correctness.

### 2.7 Device-context Lava: `{% raw %}`, `Device`, `setpagevalue` â†’ `PageValues`
Some Lava must run **on the device** (it needs `Device.Width` etc.), not on the server. Wrap that in `{% raw %}â€¦{% endraw %}` so the server passes it through untouched, then read it back in XAML via `{Binding PageValues.X}`:
```lava
//- MARK: Variables
{% raw %}
    {% assign cardWidthPhone = Device.Width | Times:0.68 | Ceiling %}
    {% setpagevalue 'CardWidthPhone', cardWidthPhone %}
    {% assign cardWidthTablet = Device.Width | Times:0.36 | Ceiling %}
    {% setpagevalue 'CardWidthTablet', cardWidthTablet %}
{% endraw %}
```
```xaml
<Grid WidthRequest="{Rock:OnDeviceType Phone={Binding PageValues.CardWidthPhone},
                                       Tablet={Binding PageValues.CardWidthTablet}}" />
```
This is the standard responsive-sizing idiom for cards/carousels.

### 2.8 Real Rock Step completion (unlock gates)
Prefer live Steps over legacy person attributes (the `Steps` filter requires Rock server **v13+**):
```lava
{%- if CurrentPerson != null and CurrentPerson != empty -%}
    {%- assign personSteps = CurrentPerson | Steps:'all','all',stepTypeId -%}
    {%- for step in personSteps -%}
        {%- assign done = step.StepStatus.IsCompleteStatus | Default:false -%}
        {%- if done == true or step.CompletedDateTime != empty -%}
            {%- assign completed = true -%}{%- break -%}
        {%- endif -%}
    {%- endfor -%}
{%- endif -%}
```
`Steps` args are (Step Program, Step Status, Step Type). Check **both** `StepStatus.IsCompleteStatus` and `CompletedDateTime` â€” statuses are install-configurable.

### 2.9 Firing a workflow from a block
```lava
{%- assign aliasGuid = CurrentPerson.PrimaryAlias.Guid -%}
{%- workflowactivate workflowtype:'494' CurrentPerson:'{{ aliasGuid }}' StepTypeId:'{{ stepTypeId }}' StepStatusId:'6' -%}
{%- endworkflowactivate -%}
```
Pass the person as **PrimaryAlias.Guid**. Requires the workflow Lava command enabled.

### 2.10 Video: loading videos and tracking watches
Two real patterns â€” know both:

**(a) Step-type attribute matrix (the common pathway pattern):** most stage pages pull videos from a Step Type's attribute matrix rather than a hardcoded id:
```lava
{%- steptype where:'Id == {{ stepTypeId }}' iterator:'stItems' -%}
    {%- assign videos = stItems | First | Attribute:'Videos','Object' | Property:'AttributeMatrixItems' -%}
    {%- for item in videos -%}
        {%- assign v = item | Attribute:'Video','Object' | AppendWatches:'Media', 30 -%}
        {%- assign pct = v.WatchLength | Default:0 -%}
    {%- endfor -%}
{%- endsteptype -%}
```
**(b) Direct media element by id:**
```lava
{%- mediaelement where:'Id == {{ videoId }}' iterator:'items' -%}
    {%- assign v = items | First | AppendWatches:'Media', 30 -%}
{%- endmediaelement -%}
```
Use a watch-length threshold (e.g. `>= 95`%, or a `.90` fraction) to treat a video as "completed," then fire the completion workflow (Â§2.9).

> **Caveat:** `AppendWatches` could not be confirmed in the public Lava filter docs â€” treat its exact name/args as install/version-specific and verify against your Rock version. The `{% mediaelement %}`/`{% steptype %}` commands and `WatchLength` are standard.

### 2.11 Backend vs UI vocabulary (a naming trap)
If a program renames a concept in the UI (e.g. "Step" â†’ "Stage"), **the Rock backend feature keeps its original name.** The `| Steps` filter, `{% steptype %}` tag, `[Step]`/`[StepStatus]` SQL tables, entity properties (`.StepStatus`, `.CompletedDateTime`, `.StepTypeId`), and workflow attribute keys all stay "Step" even when the UI, file names, and dataset keys say "Stage." Renaming the Rock-side identifiers breaks the feature.

### 2.12 Mobile Lava filters/tags/objects you'll actually use
- Objects: `CurrentPerson`, `Person`, `Request` (detail-context entity), `Parameters.*` (callback params, Â§4.3), `PageValues.*` (device values, Â§2.7), `PageParameter.*`.
- Filters: `Attribute:'Key'['RawValue'|'Object']`, `Escape`, `PersonByGuid`, `PersonTokenCreate:10,2` (impersonation token for URLs â€” **cache-sensitive**, see Â§8), `FromCache:'DefinedType'` + `EntityFromCachedObject`, `FromJson`/`ToJSON`, `Where`, `OrderBy`, `AppendWatches`, `DaysFromNow`, `SentenceCase`, `AsInteger`/`AsDateTime`, `Default`.
- Tags/commands: `{% sql return:'x' %}`, `{% workflowactivate %}`, `{% mediaelement %}`, `{% steptype %}`, `{% cache %}`, `{% setpagevalue %}` (inside `{% raw %}`).

---

## 3. XAML â€” layout, `Rock:` controls & `StyleClass`

MAUI controls: `Grid`, `VerticalStackLayout`, `HorizontalStackLayout`, `ScrollView`, `Border`, `Label`, `Button`, `ProgressBar`, `RoundRectangle`, `Path`. Plus Rock-specific `Rock:*` controls and CommunityToolkit `toolkit:*` behaviors.

### 3.1 Layout basics
- `Grid RowDefinitions="Auto,*"` / `ColumnDefinitions`; `VerticalStackLayout Spacing="â€¦"` for stacks.
- `ScrollView` wraps long content: `VerticalScrollBarVisibility="Never"`, top/bottom padding clears floating headers/tab bars (e.g. `Padding="0,188,0,72"`).
- Sizes are unitless device-independent pixels. `Padding`/`Margin` take `left,top,right,bottom` or a single value.
- `Rock:Zone.Expands="True"` lets a root grid fill its zone.

### 3.2 `StyleClass` â€” the theme system (use it instead of hardcoding)
Real blocks style almost entirely through utility classes on `StyleClass`, Tailwind-style â€” this is how output stays on-theme:
- **Type ramp:** `title1 title2 title3 headline callout footnote caption1 caption2`, plus `bold`, `text-center`.
- **Text/background color:** `text-interface-strongest|stronger|strong|softâ€¦`, `bg-interface-strongestâ€¦softer` (note: `bg-interface-softer` is near-white in **both** themes â€” see Â§3.5).
- **Spacing:** `mx-/my-/mt-/mb-/ml-/mr-` and `px-/py-/pt-/pb-â€¦` with a size (e.g. `mx-16`, `px-12`, `py-8`).
```xaml
<Label Text="{{ Request.Text | Escape }}" StyleClass="title3, text-interface-stronger" LineHeight="1.18" />
<Border StyleClass="px-12, py-16" BackgroundColor="#{{ mar.Colors.Primary.Strong }}"> â€¦ </Border>
```
Prefer `StyleClass` over raw `Padding`/`BackgroundColor`/hex whenever a class exists â€” it keeps blocks consistent and theme-aware.

### 3.3 `Border` clipping
> A MAUI `Border` clips its children only if it has a `StrokeShape`. A `Border` with no `StrokeShape` does **not** clip â€” overflowing children bleed past rounded corners.
```xaml
<Border StrokeThickness="0" StrokeShape="RoundRectangle 26,26,0,0" BackgroundColor="#{{ secondary2 }}"> â€¦ </Border>
```
`RoundRectangle` corner order is `topLeft,topRight,bottomRight,bottomLeft`. Datasets often expose a ready `StrokeShape`/`StrokeThickness` (`mar.Styles.Border.*`) â€” reuse it.

> **Caveat:** clipping is required-but-not-guaranteed across platforms/versions â€” some MAUI versions have Border-clip bugs, and `Border` has **no `IsClippedToBounds`** escape hatch (unlike the old `Frame`). Verify clipped UI on both iOS and Android.

### 3.4 Colors
- Hex **with a leading `#`**. Dataset colors are typically stored **without** the `#`, so prepend it: `BackgroundColor="#{{ secondary2 }}"`.
- 8-digit hex in **XAML attribute strings is `#AARRGGBB` (alpha first)**: `#40FFFFFF` = 25% white. (Caveat: this is the XAML/`FromArgb` order; MAUI's `FromRgba` *API* uses `#RRGGBBAA` â€” but in markup you're always in the AARRGGBB world.)

### 3.5 Dark mode is NOT auto-inverted â€” theme it explicitly
> The app does not auto-invert for dark mode; a light-only color stays light on a dark screen. Utility class `bg-interface-softer` renders near-white in *both* modes â€” it does not make a dark card.

Use `AppThemeBinding` for anything that must adapt (partial hex interpolation is fine):
```xaml
<Border BackgroundColor="{AppThemeBinding Light=#FFFFFF, Dark=#1C1C1E}" />
<ProgressBar BackgroundColor="{AppThemeBinding Light=#80{{ mar.Colors.Light.Softest }}, Dark=#1d242a}" />
```
Example native-card values (yours differ): card `#FFFFFF`/`#1C1C1E`, page bg `#000000` dark, dividers `#B8C0C2` ~20%.

### 3.6 Images â€” use `Rock:Image`, not MAUI `Image`
`Rock:Image` adds a loading placeholder, aspect ratio, and CDN sizing. Append a width query (`&amp;w=512`, XML-escaped) so the CDN returns a right-sized image:
```xaml
<Rock:Image Source="{{ thumbnailUrl | Escape }}&amp;w=512"
            LoadingPlaceholder="{{ mar.Resources.Placeholder }}"
            Aspect="AspectFill" Ratio="16:9"
            WidthRequest="{Rock:OnDeviceType Phone={Binding PageValues.CardWidthPhone}, Tablet={Binding PageValues.CardWidthTablet}}" />
```

### 3.7 Icons â€” `Rock:Icon`, not image files
```xaml
<Rock:Icon IconFamily="TablerIcons" IconClass="chevron-down" />
<Rock:Icon IconFamily="MaterialDesignIcons" IconClass="heart" />
```
Icon families in use: `TablerIcons`, `MaterialDesignIcons`.

### 3.8 Safe area (notch / home indicator)
Wrap top/bottom-anchored content so it clears the notch and home indicator:
```xaml
<Rock:SafeAreaPaddingBehavior />   <!-- typically iOS Top,Bottom vs Android Bottom -->
```
Omitting it ships content under the notch on iOS.

### 3.9 Interpolation & escaping
Use `{{ var }}` in attribute values; **guard nulls in Lava first** so you never emit a bare `{{ }}` or an empty required attribute. Run DB/user text through `| Escape`.

### 3.10 Video playback â€” `Rock:MediaPlayer`
For playing sermons / live stream (distinct from the watch-tracking Lava in Â§2.10):
```xaml
<Rock:MediaPlayer Source="{{ mar.LiveStreamUrl }}" ... />
```

---

## 4. Navigation, commands & callbacks

### 4.1 Interaction commands (`Command="{Binding â€¦}"`)
Interactive elements bind a Rock shell command, usually on a `TapGestureRecognizer`. Each verb has a matching `Rock:*Parameters` object for structured args:

| Command | Purpose | Parameters object |
| --- | --- | --- |
| `PushPage` | Navigate to a page (keeps back stack) | `Rock:PushPageParameters` |
| `ReplacePage` | Navigate, replacing current | `Rock:ReplacePageParameters` |
| `ShowCoverSheet` / `CloseCoverSheet` | Modal cover sheet | `Rock:ShowCoverSheetParameters` |
| `ShowActionPanel` | Action sheet / confirm | `Rock:ShowActionPanelParameters` (+ `Rock:ActionPanelButton`, `DestructiveButton`) |
| `OpenBrowser` | External/in-app browser | `Rock:OpenBrowserParameters` |
| `Callback` | Re-invoke this block server-side | `Rock:CallbackParameters` (+ `Rock:Parameter`) |
| `ShareContent`, `PlayVideo` | Share sheet, video | â€” |

### 4.2 Navigate by page GUID
Navigate by **page GUID**, not id. Prefer dataset-provided GUIDs (`mar.Stages.X.StagePageGuid`, `mar.Pages.Home`); preserve any GUID you're given exactly. `PushPage` takes a GUID string with optional `?param`:
```xaml
<TapGestureRecognizer Command="{Binding PushPage}"
    CommandParameter="72c37209-bd98-4e4b-b314-2addf85d7720?PersonGuid={{ person.Guid }}" />
```
A person-scoped page identifies the person via a `PersonGuid` page parameter (PageContext).

### 4.3 Callbacks â€” the block re-invokes itself with `Parameters.*`
A `Callback` re-runs the *same* block on the server with named parameters you can read back at the top of the preamble. This powers filters, "I've prayed," report/flag, paging, etc.
```xaml
<TapGestureRecognizer Command="{Binding Callback}"
    CommandParameter="{Rock:CallbackParameters Name=':FlagRequest',
        Parameters={Rock:Parameter Name='SessionContext', Value='{{ SessionContext }}'}}" />
```
Read it server-side:
```lava
{%- assign category = Parameters.CategoryValue -%}
{%- if category == empty -%}{%- assign category = 'All' -%}{%- endif -%}
```
The `Name` (e.g. `:FlagRequest`) selects which branch/handler runs; the `Rock:Parameter` values arrive as `Parameters.<Name>`.

### 4.4 Callbacks hit Rock REST endpoints (know the side effects)
Built-in callbacks (e.g. a Prayer Session `:FlagRequest`) invoke standard Rock Web API endpoints (e.g. `PUT /api/PrayerRequests/Flag/{id}`). Know exactly what the endpoint does â€” e.g. **Flag sets `IsApproved=false` but does NOT touch `IsPublic`**, and no server automation makes a flagged request private â€” so the UI reflects the real server-side effect (if it must also hide the request, that's a separate `PATCH`, not automatic).

### 4.5 Launching a Rock workflow from mobile (person-scoped) â€” verified gotchas
The native **Workflow Entry** block sets the workflow entity from a **`PersonId`** page parameter. Three traps, all verified on-device:
1. **In a Custom Actions template slot, `{{ Person }}` is EMPTY.** Resolve the person yourself: `{{ PageParameter.PersonGuid | PersonByGuid }}`.
2. **The block resolves `PersonId` as an INTEGER only.** A Guid arrives as "no entity." Pass `â€¦| Property:'Id'`.
3. `PushPage` to a page hosting a Workflow Entry block ("Disable Passing WorkflowTypeId" = No), passing `?WorkflowTypeId={int}&PersonId={int}`. The shell does **not** strip `PersonId`.

A WebView to the internal web `â€¦/WorkflowEntry/{int}?PersonId={int}` page also works (auth flows automatically) â€” a valid fallback when the native path is blocked. Otherwise avoid WebView for anything interactive.

---

## 5. Animation (verified on-device)

- **On-load animations run via `Rock:BeginAnimationBehavior`.** The element starts hidden (`Opacity="0"` and/or `TranslationX/Y`), and the behavior fades/moves it in once on load:
  ```xaml
  <VerticalStackLayout Opacity="0">
      <VerticalStackLayout.Behaviors>
          <Rock:BeginAnimationBehavior>
              <Rock:BeginAnimationBehavior.Animation>
                  <Rock:DoubleAnimation Property="Opacity" Duration="250" To="1.0" Delay="180" />
              </Rock:BeginAnimationBehavior.Animation>
          </Rock:BeginAnimationBehavior>
      </VerticalStackLayout.Behaviors>
      â€¦
  </VerticalStackLayout>
  ```
  Stagger entrances by increasing `Delay` per element (the "alive" feel; pages reload each visit so it replays).
- **`RepeatForever` is NOT honored** â€” animations run once and stop (Rock docs confirm `TranslateToAnimation` ignores `RepeatForever`, and also ignores `Easing`). **Do not build looping animations.**
- **`DoubleAnimation Property="â€¦"` only reliably animates `Opacity`** (empirical; `TranslationY` via `DoubleAnimation` produced no motion). For translation use `TranslateToAnimation` (its own `TranslateY` attr) â€” one-shot only.
- **`Easing`:** `CubicOut` is the only value proven across real usage â€” treat others as unproven.
- For a fake "scroll-through" of stacked text, duplicate the first item at the end and translate the track by `rowHeight Ã— itemCount` (there is no true loop).

---

## 6. Cross-platform (iOS vs Android â€” verified on-device)

Several things render on iOS and silently fail on Android. Always check both.

- **Branch platform/device with Rock markup extensions** (and, for a few properties, the `<OnPlatform>` element form):
  ```xaml
  <Grid WidthRequest="{Rock:OnDeviceType Phone={Binding PageValues.CardWidthPhone}, Tablet={Binding PageValues.CardWidthTablet}}" />
  <!-- {Rock:OnDevicePlatform iOS=â€¦, Android=â€¦} for platform branching -->
  ```
- **Overflow-clip scrolling is not portable.** A fixed-height container with taller translated content works on iOS; on Android off-bounds children aren't laid out â€” you get blank after the first item. Use static content, not scroll-by-overflow tickers.
- **`TouchBehavior` consumes the touch on Android.** If a child has `toolkit:TouchBehavior` and a parent has the `TapGestureRecognizer`, the tap never fires on Android (works on iOS). **Put the `TapGestureRecognizer` on the same element as the `TouchBehavior`.**
- **Safe area** differs per platform (Â§3.8) â€” handle the notch/home indicator.

---

## 7. Performance

### 7.1 One SQL beats N per-load Lava DB filters
Replace many per-load attribute/DB-filter Lava calls with a single `{% sql %}` that returns the rows you need, then branch in Lava:
```lava
{% sql return:'permRows' personid:'{{ CurrentPerson.Id }}' %}
    SELECT DISTINCT GM.[GroupId] FROM [GroupMember] GM
    WHERE GM.[PersonId]=@personid AND GM.[GroupId] IN (35,2,33,3)
      AND GM.[GroupMemberStatus]=1 AND GM.[IsArchived]=0
{% endsql %}
{%- for row in permRows -%}
    {%- if row.GroupId == 35 -%}{%- assign canViewPrayerAccess = true -%}{%- endif -%}
{%- endfor -%}
```

### 7.2 Precompute heavy metrics into Person Attributes on a nightly job
> If a page runs an expensive live query on every load (e.g. scanning the huge `[Interaction]` table for streaks/activity counts), **move the computation to a nightly Run SQL service job that writes results into precomputed Person Attributes**, and have the page just *read the attributes* (via `'RawValue'`, Â§2.2). This turns a multi-second load into an instant one.

Conventions when building such a job:
- Scan the big table **once** into a temp table at the right grain (person/page/day), then derive every metric from it â€” don't re-scan per metric (a common bug is a CTE that silently re-runs several times).
- **Insert-only fields** (e.g. "first used the app") must never be recalculated â€” `MERGE` with no `WHEN MATCHED` clause.
- Match Rock's exact stored formats. Dates in Person Attributes use `yyyy-MM-ddT00:00:00.0000000`; build it explicitly (`CONVERT(NVARCHAR(10),d,23) + 'T00:00:00.0000000'`) â€” SQL Server style 126 on `DATETIME2` drops the trailing `.0000000`, so don't rely on it. The page reads it with `| Date:'MMM yyyy'` and falls back to a literal like `'New'`.
- Decide scope deliberately â€” some counts should reflect **all** of a person's data, not just app-active people; don't silently narrow them.

### 7.3 Read-only mindset
Diagnostic SQL is **SELECT-only**. Any data change uses `BEGIN TRANSACTION â€¦ ROLLBACK` with a row-count/preview first, committed only after scope is verified.

---

## 8. Block settings & Deploy (operational â€” easy to lose hours here)

- **Nothing you change reaches phones until you Deploy the mobile app site.** Edit a block, see no change on-device â†’ you probably haven't deployed. This is the most common "why isn't my change showing" cause.
- **Per-block Cache Duration** (in the block's Additional Settings) caches the *rendered output on the device, per person*. Great for static content; **set it to `0` for anything transactional or that embeds a per-person token/URL** (e.g. `PersonTokenCreate` impersonation links) â€” otherwise one person's cached token/URL can be reused by others.
- **Enabled Lava Commands** are per-block (Â§2.1) â€” set them or `{% sql %}`/`{% workflowactivate %}`/`{% steptype %}` silently no-op.
- After editing a **persisted dataset**, refresh/rebuild it in Rock (Â§2.6).

---

## 9. Debugging methodology

| Symptom | Almost always means |
| --- | --- |
| `invalid root data 1,1`, nothing renders | A comment **inside a `{% lava %}` block**, OR a stray Lava delimiter in literal text (Â§1.2). |
| `Invalid configuration data` | A `{%- lava -%}` block terminated early by a `-%}` buried in its text; preamble spilled out. |
| Raw `{{ var }}` visible on device | Lava didn't run on that fragment / a required Lava command isn't enabled / tag typo. |
| A membership/group check is always empty | `CurrentPerson \| Groups` unreliable in mobile (Â§2.3) â€” use `{% sql %}`. |
| Assembled markup renders as text / breaks | `{% capture %}` used for XAML (Â§2.4) â€” loop and emit directly (JSON capture is fine). |
| Change not showing on device | Site not **Deployed**, or block **Cache Duration** serving stale output (Â§8). |
| Blank area after first item, Android only | Overflow-clip scrolling (Â§6) or a missing `StrokeShape` clip (Â§3.3). |
| Tap does nothing, Android only | `TouchBehavior` vs `TapGestureRecognizer` on different elements (Â§6). |
| Rounded corners leak content | `Border` missing `StrokeShape` (Â§3.3). |
| Card stays light in dark mode | Not auto-inverted; needs `AppThemeBinding` (Â§3.5). |
| Content under the notch | Missing `Rock:SafeAreaPaddingBehavior` (Â§3.8). |
| Workflow launches with no person | Guid passed where `PersonId` needs an int, or `{{ Person }}` empty in a Custom Action (Â§4.5). |
| One person sees another's data/token | Cache Duration > 0 on a token-bearing block (Â§8). |

**Isolation technique that works:** paste tiny self-contained blocks (each with its own minimal preamble) one at a time. To settle a yes/no question, make two otherwise-identical blocks differing in exactly one variable and toggle it. A `<Grid><Label Text="ok"/></Grid>` body plus the full preamble isolates preamble-vs-body problems. **Trust A/B results over theory** â€” this is how the comment/delimiter rules were nailed down.

---

## 10. `Rock:*` control catalog (recognize these in sibling files)

Beyond the layout/media controls above, real blocks use: `Rock:Image`, `Rock:Icon`, `Rock:MediaPlayer`, `Rock:Avatar`, `Rock:Expander`, `Rock:Divider`, `Rock:Countdown`, `Rock:BeginAnimationBehavior`, `Rock:SafeAreaPaddingBehavior`, `Rock:BooleanValueConverter` / `Rock:InverseBooleanConverter`, and native domain controls like `Rock:BibleBrowser` / `Rock:BibleReader` / `Rock:BibleAudio` / `Rock:Picker`. When mirroring a sibling, keep its `Rock:` controls rather than substituting raw MAUI equivalents.

---

## 11. Pre-ship checklist

- [ ] No comment of any kind sits **inside** a `{%- lava â€¦ -%}` block.
- [ ] No `{{`, `}}`, `{%`, `%}`, or `-%}` appears in any literal/comment text inside a lava block.
- [ ] Comments per region: `//-`/`{%- comment -%}` pre-root; `<!-- -->` preferred in markup; existing `//- MARK:` markers preserved; no `//-` inline within an element.
- [ ] Group membership comes from `{% sql %}` (not the unreliable `Groups` filter) for anything load-bearing.
- [ ] `{% capture %}` used only for JSON/data, never to assemble XAML.
- [ ] Numeric/date person attributes read with `'RawValue'`.
- [ ] Device-dependent sizing uses `{% raw %}` + `setpagevalue` â†’ `{Binding PageValues.X}`.
- [ ] Every `{% sql %}` param passed named + cast; no `{{ PageParameter }}` in query text.
- [ ] Required Lava commands enabled on the block.
- [ ] Styling uses `StyleClass` utilities where a class exists; DB/user text run through `| Escape`.
- [ ] Every `Border` that should clip has a `StrokeShape` (and clipping verified on both platforms).
- [ ] Dataset colors prefixed with `#`; 8-digit hex is alpha-first in markup.
- [ ] Anything that must adapt to dark mode uses `AppThemeBinding` (nothing relies on auto-inversion).
- [ ] Images use `Rock:Image` with a `LoadingPlaceholder` and `&amp;w=` sizing; icons use `Rock:Icon`.
- [ ] Top/bottom-anchored content has `Rock:SafeAreaPaddingBehavior`.
- [ ] Nulls guarded (`| Default:`) before interpolation into a required attribute.
- [ ] No `RepeatForever`; `DoubleAnimation` only on `Opacity`; `CubicOut` easing; on-load motion via `Rock:BeginAnimationBehavior`.
- [ ] `TapGestureRecognizer` on the same element as any `TouchBehavior`; platform branches use `{Rock:OnDevicePlatform}`/`{Rock:OnDeviceType}`.
- [ ] Navigation uses page GUIDs; callbacks read back via `Parameters.*`; workflow launches pass `PersonId` as an int.
- [ ] Backend feature vocabulary (e.g. "Step") preserved even where the UI says otherwise (e.g. "Stage").
- [ ] Persisted dataset refreshed after edits; **site Deployed**; Cache Duration set to 0 on token-bearing/transactional blocks.
- [ ] Smallest necessary change made; structure, naming, `Rock:` controls, and section markers preserved.
- [ ] Stated whether this was actually tested on-device or only reviewed statically.
```