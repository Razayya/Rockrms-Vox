---
name: rock-page-create
description: Use this skill to create new Rock RMS pages and configure their blocks via SQL against the Vox prod DB. Covers Page/Block/Attribute/AttributeValue/AttributeQualifier wiring, the BlockType-vs-block-instance attribute distinction, Page Parameter Filter setup with single-select filters, Dynamic Data block configuration with Lava merge-field guards, auto-refresh that preserves URL params, and the gotchas around SiteId inheritance, the DisplayInNavWhen value mapping, AttributeQualifier's missing timestamp columns, and the reserved-keyword Key column. Pairs with rock-workflow-deploy for the workflow half of any page-with-workflow flow.
---

# Rock page creation + block wiring via SQL

When the user wants to create a new Page in Rock and configure its blocks (Page Parameter Filter, Dynamic Data, HTML Content, etc.) by writing a SQL migration rather than clicking through the UI.

## When to use this skill

- "Add a new page under X for Y…"
- "Build a dashboard at /something that…"
- "Create a filtered report page with a campus selector…"
- "Add a block to that page that runs SQL and renders…"
- Any inserts/updates touching `Page`, `Block`, or block-instance `Attribute` / `AttributeValue` / `AttributeQualifier` rows.
- Triggers alongside `rock-workflow-deploy` if the page also kicks off a workflow or job.

## Session startup: read the Rock version pin

Rockrms-Vox is a thin-repo — upstream Rock code lives at `build/rock-source-<instance>-<tag>/`. Read the pin for the target instance from `overlay/instance-versions.json`:

```powershell
$pin = (Get-Content overlay\instance-versions.json | ConvertFrom-Json).instances.prod
$cloneDir = Join-Path (git rev-parse --show-toplevel) $pin.clone_dir
```

That `clone_dir` is where to grep BlockType source for the real attribute shape (especially `[BlockField]` declarations, default values, and the FieldType used by each). The block-instance attribute set you mirror in SQL must match what the BlockType source actually declares for the pinned Rock version — these change between releases, and hand-rolled attribute lists from training-data memory will drift.

If the directory doesn't exist, run `.\scripts\prepare-build.ps1 -Instance <env>` once. The pin is authoritative — trust it (see `memory/feedback_user_input_is_king_for_versions.md`). If the instance isn't pinned, route to the `rock-update` skill.

**Use the source as planning input** (see `memory/feedback_read_rock_source_when_planning.md`): when you're about to wire a Dynamic Data block or Page Parameter Filter, the canonical reference is `RockWeb/Blocks/Reporting/DynamicData.ascx.cs` and `RockWeb/Blocks/Reporting/PageParameterFilter.ascx.cs` in the cached clone — not assumption about which `[Key]` slugs they accept.

## High-level pattern

1. **Inherit the Site from the parent page.** Every Page must have `SiteId` set to a non-null value (EF model rule, even though the column is nullable). Look up the parent page's `LayoutId` → `Layout.SiteId` and use that. **Do not** look up a Layout by name like `'Full Width'` without scoping to a Site — multiple Sites can have a Layout with the same name.
2. **Generate, don't hand-write.** SQL bodies for `Query` and Lava bodies for `FormattedOutput` get embedded in `INSERT … VALUES (N'…')`, which means single quotes need escaping. Use a `sed`-based generator (see template below) so the source files keep plain apostrophes and the escape happens once at generation time.
3. **Toggle commit.** Every migration starts with `DECLARE @Commit BIT = 0;` wrapping `BEGIN TRAN` and an `IF @Commit = 1 COMMIT ELSE ROLLBACK` at the bottom. Test with 0, persist with 1.
4. **Idempotency guard.** Hardcode the new Page's `Guid` and `IF EXISTS (SELECT 1 FROM Page WHERE [Guid] = @PageGuid) ROLLBACK; PRINT 'Skipped'; RETURN;` so a re-run is a safe no-op.
5. **Verify in-transaction.** `SELECT` the new Page, blocks, attributes, and qualifiers just before the COMMIT decision. The user reads the verification output before approving the flip to 1.

## The Page → Block → Attribute → AttributeValue chain

```
Page  ──────────────────────  PageId on Block.PageId
  │
  └─ has parent via ParentPageId
  └─ has SiteId, LayoutId (Layout.SiteId must match Page.SiteId)

Block  ──────────────────────  BlockId on AttributeValue.EntityId (when Attr is block-level)
  │                            BlockId in Attribute.EntityTypeQualifierValue (when Attr is block-instance)
  │
  └─ BlockTypeId references BlockType (Path = ~/Blocks/.../X.ascx)

Attribute (block-LEVEL)  ────  EntityTypeId = Block, EntityTypeQualifierColumn='BlockTypeId',
  │                            EntityTypeQualifierValue=<BlockType.Id as varchar>
  │                            -- one set per BlockType, shared across all instances
  │
Attribute (block-INSTANCE) ──  EntityTypeId = Block, EntityTypeQualifierColumn='Id',
                               EntityTypeQualifierValue=<Block.Id as varchar>
                               -- one set per specific Block, e.g. each PPF filter

AttributeValue  ──────────────  EntityId = the specific Block.Id (always)
                               -- the actual saved value for that (Attribute, Block) pair

AttributeQualifier  ──────────  AttributeId = the Attribute being qualified
                               -- per-attribute config like 'fieldtype', 'values', 'repeatColumns'
                               -- NO CreatedDateTime / ModifiedDateTime columns
```

The single most-common confusion: when configuring a Page Parameter Filter, the *block settings* are existing block-level Attributes (you only INSERT AttributeValue rows). The *individual filters* are NEW block-instance Attribute rows (you INSERT both the Attribute AND its AttributeQualifier rows).

## Cadence: dry-run → check in → commit

Every migration is a two-step: run with `@Commit = 0`, surface the verification output to the user, and **stop**. Don't flip `@Commit = 1` and re-run in the same turn — even when the dry-run looks clean. The user reads the output, confirms, then approves the flip. Cross-references `memory/feedback_project_work_check_in_each_step.md`. After commit, revert the file's toggle back to `0` so future re-runs default to dry-run.

### Recovery: when a fix appears to stop working

If a previously-shipped migration appears to "stop working" between sessions (block renders blank, query returns 0 rows that used to populate, etc.), **before re-debugging the logic, re-pull the AttributeValue from prod and verify it still matches the latest migration**. A UI form save can silently overwrite a SQL migration: when a user opens a block in the Rock admin without first clearing cache, the form holds the cached snapshot from when the tab loaded; saving writes that stale copy back. Check `LEN(av.Value)` and a known marker string (a CTE name, a CSS class, etc.) against the migration file before assuming the bug is in your logic. Cross-references `memory/reference_attr_cache_refresh.md`.

## Migration shape

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Commit BIT = 0;

BEGIN TRAN;

DECLARE @Now DATETIME = GETDATE();
DECLARE @ParentPageId INT = <parent page id>;

-- Inherit Site/Layout from parent so the new page lands on the same Site
DECLARE @ParentLayoutId INT = (SELECT LayoutId FROM Page WHERE Id = @ParentPageId);
DECLARE @ParentSiteId   INT = (SELECT l.SiteId FROM Layout l WHERE l.Id = @ParentLayoutId);
DECLARE @LayoutId       INT = (SELECT TOP 1 Id FROM Layout WHERE Name = 'Full Width' AND SiteId = @ParentSiteId);
DECLARE @SiteId         INT = @ParentSiteId;

DECLARE @BlockEntityTypeId INT = (SELECT Id FROM EntityType WHERE Name = 'Rock.Model.Block');
DECLARE @PageGuid UNIQUEIDENTIFIER = '<hardcoded guid>';

-- Idempotency
IF EXISTS (SELECT 1 FROM Page WHERE [Guid] = @PageGuid)
BEGIN
    PRINT CONCAT('Page already exists (Id ', (SELECT Id FROM Page WHERE [Guid] = @PageGuid), '). Skipping.');
    ROLLBACK TRAN;
    RETURN;
END

-- 1. Create Page (see "Page NOT NULL columns" below for full set)
INSERT INTO Page ( … ) VALUES ( … );
DECLARE @PageId INT = SCOPE_IDENTITY();

-- 2. Create Block(s)
-- 3. Insert block-level AttributeValue rows for settings
-- 4. (If filter block) Insert block-instance Attribute + AttributeQualifier rows for each filter

-- Verification SELECTs

IF @Commit = 1 COMMIT TRAN; ELSE ROLLBACK TRAN;
```

## Page NOT NULL columns (full set, verified against Vox prod)

You must include all of these in the Page INSERT:

- `InternalName`, `LayoutId`, `SiteId`, `[Order]`, `[Guid]`
- `IsSystem`
- `RequiresEncryption`, `EnableViewState`, `IncludeAdminFooter`
- `PageDisplayTitle`, `PageDisplayBreadCrumb`, `PageDisplayIcon`, `PageDisplayDescription`
- `DisplayInNavWhen`, `MenuDisplayDescription`, `MenuDisplayIcon`, `MenuDisplayChildPages`
- `BreadCrumbDisplayName`, `BreadCrumbDisplayIcon`
- `OutputCacheDuration` (use `0` for live data)

## Polish: match the navigation siblings

Before finalizing the page, query the parent page's existing children and match their conventions — particularly the **`IconCssClass`**. The column is nullable, so a page with no icon set just renders blank in the nav while siblings have icons; visually it sticks out as unfinished.

```sql
SELECT Id, InternalName, IconCssClass
FROM Page
WHERE ParentPageId = @ParentPageId OR Id = @ParentPageId
ORDER BY [Order];
```

Pick a FontAwesome class (Rock typically loads FA4-syntax `fa fa-<icon>`, which most modern FA versions still render) that is:
- **Semantically distinct** from siblings (don't reuse `fa-chart-bar` if Live Metrics already has it)
- **Relevant to the page's purpose** (gauge / dashboard → `fa fa-tachometer`; people / room → `fa fa-users`; checklist → `fa fa-clipboard-list`; etc.)
- **Available in the FA version Rock uses** — check existing pages' icons as proof of what works (the Vox `fa fa-walking`, `fa fa-door-open`, `fa fa-tachometer` all confirmed live)

Set via `Page.IconCssClass = 'fa fa-<icon>'` in the INSERT. Also confirm `MenuDisplayIcon = 1` (it's a NOT NULL column already in the INSERT list above, but it's the toggle that actually surfaces the icon in the menu).

## Page Parameter Filter setup (BlockType 644)

```sql
-- Block-level settings (existing Attributes; INSERT AttributeValue only)
INSERT INTO AttributeValue (IsSystem, AttributeId, EntityId, Value, [Guid], CreatedDateTime, ModifiedDateTime)
SELECT 0, a.Id, @PpfBlockId, v.Val, NEWID(), @Now, @Now
FROM (VALUES
    ('ShowBlockTitle',         'False'),
    ('FiltersPerRow',          '1'),
    ('FilterButtonText',       'Apply'),
    ('FilterButtonSize',       '3'),
    ('ShowResetFiltersButton', 'True'),
    ('DoesSelectionCausePostback', 'True')   -- see selection-action rule below
) v(K, Val)                                  -- alias as K, NOT Key (reserved keyword)
INNER JOIN Attribute a
    ON a.[Key] = v.K
   AND a.EntityTypeId = @BlockEntityTypeId
   AND a.EntityTypeQualifierColumn = 'BlockTypeId'
   AND a.EntityTypeQualifierValue = '644';

-- Per-filter Attribute (new block-instance Attribute row per filter)
INSERT INTO Attribute (
    IsSystem, FieldTypeId, EntityTypeId, EntityTypeQualifierColumn, EntityTypeQualifierValue,
    [Key], [Name], Description, [Order], IsGridColumn, IsMultiValue, IsRequired,
    AllowSearch, IsIndexEnabled, IsAnalytic, IsAnalyticHistory, IsActive, EnableHistory,
    ShowOnBulk, IsPublic, IsDefaultPersistedValueDirty, IsSuppressHistoryLogging,
    [Guid], CreatedDateTime, ModifiedDateTime
)
VALUES (
    0, 6 /*Single-Select*/, @BlockEntityTypeId, 'Id', CAST(@PpfBlockId AS VARCHAR(20)),
    'CheckInConfigId', 'Check-In Configuration', '<help text>',
    0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0,
    NEWID(), @Now, @Now
);
DECLARE @FilterAttrId INT = SCOPE_IDENTITY();

-- AttributeQualifier rows (NO CreatedDateTime / ModifiedDateTime — table doesn't have those)
INSERT INTO AttributeQualifier (IsSystem, AttributeId, [Key], [Value], [Guid]) VALUES
    (0, @FilterAttrId, 'fieldtype',     'ddl',                        NEWID()),
    (0, @FilterAttrId, 'repeatColumns', '',                           NEWID()),
    (0, @FilterAttrId, 'values',        '<val>^<text>,<val>^<text>',  NEWID());
```

### `DoesSelectionCausePostback` (Selection Action) — value rule

The attribute is labeled **"Selection Action"** in the UI. The block code reads it via `EnumExtensions.ConvertToEnum<SelectionAction>(string)`, so the value must match the **C# enum member name**, NOT the qualifier dropdown key. **Don't trust the AttributeQualifier `values` list (`nothing^,block^Update Block,page^Update Page`)** — those first-half tokens are UI dropdown keys, and saving them throws `'<x>' is not a member of the SelectionAction enumeration` when the page renders.

The actual enum members (verified by exception message + successful render):

| Save this value | UI label | Behavior |
|---|---|---|
| `Nothing` | (default, blank) | No postback — user must click Apply / Filter button |
| `UpdateBlock` | Update Block | Postback updates the filtered block in-place without a page reload |
| `UpdatePage` | Update Page | Full page reload (preserves URL query string and refreshes every block on the page) |

Legacy values `False` and `0` exist in 60+ prod instances; they fail parse and silently fall back to default (`Nothing`). Don't write new ones — they're brittle.

**Vox-confirmed default rule** (cross-references `memory/feedback_ppf_selection_action_default.md`):

- **Single filter on the block → set `'UpdatePage'`.** The selection updates the URL parameter and triggers a full reload, refreshing the data block downstream. Standing default — don't ask.
- **Two or more filters → ASK the user.** With multiple filters, immediate postback usually fires before the user has set all their selections. The right answer is often `'Nothing'` (require Apply) or `'UpdateBlock'` (update without reload), but it depends on the page's flow. Surface the choice.

### Filter source — prefer dynamic over hardcoded

When the dropdown options come from a Rock entity set (Defined Values, Group Types, Campuses, Schedules, etc.), use a **typed FieldType** that auto-populates from the entity table — don't hardcode the values into a `Single-Select` `values` qualifier. Hardcoded lists go stale the first time the user adds a new entry; typed FieldTypes pick up new entries automatically.

Common dynamic patterns:

| Source | FieldType | Key qualifier | Value stored as |
|---|---|---|---|
| Any GroupType under a specific purpose | `33` Group Type (`Rock.Field.Types.GroupTypeFieldType`) | `groupTypePurposeValueGuid` = the purpose's DefinedValue Guid | GroupType.Guid string |
| Any value from a Defined Type | `16` Defined Value (`Rock.Field.Types.DefinedValueFieldType`) | `definedtype` = the DefinedType.Id | DefinedValue.Guid (single) or comma-separated guids (multi) |
| All campuses | `69` Campus | (no scoping qualifier needed) | Campus.Guid |
| All schedules | various Schedule field types | scoped via category qualifier | Schedule.Guid |

**Important:** when the FieldType stores a Guid (which most entity FieldTypes do), the `PageParameter` value Lava substitutes is also a Guid string. The SQL body must convert back to the integer Id for joins:

```sql
DECLARE @CheckInConfigGuid UNIQUEIDENTIFIER = TRY_CAST('{{ PageParameter.CheckInConfigGuid }}' AS UNIQUEIDENTIFIER);
DECLARE @CheckInConfigId   INT = (SELECT Id FROM GroupType WHERE [Guid] = @CheckInConfigGuid);
```

Reflect this in the filter's `Key` — name it `<Thing>Guid` not `<Thing>Id` when the value is a Guid, so future-readers aren't misled.

**Guid case mismatch (cosmetic but worth knowing):** the URL parameter that PPF emits for a Guid-valued FieldType is **lowercase** (`?CheckInConfigGuid=26a4b129-...`), even when the GroupType row's `[Guid]` is stored uppercase (`26A4B129-...`). SQL Server's `UNIQUEIDENTIFIER` comparison is case-insensitive so the join works regardless, but don't be thrown off when scanning logs / URLs and the case doesn't match the DB.

For the Check-in Templates use case specifically, the Vox-confirmed pattern is:

```sql
INSERT INTO Attribute ( … FieldTypeId, [Key], … )
VALUES ( …, 33 /*Group Type*/, 'CheckInConfigGuid', … );

INSERT INTO AttributeQualifier (IsSystem, AttributeId, [Key], [Value], [Guid])
VALUES (0, @FilterAttrId, 'groupTypePurposeValueGuid', '4A406CB0-495B-4795-B788-52BDFDE00B01', NEWID());
-- '4A406CB0-…' is the DefinedValue.Guid for "Check-in Template" group-type-purpose.
```

### When hardcoded `Single-Select` (FieldType 6) IS the right call

Use `Single-Select` only when the option set is genuinely fixed and small (e.g., status pickers like `Pending / Approved / Denied`, "Yes / No" toggles, etc.). Qualifier shape:

| Qualifier `Key` | Value | Meaning |
|---|---|---|
| `fieldtype` | `ddl` or `rb` | Dropdown vs radio buttons |
| `values` | `<val>^<text>,<val>^<text>,…` | Comma-separated value^text pairs |
| `repeatColumns` | empty (for `ddl`) or integer | Layout columns for radio buttons |

## Dynamic Data block setup (BlockType 143)

Setting `FormattedOutput` (a Lava template) automatically suppresses the default grid render. Don't try to set `ShowGrid` — that key isn't an attribute on this BlockType and the INSERT silently drops via the JOIN.

Common attribute settings:

| Key | Recommended value | Notes |
|---|---|---|
| `Query` | The SQL body, with Lava merge fields like `{{ PageParameter.X }}` and `{{ Context.Campus.Id }}` | Lava is processed before SQL execution |
| `FormattedOutput` | The Lava template; receives the SQL result as `rows` | Suppresses the grid when set |
| `EnabledLavaCommands` | `Sql,RockEntity,Cache,Execute` | Required if Lava uses `{% sql %}`, `{% execute %}`, etc. |
| `Timeout` | `30` | Seconds |
| `UpdatePage`, `StoredProcedure`, `PaneledGrid` | `False` | Standard for filtered dashboards |
| `ShowExcelExport`, `ShowMergeTemplate`, `ShowCommunicate` | `False` | Toolbar buttons usually irrelevant when using FormattedOutput |

### Lava merge-field guard pattern in the SQL body

```sql
DECLARE @CheckInConfigId INT = TRY_CAST('{{ PageParameter.CheckInConfigId }}' AS INT);
DECLARE @CampusId        INT = TRY_CAST('{{ Context.Campus.Id }}' AS INT);

IF @CheckInConfigId IS NULL OR @CampusId IS NULL
BEGIN
    -- Return empty result set with the columns the Lava expects
    SELECT
        CAST(NULL AS INT)           AS Col1,
        CAST(NULL AS NVARCHAR(100)) AS Col2,
        …
    WHERE 1 = 0;
    RETURN;
END
```

Why: `TRY_CAST` returns `NULL` on missing/empty input, so a missing query-string param produces an empty result rather than a SQL syntax error from `WHERE x = ` (with empty RHS after Lava substitutes).

### `Context.X` pre-flight: confirm a Page Context source exists

Before relying on `{{ Context.Campus.Id }}` (or any other Context entity) in a DD block's Query, **confirm the rendered page actually has a Context source for that entity**:

1. Inherit the Layout from a known-good sibling that already uses Context for that entity (e.g., on Vox CIM, Layout 30 / Site 5 hosts the layout-level Campus Context Setter Block 478, so `Context.Campus` flows automatically to children).
2. The Page row's `PageContext` table can be empty — that's not the source. The site-scoped cookie set by the layout-level context-setter block is.
3. If unsure, sanity-test from a quick DD block: `SELECT '{{ Context.Campus.Id }}' AS X;`. If it renders `13` you're good; if blank, the page has no campus context source for the current session.

If no context source is wired up, fall back to a `PageParameter`-driven flow (e.g., add `CampusId` as a second PPF filter or include it in the route) rather than silently rendering empty results.

### Auto-refresh — buttonized countdown + pause/resume

`window.location.reload()` re-fetches the same URL including query string, so filter selections survive the refresh. Only inject when there's actually data to refresh — empty-state pages should not auto-reload.

**Default to a button-controlled refresh footer** rather than silent `setTimeout`. Pulse dot + visible countdown + `[⏸ Pause]` toggle (resume = full reload, no resume-from-count logic). Cross-references `memory/feedback_auto_refresh_pause_resume.md`. Reference implementation: Project 4388 Room Capacity, migration `12-add-pause-refresh-button.sql`. Skeleton:

```html
<div class="rcd-footer">
  <span class="rcd-pulse" id="rcd-pulse"></span>
  <span>Refreshes in <span id="rcd-countdown">60s</span></span>
  <button type="button" id="rcd-toggle"><i class="fa fa-pause"></i>Pause</button>
</div>
<script>
(function () {
  var seconds = 60, timer = null, paused = false;
  var countEl = document.getElementById("rcd-countdown");
  var btn = document.getElementById("rcd-toggle");
  var pulse = document.getElementById("rcd-pulse");
  function tick() { seconds -= 1; if (seconds <= 0) { window.location.reload(); return; } countEl.textContent = seconds + "s"; }
  function start() { seconds = 60; paused = false; countEl.textContent = "60s"; btn.innerHTML = '<i class="fa fa-pause"></i>Pause'; pulse.classList.remove("rcd-pulse--paused"); timer = setInterval(tick, 1000); }
  function pause() { paused = true; if (timer) clearInterval(timer); timer = null; btn.innerHTML = '<i class="fa fa-play"></i>Resume'; countEl.textContent = "Paused"; pulse.classList.add("rcd-pulse--paused"); }
  btn.addEventListener("click", function () { if (paused) window.location.reload(); else pause(); });
  start();
})();
</script>
```

For static / one-shot reports without live data, the buttonized footer isn't needed — just render the table and skip auto-refresh entirely.

### Lava `Sum` filter is unreliable on Where-piped DataTable rows

Looks idiomatic, doesn't work. `{{ rows | Where: 'ScheduleId', sid | Sum: 'PresentCount' }}` returns `0` even when the underlying values are populated integers — `Where` returns a non-numeric-typed projection that `Sum` doesn't reduce correctly. **Fix:** use a manual accumulator loop.

```liquid
{%- assign schedRoomCount = 0 -%}
{%- assign schedTotalPresent = 0 -%}
{%- for sr in rows -%}
  {%- if sr.ScheduleId == r.ScheduleId -%}
    {%- assign schedRoomCount = schedRoomCount | Plus: 1 -%}
    {%- assign schedTotalPresent = schedTotalPresent | Plus: sr.PresentCount -%}
  {%- endif -%}
{%- endfor -%}
```

Explicit, deterministic, no surprises. Default to this when computing per-group rollups in a Dynamic Data Lava template.

## Common query patterns for schedule / attendance dashboards

### Today's day-of-week filter (weekly + iCal)

`Schedule.WeeklyDayOfWeek` is a `.NET DayOfWeek` int (0 = Sunday … 6 = Saturday). Non-weekly schedules carry day rules in `iCalendarContent` instead (RRULE/DTSTART). To filter to "active today," check both forms.

Robust today-day calc, independent of `@@DATEFIRST`:

```sql
DECLARE @Today           DATE     = CAST(GETDATE() AS DATE);
DECLARE @TodayDayOfWeek  INT      = ((DATEPART(WEEKDAY, @Today) + @@DATEFIRST - 1) % 7);
DECLARE @TodayBYDAY      CHAR(2)  = CASE @TodayDayOfWeek
    WHEN 0 THEN 'SU' WHEN 1 THEN 'MO' WHEN 2 THEN 'TU'
    WHEN 3 THEN 'WE' WHEN 4 THEN 'TH' WHEN 5 THEN 'FR' WHEN 6 THEN 'SA' END;
DECLARE @TodayCompact    CHAR(8)  = CONVERT(CHAR(8), @Today, 112);
```

Schedule day match:

```sql
INNER JOIN Schedule s ON s.Id = gls.ScheduleId
   AND s.IsActive = 1
   AND (s.EffectiveStartDate IS NULL OR s.EffectiveStartDate <= @Today)
   AND (s.EffectiveEndDate   IS NULL OR s.EffectiveEndDate   >= @Today)
   AND (
        (s.WeeklyDayOfWeek IS NOT NULL AND s.WeeklyDayOfWeek = @TodayDayOfWeek)
        OR
        (s.WeeklyDayOfWeek IS NULL AND (
            s.iCalendarContent IS NULL
            OR LEN(LTRIM(RTRIM(s.iCalendarContent))) = 0
            OR s.iCalendarContent LIKE '%FREQ=DAILY%'
            OR s.iCalendarContent LIKE '%BYDAY=%' + @TodayBYDAY + '%'
            OR s.iCalendarContent LIKE '%DTSTART:'           + @TodayCompact + '%'
            OR s.iCalendarContent LIKE '%DTSTART;VALUE=DATE:'+ @TodayCompact + '%'
        ))
   )
```

The two-letter BYDAY codes (`SU`/`MO`/`TU`/`WE`/`TH`/`FR`/`SA`) don't overlap as substrings, so the `LIKE '%BYDAY=%XX%'` pattern is safe. Add the `EffectiveEndDate` gate to filter out one-off events whose end date has passed.

### Show all configured rooms, not just attended ones

For a "current capacity" dashboard, the user wants every room a kid *could* check into to be visible — empty rooms with `0` attendance and their threshold. Don't INNER JOIN through `AttendanceOccurrence` (that filters out empty rooms); build a `ConfiguredSlots` CTE first (the Group → Location → Schedule cross product), then `LEFT JOIN` AttendanceOccurrence/Attendance:

```sql
;WITH ConfiguredSlots AS (
    SELECT DISTINCT gl.GroupId, gl.LocationId, gls.ScheduleId
    FROM GroupLocation gl
    INNER JOIN /* … config groups CTE … */
    INNER JOIN GroupLocationSchedule gls ON gls.GroupLocationId = gl.Id
    INNER JOIN [Location] loc ON loc.Id = gl.LocationId AND loc.IsActive = 1
    INNER JOIN Schedule s ON s.Id = gls.ScheduleId AND /* … day filter above … */
),
LocationCounts AS (
    SELECT slot.ScheduleId, slot.LocationId,
           COUNT(DISTINCT a.PersonAliasId) AS PresentCount
    FROM ConfiguredSlots slot
    LEFT JOIN AttendanceOccurrence ao
        ON ao.ScheduleId = slot.ScheduleId
       AND ao.GroupId    = slot.GroupId
       AND ao.LocationId = slot.LocationId
       AND ao.OccurrenceDate = @Today
    LEFT JOIN Attendance a
        ON a.OccurrenceId = ao.Id
       AND a.DidAttend = 1
       AND a.EndDateTime IS NULL
    GROUP BY slot.ScheduleId, slot.LocationId
)
```

`COUNT(DISTINCT a.PersonAliasId)` returns `0` (not `NULL`) when the LEFT JOIN finds nothing, so empty rooms surface naturally with a zero count.

### Filtering check-in queries by campus at Vox

Vox check-in Groups have `CampusId = NULL`. Don't filter on `g.CampusId = @CampusId` — campus is at the Location level. Walk `Location.ParentLocationId` recursively to a campus root matched against `Campus.LocationId`. Full pattern in `memory/reference_vox_checkin_campus_model.md`.

## Generator script template

Source files keep apostrophes plain. A single `sed` pass at generation time doubles them for embedding in `N'…'` SQL string literals. (Cross-references `memory/feedback_dont_pre_escape_source.md`.)

```bash
#!/usr/bin/env bash
# sql/generate_<n>.sh - emits sql/<n>_apply.sql with embedded SQL/Lava bodies escaped.
set -euo pipefail
cd "$(dirname "$0")"

SQL_BODY="$(sed "s/'/''/g" <data-source>.sql)"
LAVA_BODY="$(sed "s/'/''/g" <template>.lava)"

cat > <n>_apply.sql <<'OUTER_EOF'
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Commit BIT = 0;
BEGIN TRAN;

-- … inserts/updates, with N'__SQL_BODY__' and N'__LAVA_BODY__' placeholders …

-- verification SELECTs

IF @Commit = 1 COMMIT TRAN; ELSE ROLLBACK TRAN;
OUTER_EOF

awk -v sql="$SQL_BODY" -v lava="$LAVA_BODY" '
{ gsub(/__SQL_BODY__/, sql); gsub(/__LAVA_BODY__/, lava); print }
' <n>_apply.sql > <n>_apply.sql.tmp
mv <n>_apply.sql.tmp <n>_apply.sql
```

Run with `sqlcmd … -f 65001 -I -b -i <n>_apply.sql`.

## Gotchas (priority order)

### 1. `Page.SiteId` is required (EF model), even though the column is nullable
**Symptom:** "An error has occurred while generating the page menu. Error details: The 'SiteId' property on 'Page' could not be set to a 'null' value." Page exists in the DB but the navigation menu render explodes.

**Fix:** Always set `Page.SiteId` and inherit from the parent page (see migration shape above). Do not look up a Layout by Name without scoping to a Site — `'Full Width'` exists on multiple sites (Rock RMS = SiteId 1; Rock Check-in Manager = SiteId 5; etc.) and picking the wrong one puts the page on the wrong nav surface.

### 2. `DisplayInNavWhen` value mapping is unintuitive
The `int` column has values **0 = When Allowed, 1 = Always, 2 = Never**. "2" reads natural-language like "always" but actually means "never," and the page won't appear in navigation. Default to `0` for nav-visible pages. Match what other children of the parent page use (`SELECT DisplayInNavWhen FROM Page WHERE ParentPageId = @ParentPageId`).

### 3. `AttributeQualifier` does NOT have `CreatedDateTime` / `ModifiedDateTime`
Most Rock tables do; this one doesn't. Including them in the INSERT fails with "Invalid column name." Use:
```sql
INSERT INTO AttributeQualifier (IsSystem, AttributeId, [Key], [Value], [Guid])
VALUES (0, @AttrId, 'fieldtype', 'ddl', NEWID());
```

### 4. `Key` is a reserved keyword in SQL Server
When using a `VALUES (…) v(…)` table-construct, never alias a column as `Key` — it errors with "Incorrect syntax near the keyword 'Key'." Use `K`, `KeyName`, or any non-reserved name. When referencing `Attribute.[Key]` in joins, always bracket it.

### 5. Block-level vs block-instance Attribute pattern is easy to invert
Block-level Attributes (the BlockType's settings like `Query`, `FormattedOutput`) are qualified on `BlockTypeId`. Block-instance Attributes (the individual filters configured on a PPF block) are qualified on `Id` (the specific Block.Id). Mixing these up means your INSERT silently produces nothing because the JOIN doesn't match.

Quick check: block-level Attributes already exist for any installed BlockType — you only INSERT *AttributeValue* rows. Block-instance Attributes are new rows you INSERT.

### 6. `GroupTypeAssociation` has cycles — recursive CTEs need cycle detection
**Symptom:** "The maximum recursion 100 has been exhausted before statement completion." Selecting a Check-in Configuration that involves the affected GroupType makes the Dynamic Data block error out.

**Why:** the prod `GroupTypeAssociation` table has cycles (e.g., Volunteers Check-in id 30 has a child that loops back), so a naive descendant walk runs forever.

**Fix:** track visited GroupTypeIds in a pipe-delimited path, only recurse when the candidate child isn't already in the path:

```sql
;WITH ConfigGroupTypes AS (
    SELECT @CheckInConfigId AS GroupTypeId,
           CAST('|' + CAST(@CheckInConfigId AS NVARCHAR(20)) + '|' AS NVARCHAR(MAX)) AS VisitedPath
    UNION ALL
    SELECT gta.ChildGroupTypeId,
           c.VisitedPath + CAST(gta.ChildGroupTypeId AS NVARCHAR(20)) + '|'
    FROM GroupTypeAssociation gta
    INNER JOIN ConfigGroupTypes c ON c.GroupTypeId = gta.GroupTypeId
    WHERE c.VisitedPath NOT LIKE '%|' + CAST(gta.ChildGroupTypeId AS NVARCHAR(20)) + '|%'
),
```

**Generalization:** any time you write a recursive CTE over `GroupTypeAssociation` (or any Rock parent/child table that lacks an explicit acyclic constraint), include cycle detection. `OPTION (MAXRECURSION N)` only delays the error; it doesn't fix the underlying cycle and the resulting set is still wrong.

### 8. AttributeQualifier `values` keys ≠ what gets saved for enum-backed attributes
For attributes that the block code parses against a C# enum (notably `PageParameterFilter.DoesSelectionCausePostback` → `SelectionAction`), the `AttributeQualifier.Value` for `values` lists **UI dropdown keys** like `nothing^,block^Update Block,page^Update Page` — but the block does `Enum.TryParse(stringValue)` against enum **member names** (`Nothing`, `UpdateBlock`, `UpdatePage`). Saving the dropdown keys throws on render: `'page' is not a member of the SelectionAction enumeration`.

**Rule:** before relying on the qualifier `values` list for what to save, find an existing block instance where the value is actually working and use that string verbatim. If only legacy/garbage values exist (e.g., `False`, `0`), guess from the enum member naming convention (PascalCase, matches the UI label like `Update Page` → `UpdatePage`) and verify on render. Don't trust qualifier dropdown keys for enum-backed attributes.

### 7. `ShowGrid` is not an attribute on Dynamic Data block
The grid is suppressed automatically when `FormattedOutput` is set. Trying to INSERT an AttributeValue with Key `ShowGrid` silently fails (the JOIN doesn't match) and adds nothing.

### 7. UTF-8 codepage when running migrations
Pass `-f 65001` to `sqlcmd` if any non-ASCII chars are in the file (em-dashes in PRINT comments, smart quotes, etc.). Without it, those bytes get mojibaked. Or stick to ASCII (use `-` not `–`, plain quotes not `"…"`). See `memory/feedback_sqlcmd_utf8.md`.

### 8. Don't pre-escape apostrophes in source files
Source `.sql` and `.lava` files for the generator use **plain single apostrophes only**, never `''`. The `sed` step in the generator escapes once. Pre-escaping doubles up to `''''`, the SQL parse stores `''`, and Lava can't parse the result. See `memory/feedback_dont_pre_escape_source.md`.

## Schema cheat sheet (Vox-confirmed; verify with a quick SELECT before relying on)

**EntityType IDs (subset relevant here):**
- 6 = Rock.Model.Block (the EntityType for Block-targeted Attributes)

**FieldType IDs:**
- 1 = Text
- 3 = Boolean
- 6 = Single-Select
- 7 = Integer
- 11 = Date
- 21 = Memo
- 46 = Date Range
- 51 = Code Editor
- 107 = Lava Commands

**BlockType IDs:**
- 6 = HTML Content (`~/Blocks/Cms/HtmlContentDetail.ascx`)
- 143 = Dynamic Data (`~/Blocks/Reporting/DynamicData.ascx`)
- 644 = Page Parameter Filter (`~/Blocks/Reporting/PageParameterFilter.ascx`)

**Block attribute qualifier (block-LEVEL):** `EntityTypeId=Block`, `EntityTypeQualifierColumn='BlockTypeId'`, `EntityTypeQualifierValue='<BlockType.Id as varchar>'`.

**Block attribute qualifier (block-INSTANCE):** `EntityTypeId=Block`, `EntityTypeQualifierColumn='Id'`, `EntityTypeQualifierValue='<Block.Id as varchar>'`.

## Reference — what was built in this skill's defining session (Project 4388)

Page **2486** "Room Capacity" under Check-in Manager (Page 300), inheriting Layout 30 / Site 5 (so the layout-level Campus Context Setter Block 478 supplies `Context.Campus`):

- **PPF Block 6177** — single filter `CheckInConfigGuid` (FieldType `33` Group Type, qualifier `groupTypePurposeValueGuid = 4A406CB0-495B-4795-B788-52BDFDE00B01` for the "Check-in Template" purpose), `DoesSelectionCausePostback = UpdatePage` (single-filter default)
- **DD Block 6178** — Lava-driven SQL filtered by `{{ PageParameter.CheckInConfigGuid }}` and `{{ Context.Campus.Id }}`. Campus filter via `Location.ParentLocationId` chain matched to `Campus.LocationId` (because Vox check-in groups have `CampusId = NULL`). Day filter combines `Schedule.WeeklyDayOfWeek` with `iCalendarContent` BYDAY/FREQ scan for non-weekly schedules. Renders dashboard-style cards (4 / 2 / 1 col responsive) with status pills, accent stripes, and a buttonized auto-refresh footer (countdown + pause/resume).

Migration files at `claudefiles/rock/projects/4388/sql/` (idempotent on Page Guid `7E9B30C5-1A14-4D3D-BF11-30C5DDF11A14`):

- `02-create-room-capacity-page.sql` — initial Page + PPF + DD wiring
- `03-update-config-filter-and-icon.sql` — switch PPF filter to FieldType 33, set MenuDisplayIcon
- `04` / `05-fix-selection-action(-enum).sql` — DoesSelectionCausePostback enum-name fix
- `06-fix-cte-cycle.sql` — cycle-safe `GroupTypeAssociation` recursion (Volunteers had a loop)
- `07-show-all-active-schedules.sql` — drop attendance gate; show every configured slot
- `08-filter-by-todays-day.sql` — add `WeeklyDayOfWeek = @TodayDayOfWeek` clause
- `09-scan-ical-for-day.sql` — extend day filter to scan `iCalendarContent` (never committed, superseded by 10)
- `10-fix-campus-via-location-chain.sql` — campus through `Location` parent walk; folds in iCal scan
- `11-improve-output-ux.sql` — dashboard CSS + per-schedule summary chips + status pills
- `12-add-pause-refresh-button.sql` — buttonized auto-refresh + manual accumulator (Lava `Sum` was returning 0)

## Cross-references

- `rock-workflow-deploy/SKILL.md` — for ServiceJob / WorkflowType creation half of any page-with-workflow flow.
- `rock-project-review/SKILL.md` — for the project-context flow that often surrounds page-creation work.
- `memory/feedback_dont_pre_escape_source.md` — escape rule for the generator.
- `memory/feedback_sqlcmd_utf8.md` — `-f 65001` when migration files contain non-ASCII.
- `memory/feedback_servicejob_history_default.md` — closely related defaults pattern (HistoryCount=500 on new ServiceJobs).
- `memory/reference_attr_cache_refresh.md` — cache flush after AttributeValue migrations (user handles routinely).
- `memory/reference_vox_checkin_campus_model.md` — Vox check-in groups have `CampusId = NULL`; campus via Location chain.
- `memory/feedback_auto_refresh_pause_resume.md` — buttonized refresh footer is the default for live dashboards.
