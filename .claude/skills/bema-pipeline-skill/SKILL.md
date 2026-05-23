---
name: bema-pipeline-skill
description: Use this skill any time you read, modify, debug, or extend a BEMA Pipeline (BlueBoxMoon Pipeline / `_com_bemaservices_BemaPipeline_*`). Triggers on the words "pipeline", "BEMA pipeline", "BBM pipeline", "connection pipeline", "pipeline step", "pipeline action", "ProcessLogic" / "Process Logic", or any reference to per-step Lava that determines whether a pipeline action runs. Pairs with rock-project-review (which routes here when a Rock Request involves pipeline work) and rock-workflow-deploy (for the workflows that pipeline actions launch).
---

# BEMA Pipeline reference

The BEMA Pipeline plugin (`com.bemaservices.BemaPipeline`) layers a step-wise pipeline on top of a parent entity (almost always `ConnectionRequest`). Pipelines drive Vox's volunteer onboarding (Kids, Security, Hospitality, Media), Community Group Leader onboarding, Prayer Team onboarding, Vox Residency, and the new-person Prepare & Enrich flow.

**Before doing anything in this surface area, check whether the work belongs to project 6188's audit body.** That project produced:
- `claudefiles/rock/projects/6188/notes/CLAUDE_NOTES.md` — schema map, state enum values, QA-family conventions, the comm-history audit narrative.
- `claudefiles/rock/projects/6188/notes/README.md` — QA-script Guid scheme + backout dependency order.
- `claudefiles/rock/projects/6188/lava/*.lava` — canonical reusable ProcessLogic templates (Requirement gating, Immediate variant, WaitUntil variant) **plus** the DefaultPipelineActionDisplay shortcode-friendly display Lava.
- `claudefiles/rock/projects/6188/docs/NN-<pipeline>.docx` — narrative documentation per pipeline (one DOCX per active pipeline; pipelines 2/3/6/8 deliberately skipped).
- `claudefiles/rock/projects/6188/sql/pipelines/NN-<pipeline>-{create,backout}.sql` — deterministic QA seed/teardown per pipeline.
- `claudefiles/rock/projects/6188/sql/investigations/*.sql` — the long tail of remediation scripts that produced the current Vox conventions (BGC ProcessLogic fixes, Pastoral Conversation gating, stuck-pipeline cleanup, etc.).

If a 6188 investigation script already exists for the exact thing you're about to do, **mirror its shape** instead of inventing one — the BGC ProcessLogic fixes and AddCglFailNotificationStep are the most reusable templates.

## Schema map (`_com_bemaservices_BemaPipeline_*`)

| Table | Purpose | Key columns |
|---|---|---|
| `BemaPipelineType` | Pipeline definition | `Id`, `Name`, `EntityTypeId` (filter `= 240` for ConnectionRequest pipelines), `IsActive` |
| `BemaPipelineActionType` | Step definition | `Id`, `Name`, `BemaPipelineTypeId`, `[Order]`, `ComponentEntityTypeId`, `IsActive` |
| `BemaPipeline` | Instance per parent entity | `Id`, `BemaPipelineTypeId`, `EntityId` (= `ConnectionRequest.Id`), `BemaPipelineState`, `ActivatedDateTime`, `CompletedDateTime` |
| `BemaPipelineAction` | Step instance | `Id`, `BemaPipelineId`, `BemaPipelineActionTypeId`, `BemaPipelineActionState`, `ActivatedDateTime`, `LastProcessedDateTime`, `CompletedDateTime` |

**Vox pipeline inventory** (`BemaPipelineTypeId` → name; pipelines 2/3/6/8 deliberately omitted):
1 Prepare and Enrich · 4 Kids Volunteer Onboarding · 5 Security Volunteer Onboarding · 7 Hospitality Volunteer Onboarding · 10 Vox Residency · 11 Prayer Team Onboarding · 12 Community Group Leader Onboarding · 13 Media Volunteer Onboarding.

### State enum values

**`BemaPipelineActionState`** (best-guess from framework convention; 4=Completed and 6=Skipped verified from data; the rest match the C# enum order):
`0 Pending · 1 ReadyToProcess · 2 WaitingOnItems · 3 Errored · 4 Completed · 5 Failed · 6 Skipped`

**`BemaPipelineState`**: `0 Pending · 1 Active · 2 Completed`

Skipped rows **do** populate `CompletedDateTime` (verified 5011/5011 in the BGC Conversation fix). So a `CompletedDateTime IS NOT NULL` check correctly treats Skipped as "done" for downstream "wait for previous actions" loops.

## Component classes and the per-action AttributeId map (critical)

Each `BemaPipelineActionType` has a `ComponentEntityTypeId` pointing at one of four component classes. The plugin registers a duplicate set of base attributes (ProcessLogic / DisplayLogic / Instructions / DisplayedLava / AllowManualOverride / ButtonText / IconCssClass / TimeoutWorkflows) per component class. **Per-action overrides are stored against the component-class Attribute, not the model-scoped Attribute.**

This is the single biggest gotcha in this codebase. Looking at the wrong Attribute Id will make you conclude "no override is set, defaults apply" when in fact a custom Lava override is live in prod.

### Component EntityTypeId → component class

| ComponentEntityTypeId | Class | Friendly name |
|---|---|---|
| `895` | `com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow` | Launch Workflow (the workhorse — most actions) |
| `897` | `com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionSendCommunication` | Send Communication |
| `898` | `com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionSendSystemCommunication` | Send System Communication |
| `899` | `com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionUpdateConnectionRequest` | Update Connection Request |

### Per-action override AttributeId map

| Key | LaunchWorkflow (895) | SendCommunication (897) | SendSystemCommunication (898) | UpdateConnectionRequest (899) |
|---|---|---|---|---|
| **ProcessLogic** | **18031** | 18003 | 18017 | 18046 |
| DisplayLogic | (none, not yet used) | (none) | 18019 | (none) |
| AllowManualOverride | 18034 | 18006 | 18020 | 18049 |
| ButtonText | 18035 | 18007 | 18021 | 18050 |
| IconCssClass | 18036 | 18008 | 18022 | 18051 |
| DisplayedLava | 18038 | 18010 | 18024 | 18053 |
| WorkflowType | 18027 | — | — | — |
| WorkflowAttributes | 18029 | — | — | — |
| WorkflowEntryPage | 18030 | — | — | — |
| WorkflowNameTemplate | 18028 | — | — | — |

(Numbers verified against prod — count of `AttributeValue` rows is non-zero for each cell shown above.)

### Decoy / duplicate ProcessLogic attributes — IGNORE

There are **five** model-scoped ProcessLogic Attribute rows (`EntityType = BemaPipelineActionType` model, sometimes with a `BemaPipelineActionTypeEntityTypeId` qualifier):

| AttrId | Qualifier |
|---|---|
| 17973 | (no qualifier — bare model attribute) |
| 18061 | `BemaPipelineActionTypeEntityTypeId = 897` |
| 18075 | `BemaPipelineActionTypeEntityTypeId = 898` |
| 18089 | `BemaPipelineActionTypeEntityTypeId = 895` |
| 18104 | `BemaPipelineActionTypeEntityTypeId = 899` |

**No `AttributeValue` rows are ever stored against these.** They appear to be artifacts of the plugin's migration history. If you write a value to one of them, the plugin will not read it. Always target the component-class Attribute from the table above.

### How to look up the right AttributeId on the fly

When in doubt, query by component class name (not Id):

```sql
SELECT a.Id, a.[Key], et.Name AS ComponentClass
FROM Attribute a
INNER JOIN EntityType et ON et.Id = a.EntityTypeId
WHERE a.[Key] = 'ProcessLogic'
  AND et.Name = 'com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow';
```

And to find where overrides actually live for a given key (the "ground truth" map for your DB):

```sql
SELECT a.[Key], a.Id, et.Name, COUNT(av.Id) AS PerActionRows
FROM Attribute a
INNER JOIN EntityType et ON et.Id = a.EntityTypeId
LEFT JOIN AttributeValue av ON av.AttributeId = a.Id
WHERE a.[Key] IN ('ProcessLogic','DisplayLogic','Instructions','ButtonText')
  AND et.Name LIKE '%BemaPipeline%'
GROUP BY a.[Key], a.Id, et.Name
HAVING COUNT(av.Id) > 0
ORDER BY a.[Key], et.Name;
```

## Reading an existing ProcessLogic before editing

**Never write a ProcessLogic value without reading the current value first.** Vox has accumulated nontrivial gating logic on many actions — see "Vox conventions" below. Overwriting blindly clobbers behavior that downstream steps depend on.

```sql
-- Pull the current ProcessLogic for a specific action
SELECT av.Id, av.Value
FROM AttributeValue av
INNER JOIN Attribute a ON a.Id = av.AttributeId
INNER JOIN EntityType et ON et.Id = a.EntityTypeId
WHERE et.Name = 'com.bemaservices.BemaPipeline.BemaPipelineActionTypes.BemaPipelineActionLaunchWorkflow'
  AND a.[Key] = 'ProcessLogic'
  AND av.EntityId = <pipeline action type Id>;
```

If `av.Id` comes back, **UPDATE that row** in your migration. If it doesn't (no override stored yet), then the action falls through to `Attribute.DefaultValue` on AttributeId 18031 (which is itself a non-empty loop Lava — empty does not mean missing). Inserting a new AttributeValue row in that case is correct.

When verifying a one-row UPDATE, use `@@ROWCOUNT` immediately after and RAISERROR/ROLLBACK if it isn't 1 (per the 6188 `Fix*ProcessLogic.sql` template).

## ProcessLogic Lava — semantics

The plugin parses the Lava output and converts it to `BemaPipelineActionState` via `ConvertToEnum<BemaPipelineActionState>(WaitingOnItems)` — so any unrecognized value defaults to `WaitingOnItems`.

**Recognized return values** (source: `BemaPipelineActionTypeComponent.cs:238-295`):
- `WaitingOnItems` — hold; don't show a launch button; re-evaluate next pipeline tick.
- `ReadyForManualAction` — show the launch button; do not auto-fire.
- `ReadyToProcess` — fire the component's `ProcessAction` immediately (for LaunchWorkflow that means activate + process the workflow). Also shows the launch button until the workflow completes.
- `Completed` — mark the action complete now. For LaunchWorkflow, also marks any in-flight associated workflow as "Completed By Pipeline".
- `Skipped` — same effect as `Completed` for LaunchWorkflow (marks the workflow + the action complete), but read as semantically "we deliberately didn't run this".
- `TimedOut` — launches `TimeoutWorkflows` and marks the whole pipeline complete.
- `ActionTypeArchived` — marks action complete without running. Rare.

**Default state if the variable is never assigned**: `WaitingOnItems`. So the typical pattern is to seed `logic = 'ReadyToProcess'`, walk PreviousActions to maybe demote to `WaitingOnItems`, then layer overrides that promote to `Completed` / `Skipped`.

### Lava merge fields available in ProcessLogic

From `BemaPipelineActionTypeComponent.GetMergeFields` (`com.bemaservices.BemaPipeline/BemaPipelineActionTypes/BemaPipelineActionTypeComponent.cs:415-448`):

- `Pipeline` — the `BemaPipeline` instance
- `<EntityFriendlyNameNoSpaces>` — the parent entity, keyed by `EntityType.FriendlyName.RemoveSpaces()`. For ConnectionRequest pipelines this is **`ConnectionRequest`**.
- `EntityType`
- `Action` — current `BemaPipelineAction` (use `Action.BemaPipelineActionType` to reach the type cache)
- `ActionType` — alias for `Action.ActionTypeCache`
- `PreviousActions` / `LastAction` / `FutureActions` / `NextAction`

Loop-var name collision: `Action` is a merge field AND a frequent loop variable name. The plugin's own default loop names the variable `action` (lowercase) which technically shadows the merge field inside the loop — fine, since the loop only uses `action.CompletedDateTime`, but worth knowing if you reach `Action.BemaPipelineActionType.Order` from inside a loop body.

### Boolean attribute Lava semantics

`{{ ConnectionRequest | Attribute:'StopProcessing' }}` returns the **formatted** value, which for Boolean attrs is `"Yes"` or `"No"` (or empty string if unset). NOT `"True"` / `"False"`.

To get the raw stored value (`True` / `False`), pass `'RawValue'` as the second arg: `{{ ConnectionRequest | Attribute:'StopProcessing','RawValue' }}`.

The Vox-installed ProcessLogic compares against `"Yes"`. Match that convention in any new gating you add, unless you have a reason to use RawValue.

## Vox conventions encoded in production ProcessLogic

These are the patterns you should expect to find and preserve on action 89/90/92 of pipeline 12 (CGL), and the parallel actions on pipelines 4 (Kids) and 5 (Security):

1. **Defensive `StopProcessing == "Yes"` guard** — set by upstream workflows (e.g., "4. Background Check Conversation" sets it on Fail). When true, demote `logic` to `Skipped` so downstream actions don't fire. Almost every late-pipeline action in CGL has this guard. Don't strip it.

2. **Excluding an upstream action from the "all previous done" wait loop** — pattern used on CGL actions 89 and 90:
   ```lava
   {%- for action in PreviousActions -%}
       {%- if action.BemaPipelineActionTypeId == 88 -%}
           {% continue %}
       {%- endif -%}
       {%- if action.CompletedDateTime == null or action.CompletedDateTime == empty -%}
           {%- assign logic = 'WaitingOnItems' -%}
           {%- break -%}
       {%- endif -%}
   {%- endfor -%}
   ```
   Action 88 ("Submit Group Details") is data-collection that may never fire if the leader's group details get captured through the Review Details & Interview form instead. Skipping 88 in the wait loop lets the later actions advance regardless.

3. **The Requirement include pattern** (Kids / Security BGC Completed steps) — uses Vox's reusable Lava libraries at `~/Plugins/com_razayya/Pipeline/`:
   - `ProcessLogicWithRequirement.lava` — checks `Action.BemaPipelineActionType | Attribute:'Requirement','RawValue'` against a `GroupRequirementType` whose DataView the person must match. `Completed` on match.
   - `ProcessLogicWithRequirementImmediate.lava` — same logic but used for actions that don't wait for previous actions (variants identical at the time of capture — check before reusing).
   - `ProcessLogicWaitUntilRequirementMet.lava` — inverse: `WaitingOnItems` until the DataView matches, then `ReadyToProcess`.
   - Use them via `{% capture logic %}{% include '~/Plugins/com_razayya/Pipeline/ProcessLogicWithRequirement.lava' %}{% endcapture %}{% assign logic = logic | Trim %}` then layer your overrides. The Trim is required — the include leaves whitespace that breaks `ConvertToEnum`.

4. **BGC Completed gating** (Kids/Security/CGL actions 30/42/87) — wait for the safe default, then promote on `bgCheckResult == 'Pass' AND bgCheckDate within 3 years`, demote to `Skipped` on `'Fail'`, otherwise hold `WaitingOnItems`. See `FixBgcCompletedProcessLogic.sql` in 6188 investigations for the canonical pattern.

5. **BGC Conversation gating** (CGL action 92) — gate on `bgCheckResult == 'Fail'` instead of any flag attribute. The workflow it launches itself sets `StopProcessing = 'Yes'`, which ejects the rest of the pipeline. See `FixBgcConversationProcessLogic.sql`.

When in doubt about a single action's current gating, **dump its Lava and a couple of neighbors' Lava** before editing. Pasting "before / after / net delta" to the user when proposing a change is the right move.

## Cloning a pipeline action

When you need a new pipeline action that mirrors an existing one (only differing in name/order/maybe a couple of fields):

1. Capture the source action's `Id` and its `ComponentEntityTypeId` (almost always 895 for new work).
2. `INSERT INTO _com_bemaservices_BemaPipeline_BemaPipelineActionType (Name, ComponentEntityTypeId, BemaPipelineTypeId, [Order], IsActive, [Guid], CreatedDateTime, ModifiedDateTime)` — shift downstream `[Order]` values first.
3. Capture the new `Id` via `SCOPE_IDENTITY()`.
4. Copy AttributeValues with a JOIN to scope to the right component:
   ```sql
   INSERT INTO AttributeValue (IsSystem, AttributeId, EntityId, Value, [Guid], CreatedDateTime, ModifiedDateTime, IsPersistedValueDirty)
   SELECT 0, av.AttributeId, @NewActionTypeId, av.Value, NEWID(), GETDATE(), GETDATE(), 1
   FROM AttributeValue av
   INNER JOIN Attribute a ON a.Id = av.AttributeId
   WHERE av.EntityId = @SourceActionTypeId
     AND a.EntityTypeId = @LaunchWorkflowComponentEntityTypeId;
   ```
   The `INNER JOIN Attribute` + `EntityTypeId` filter is **mandatory**. `AttributeValue.EntityId` is an untyped int — without scoping by the component-class EntityType, you will silently copy AttributeValues from Person rows, Group rows, anything that happens to share the numeric Id.
5. Verify counts: source vs new should match. `RAISERROR + ROLLBACK` on mismatch. (See `AddCglFailNotificationStep.sql` in 6188 investigations for the canonical 3-step.)
6. Override only the fields that need to differ (Name, Description, optionally ProcessLogic).

## The Retrigger Pipeline Activity pattern (resend email, re-fire activity)

When a pipeline action launches a workflow that sends an email (Sign MLE, Application form, Background-check intake, etc.), Vox staff frequently need to **re-fire that email** without restarting the whole pipeline — typical reasons: the recipient's email was typo'd and now corrected; they missed a deadline; the original send landed in spam.

Vox has a standard pattern for this, wired up on **5 pipelines** as of 2026-05-20:

| Pipeline | Action | Resend WT |
|---|---|---|
| 11 Prayer | 80 "Prayer Team Application" | WT 388 |
| 13 Media | 94 "Media Team Application" | WT 469 |
| 7 Hospitality | (volunteer forms action) | WT 366 |
| 1 P&E | (registration link action) | WT 214 |
| 12 CGL | 85 "Sign Ministry Leader Expectations" | WT 480 |

Two generic-template WTs also exist (WT 474 "Pipeline Activity Retrigger", WT 493 "Retrigger Pipeline Workflow Activity") — same shape, intended as starting points; clone+target-config per use.

### Two artifacts per retriggerable step

**1. A small Resend workflow type** — 5 actions in one Start activity:

| Order | Name | Class |
|---|---|---|
| 0 | Workflow Complete if no Workflow Id | `Rock.Workflow.Action.CompleteWorkflow` (criteria on WorkflowId1 attr) |
| 1 | Set Target Workflow | `Rock.Workflow.Action.RunLava` (resolves `WorkflowId1` text → Workflow entity into `TargetWorkflow` attr) |
| 2 | Activate Send Email | Activate Other Activity (re-fires a specific activity on the target workflow — identified by Activity attr) |
| 3 | Redirect | `Rock.Workflow.Action.Redirect` (uses `Redirect` text attr) |
| 4 | Close Previous Activities | `Rock.Workflow.Action.RunSQL` (closes prior in-flight WorkflowActivity rows on the target so the re-fire starts clean) |

Workflow attrs on the Resend WT (input contract — bind via query string):

| Key | FieldType | Bound from |
|---|---|---|
| `WorkflowId1` | Text | URL query `?WorkflowId1=…` |
| `TargetWorkflow` | Workflow | populated by action 1 |
| `Activity` | Workflow Activity | static — the specific activity Guid on the target type to re-activate |
| `Redirect` | Text | URL query `?Redirect=…` |

Each Resend WT is target-specific (the `Activity` attr points at a specific activity Guid on a specific target WorkflowType). You can't reuse one Resend WT across different target workflows. Pattern is: clone the Prayer/MLE template, swap the target activity reference.

**2. Customized `DisplayedLava` on the source pipeline action** — renders a "Retrigger {{ActionType.Name}}" button on the action card when the action is pending and a launched workflow exists. Two-`{% sql %}`-block scaffold prepended at the top of the DisplayedLava resolves:

- `workflowId` — the Id of the workflow this pipeline-action instance launched. Looked up via the `BemaPipelineAction.Workflow` per-instance attribute (**`AttrId 17972`**, key `Workflow`, EntityType `com.bemaservices.BemaPipeline.Model.BemaPipelineAction`). The value is the workflow's Guid; the `{% sql %}` joins through to the Workflow row to return the Id.
- `latest` — `MAX(WorkflowActivity.CreatedDateTime)` on that workflow. Proxy for "when was the last send" surfaced in the UI as `Last send: M/d/yyyy`.

The button URL:
```
/workflowentry/<resendWtId>?WorkflowId1=<workflowId>&Redirect=<urlencoded CR detail URL>
```
For Vox the CR detail URL is `/page/1270?ConnectionRequestId={{ ConnectionRequest.Id }}` (same page for every Connection Opportunity).

Guard the button on `state != 'Completed' and state != 'Skipped' and workflowId is not empty/null`. (Lava AND chains parse unambiguously even though parens aren't supported — see `rock-workflow-deploy` skill gotcha #10.)

### `BemaPipelineAction.Workflow` (AttrId 17972) — important

Not to be confused with the per-component-class config attrs in the AttributeId map above. AttrId 17972 is on the **per-instance pipeline-action row** (`BemaPipelineAction`, not `BemaPipelineActionType`). It stores the Guid of the workflow that THAT specific pipeline-action instance launched. Used by:
- The retrigger DisplayedLava (above) to find the launched workflow.
- The default DisplayedLava's `{% assign workflowStatus = Action | Attribute:'Workflow','Status' %}` pattern (showing the workflow's Status name on the card badge).
- The plugin's own `CompleteWorkflow()` helper when the action transitions to Completed/Skipped/Timeout (marks the linked workflow done too).

To query the launched workflow for a specific CR + action type:
```sql
SELECT W.Id, W.[Guid], W.Status
FROM _com_bemaservices_BemaPipeline_BemaPipeline BP
JOIN _com_bemaservices_BemaPipeline_BemaPipelineAction BPA ON BPA.BemaPipelineId = BP.Id
JOIN AttributeValue AV ON AV.EntityId = BPA.Id AND AV.AttributeId = 17972
JOIN Workflow W ON W.[Guid] = TRY_CAST(AV.[Value] AS UNIQUEIDENTIFIER)
WHERE BP.EntityId = <connection request id>
  AND BPA.BemaPipelineActionTypeId = <pipeline action type id>;
```

### Gotcha: `ActionLinks == empty` gating hides the button on staff-launched workflows

The Prayer/Media DisplayedLavas wrap the retrigger button **inside** `{% if ActionLinks == empty %}`. That works for them because their underlying workflows assign the user-entry form to the **volunteer** — staff viewing the pipeline UI see `ActionLinks` empty (no launch button for them), so the badge + retrigger render.

CGL Step 1 (action 85, WT 424 "1. Ministry Leader Expectations") is **staff-launched** — the form isn't assigned to anyone specific because staff sometimes pull it up in-person with the volunteer present and sign together. So staff *always* see a launch button → `ActionLinks` is never empty → if the retrigger lived inside that guard, it'd never render.

**Rule of thumb:** before mimicking the Prayer/Media structure, check whether the underlying workflow assigns its user-entry form to a specific person/group. If yes, follow Prayer's nested pattern. If no (or if the workflow is staff-launched for any other reason), put the retrigger block **outside** the `{% if ActionLinks == empty %}` guard so it renders alongside the launch button. The state + workflowId gating still ensures it only shows when retrigger makes sense.

Reference: `claudefiles/rock/projects/6250/06-mle-resend-show-with-launch.sql` does this surgical unnest on action 85.

### Recipe for adding retrigger to a new pipeline action

1. **Find the underlying workflow type** that the pipeline action launches (look at `WorkflowType` config attr on the action — AttrId 18027 for LaunchWorkflow).
2. **Find the activity Guid on that target type that sends the email** — usually a "Send Email" activity. Get its Guid.
3. **Clone an existing Resend WT** (e.g. WT 480, WT 388, WT 469) — copy the 5 actions + the 4 workflow attrs into a new WT. Update the `Activity` attribute on the cloned action 2 ("Activate Send Email") to point at the target activity Guid from step 2.
4. **UPDATE the source pipeline action's DisplayedLava** — prepend the two `{% sql %}` blocks (workflowId + latest), insert the button block. Reference implementation: `claudefiles/rock/projects/6250/05-wire-mle-resend.sql`.
5. **Clear Rock cache** so the new DisplayedLava + the new WT are visible.

The two pieces are independent — you can build the WT first, leave the UI unwired (Roza did this with WT 480 for 6+ weeks before we wired it up), or vice versa. The button URL just won't work until both exist.

## Cache: always tell the user to clear it after editing

Editing any `BemaPipelineActionType`, its config AttributeValues (ProcessLogic, ButtonText, etc.), or shifting `[Order]` requires Rock's cache to be cleared before the change takes effect (the pipeline UI reads from `BemaPipelineActionTypeCache`). Tell the user explicitly:

> Admin Tools → System Settings → Cache Manager → "Clear All Cache"  *(or wait for the Rock Cleanup job)*

## EntityId-untyped-int gotcha (general but bites here)

`AttributeValue.EntityId` is just an int. If you write `WHERE av.EntityId = 90` without also scoping by `Attribute.EntityType`, you'll get AttributeValue rows for **Person 90, Group 90, ConnectionRequest 90, etc.** — anything that happens to share the integer 90.

Always join through `Attribute` + `EntityType` and filter by `EntityType.Name` (or `Attribute.Id` for a specific attribute). This bit me once already on a verification SELECT; the canonical filter is `a.EntityTypeQualifierColumn = 'EntityTypeId' AND a.EntityTypeQualifierValue = CAST(<componentTypeId> AS NVARCHAR(50))` when scoping to a component-class qualifier, or `et.Name = '<ClassName>'` when the Attribute itself is scoped at the entity-type level (which is the case for the component-class attribute Ids above).

## Connection between BEMA pipelines and the Connection module

A `BemaPipeline` row is created when a `ConnectionRequest` is created on an opportunity that has a pipeline configured. Configuration lives on `ConnectionOpportunity.AdditionalSettingsJson` (Vox-specific) or via the BEMA setup UI on the ConnectionType. To map an opp to its pipeline type, the simplest read is on existing data:

```sql
SELECT TOP 1 bp.BemaPipelineTypeId
FROM _com_bemaservices_BemaPipeline_BemaPipeline bp
INNER JOIN ConnectionRequest cr ON cr.Id = bp.EntityId
WHERE cr.ConnectionOpportunityId = <oppId>
ORDER BY bp.Id DESC;
```

(CGL opp 105 → pipeline 12. Each volunteer opp typically maps 1:1 to its onboarding pipeline.)

## Recurrent debugging recipes

- **"This pipeline is stuck"** — pull `BemaPipelineAction` rows for the BemaPipeline, find the lowest `[Order]` action with `BemaPipelineActionState` not in (4, 6). Look at its ProcessLogic, then at the merge-field values it depends on (StopProcessing, BackgroundCheckResult, etc.). 6188 has `UnstickPipeline4597.sql` / `CleanupStuckPipelines.sql` / `CheckPipelineIsProcessing.sql` as references.
- **"This action is firing when it shouldn't"** — dump the ProcessLogic with the actual current values of the merge fields substituted in (run the Lava manually in the Rock Lava tester at `/admin/cms/lava-tester`).
- **"Action 90 shows 'state 4' for all recent runs but never completed properly"** — `BemaPipelineActionState = 4` is `Completed`. If `CompletedDateTime IS NULL` on those, something fired the state change directly without going through the component (e.g. a `ProcessBemaPipelines` job tick caught a Skipped → Completed handler). Cross-check the linked Workflow status.

## Related skills and memories

- `rock-project-review` — projects that touch a pipeline route through both skills. The QA seed scripts in 6188 are the gold standard for "I need to test this end-to-end without polluting prod data."
- `rock-workflow-deploy` — for the workflows that pipeline actions launch (which is most of them).
- `memory/feedback_pipeline_docs_one_at_a_time.md` — when authoring the DOCX docs, one pipeline per turn, skip pipelines 2/3/6/8.
- `memory/reference_qa_pipeline_sql.md` — pointer to 6188's QA scripts.

## Pipeline-by-pipeline reference (the 6188 audit body)

The 8 active Vox pipelines, their entry mechanisms, action maps, and Vox-specific gating quirks. Each one has a richer narrative in `claudefiles/rock/projects/6188/docs/NN-<name>.docx` (Overview + Triggers); pull the DOCX when you need more detail than the rows below. Pipelines **2** (Welcome Card?), **3**, **6** (Security Documents Signing), **8** (Vox Kids Documents Signing) are deliberately excluded from this skill — the user instructed they be skipped.

### Cross-pipeline shared workflows / jobs

| Reused thing | Id | Guid | Purpose |
|---|---|---|---|
| WF 325 Launch Volunteer Process Pipeline from CR Entity | 325 | `823906AB-C9E1-464C-ACA4-9D852FAF24F5` | Reads CO's `OnboardingProcess` attr → launches the pointed pipeline. Wired to ConnectionType 14 (Volunteer Onboarding) Request-Started trigger. Auto-launches Kids/Security/CGL/Hospitality. |
| WF 449 Background Check (Volunteer Pipeline - MD) | 449 | `5455C7DC-16F9-440F-88DB-3F98FD95DDCD` | MD background-check wrapper used by Kids, Security, CGL. Pre-populates address/birthdate, hands off to Rock's BG workflow. Each caller passes a `Ministry` Guid identifying the placement. |
| WF 322 Failed Background Check Notification | 322 | `723CF3EE-0B51-4360-8DFB-838089B0213E` | Sends fail email, sets `StopProcessing=Yes`. Used by Kids/Security/CGL. |
| WF 303 Add Person to Placement Group | 303 | `1470A135-0A47-4354-940A-928DAACF9071` | Adds CR's person to its `AssignedGroup`; falls back to SQL-derived campus-matched placement group. Used by Kids/Security/Hospitality/Prayer/Media. |
| WF 299 Alternate Recommendation | 299 | `CA0071FA-0A27-467A-A8D2-B01434455A89` | The "Not a good fit for X" off-ramp. Opens recommendation form, transfers CR to a different opp, sets default connector there, sets `StopProcessing=Yes`, inactivates CR. Used by Kids/Security/Hospitality. |
| GroupRequirementType 1 "Background Check Required" | 1 | — | Points at DataView 6 ("Background check is still valid": age<18 OR `BackgroundChecked=True AND BackgroundCheckDate within 1095 days AND BackgroundCheckResult=Pass`). Drives the BG-required gating across Kids/Security/CGL. |
| ServiceJob 128 Process Bema Pipeline | 128 | — | Generic 10-min ticker (`com.bemaservices.RoomManagement.Jobs.ProcessBemaPipelines`). Re-evaluates every open pipeline's ProcessLogic and advances ready actions. |
| ServiceJob 257 Process Stuck VoxKids/VoxSecurity Pipelines | 257 | — | Kids+Security-specific 10-min `RunSQL` job that force-marks BG Completed steps as Completed when the wrapper workflow leaves them stuck despite a valid recent pass. Stamps tagged rows with `ForeignKey='AutoFixed by Job'`. Doesn't apply to CGL. |
| Retrigger Resend WT family — WT 388 Prayer, 469 Media, 366 Hospitality, 214 P&E, 480 CGL MLE, plus 474/493 generic templates | various | various | Re-fire a pipeline action's email-send activity without restarting the workflow. See "Retrigger Pipeline Activity pattern" section below. |
| Lava include — `~/Plugins/com_razayya/Pipeline/ProcessLogicWithRequirement.lava` | — | — | Vox-built ProcessLogic helper. Reads `Action.BemaPipelineActionType | Attribute:'Requirement','RawValue'` → walks GroupRequirementType DataViews → returns `Completed` on match, else falls through to the standard wait loop. Two siblings: `ProcessLogicWithRequirementImmediate.lava` (identical to the With version at last capture — confirm before reusing), and `ProcessLogicWaitUntilRequirementMet.lava` (inverse: `WaitingOnItems` until DataView matches). |
| PCO sync — `_9embers_CommonSync_IdentifierMap` | — | — | The 9embers/CommonSync table that maps a Rock PersonAlias to a Planning Center person. Pipeline actions that gate "wait for PCO" inline a `{% sql %}` block joining via TranslatorId `'RockRMS'` (Rock side) and `'767541fb054242003bb38558d3b8f42cd817f7dd03558aac587d5c0d544cdc18'` (PCO side). |

### Pipeline 1 — Prepare and Enrich

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 1 / *(check `docs/01-Prepare-and-Enrich.docx`)* |
| Connection Opportunity | "Prepare and Enrich Inquiry" |
| Connection Type | — (its own type) |
| Entry | **Direct from ingress workflow** "P&E Interest Form" (public form). The form matches/creates primary contact + spouse, creates the CR, saves form fields as CR attrs, then explicitly launches the pipeline. |
| Steps (9) | 1 Interview · 2 Send Registration Link · 3 Initial Payment · 4 Assessments · 5 Assign Facilitators · 6 P&E Sessions · 7 Send Ceremony Payment Link (Premarital only) · 8 Notify Coordinator of Completion · 9 Complete |
| Key gating | Most steps skip when CR state is `Inactive` or `Connected`. Step 7 also skips when `Type = Marriage Enrichment` (Type=0 Premarital, Type=1 Enrichment). Step 6 has Finish/Escalate/Re-Think buttons; only Finish completes the step. Steps 3 and 8 don't auto-skip — they hold/run regardless. |
| StopProcessing sources | Step 1 Deny (inactivates CR, downstream skips kick in via Inactive/Connected gating). |
| Doc | `01-Prepare-and-Enrich.docx` |

### Pipeline 4 — Kids Volunteer Onboarding

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 4 / `3F7E996A-691D-4B2E-B98C-D742600BBE48` |
| Connection Opportunity | 98 VoxKids (`DB3BCFBE-B4D6-4F3A-BB16-3B6AA89D4FB1`), CO has `OnboardingProcess = <pipeline 4 guid>` |
| Connection Type | 14 Volunteer Onboarding |
| Entry | Auto via WF 325 (Request-Started trigger on ConnectionType 14). Coordinator manually creates the CR in the VoxKids opportunity. No public-facing form. |
| Action Map | 0 Connection/Interview (WF 298 VoxKids Interview, `68F7BE88-…`) · 2 Initiate BG Check (WF 449) · 3 BG Check Completed (`UpdateConnectionRequest` — sets `StopProcessing` on Fail) · 4 BG Fail Notification (WF 322) · 6 Training (WF 304 Training & Ministry Grid Email, `884111C4-…`) · 7 E-Forms (WF 297 VoxKids volunteer e-Forms, `AD1EA531-…`, signs **Volunteer Agreement + Policies/Procedures**) · 8 Placement Group (WF 303) · 9 Sync to PCO (`UpdateConnectionRequest`, gated by `_9embers_CommonSync_IdentifierMap` SQL) · 10 Notify Connector (SystemCommunication 73 `62EC9A18-…`) · 11 Not a good fit off-ramp (WF 299) |
| Key gating | Actions 2 + 3 use the `ProcessLogicWithRequirement.lava` include against GroupRequirementType 1 (BG Required). Action 4 fires only when `BackgroundCheckResult == 'Fail'`. Other actions skip on `StopProcessing == 'Yes'`. Action 11 inverts: only Ready while any prior action is in progress. |
| Stop-Processing sources | Action 3 (auto on BG Fail), Action 4 (Fail Notification workflow), Action 11 off-ramp. |
| Ministry Guid (passed into WF 449) | `d6d026ca-f500-418d-abb3-7e0bbd46a43d` |
| Docs | `04-Kids-Volunteer-Onboarding-Overview.docx`, `-Triggers.docx` |

### Pipeline 5 — Security Volunteer Onboarding

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 5 / `B8B41605-F7BE-4865-8E41-5F7F93CD02CE` |
| Connection Opportunity | 100 Security (`C72B9A76-49F9-428F-AC04-71A49977DEAB`), CO has `OnboardingProcess = <pipeline 5 guid>` |
| Connection Type | 14 Volunteer Onboarding |
| Entry | Auto via WF 325. Coordinator manually creates CR in the Security opportunity. CO 102 "Security E-sign (Testing)" is for the Pipeline 6 beta, NOT entry for Pipeline 5. |
| Action Map (11 actions) | 0 Background Check Coming Email (SystemCommunication 80 `12DC2E1B-…`) · 1 Membership Check (WF 475 `554FB694-…`) · 2 Initiate BG Check (WF 449, Ministry Guid `1c588dca-2973-449f-a2f0-e76c9d69b8b6`) · 3 BG Completed (UpdateConnectionRequest — sets `StopProcessing` on Fail, also sets CR State=Active) · 4 BG Fail Notification (WF 322) · 5 After BC Pass — Send Next Steps Email (SystemCommunication 81 `7F458520-…`) · 6 Interview (WF 329 VoxSecurity New Interview, `9EB27576-…`) · 7 E-Forms (WF 320 Security volunteer e-Forms, `1B50DF12-…`, **single signature** — no separate P&P like Kids has) · 8 Placement Group (WF 303) · 9 Sync to PCO (UpdateConnectionRequest, gated by PCO SQL) · 10 Send Completion Communications (WF 321 `2EB5229E-…`, 3 emails: volunteer/team/coordinator) · 11 Not a good fit off-ramp (WF 299) |
| Key gating | Same Requirement pattern as Kids on actions 0/2/3. Action 1 (Membership) is `Ready if MembershipDate is empty, Skipped if populated`. Action 2 also requires `MembershipDate` populated — i.e., membership precedes BG check. |
| Stuck-pipeline auto-fix | ServiceJob 257 force-marks Action 3 Completed when wrapper leaves it stuck despite a recent valid pass. |
| Docs | `05-Security-Volunteer-Onboarding-Overview.docx`, `-Triggers.docx` |

### Pipeline 7 — Hospitality Volunteer Onboarding

| Attribute | Value |
|---|---|
| BemaPipelineTypeId | 7 |
| Entry | Mixed. Path 1: connector creates CR manually (most common). Path 2: "Join a Team: Hospitality Only" web form → creates a First Serve request, connector promotes to Hospitality CR after First Serve. No auto-launch from a public form into Hospitality directly. |
| Action Map (7 actions) | 1 First Touchpoint (WF "VoxHospitality - FirstTouchPoint") · 2 Add to First Serve Group (WF 303) · 3 Volunteer Logistics Email + E-Form (WF "Vox Hospitality volunteer e-Forms") · 4 Notify Connector after PCO sync (Lava `{% sql %}` against PCO IdentifierMap, then SystemCommunication) · 5 Add to Campus Hospitality Team (WF "Add to Hospitality Serving Group") · 6 Mark as Connected (UpdateConnectionRequest) · 7 Not a good fit off-ramp (WF 299) |
| Key gating | All non-off-ramp actions skip on `StopProcessing == 'Yes'`. Action 4 holds on PCO sync. Off-ramp (action 7) is inverted: Ready while any prior is incomplete; Skipped when all prior done. No BG check (Hospitality doesn't require one in current config). |
| Stop-Processing sources | Off-ramp only. |
| Doc | `07-Hospitality-Volunteer-Onboarding.docx` |

### Pipeline 10 — Vox Residency

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 10 / `D3095C20-6903-482A-979E-6F6BBE5E9A01` |
| Connection Opportunity | 103 Residency Application (`AF122DF8-D167-4957-91DC-CD80FE977E61`) |
| Connection Type | 15 Internal Applications (`24A3507E-F20A-4759-9DEB-E750735A4A2B`) — NOT Volunteer Onboarding |
| Entry | **Direct** from public WorkflowType 360 Residency Application (`2FA0A6B4-…`). The submit-form activity creates the CR, generates the application PDF, sets attrs, then calls `com.bemaservices.BemaPipeline.Workflow.Action.LaunchPipeline` (reads workflow's PipelineType default = pipeline 10 Guid). NO `OnboardingProcess` on CO 103. |
| Action Map (5 actions) | 0 Application Review (WF 365 `6A8C452F-…`) · 1 Reference Requests (WF 363 `91C25A41-…`, uses `{% workflowactivate workflowtype:'364' %}` to fan out 3 reference forms) · 2 Application Part 2 (WF 369 `D6FF46DB-…`) · 3 Video Interview (WF 372 `3D9B983A-…`) · 4 Candidate Tests (WF 371 `809A99D5-…`, completion sets CR Status to Completed/49) |
| Key gating | Actions 0 and 1 run in **parallel** (no wait-on-previous loop) as soon as pipeline activates. Actions 2/3/4 use the standard wait-on-previous + StopProcessing kill switch. Every decline branch in any of the 5 target workflows ends with: SetConnectionRequestStatus=50 (Declined `BB90FAD7-…`) + SetEntityAttribute `StopProcessing=Yes` + SendEmail decline. |
| Stop-Processing sources | Decline branches in WF 365, 363, 369, 372, 371. |
| Reviewer | All admin user-entry forms assigned to SecurityRole Group 196561 (`105ADD8D-…` VOX - Residency Application Administration). |
| Notes | No background check. No PCO sync. No under-18 logic (candidates are adults). |
| Docs | `10-Vox-Residency-Overview.docx`, `-Triggers.docx` |

### Pipeline 11 — Prayer Team Onboarding

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 11 / `4CDAE3F3-637C-4F7E-85AC-C1E60033A768` |
| Connection Opportunity | 104 Prayer Team (`8238EC1D-…`) |
| Connection Type | 14 Volunteer Onboarding |
| Entry | **Direct ingress** WF 380 Create Prayer Team Connection Request (`0D974D1A-…`). Coordinator launches with Person/Campus1/ConnectionComment. WF 380 creates CR with Status 44 New, assigns Connector, calls `LaunchPipeline`. NO `OnboardingProcess` on CO 104. |
| Action Map (7 actions, ActionTypeIds 78–84) | 0 Coordinator Review (WF 381 `9B369BB7-…`, 3 branches: Approve/Follow Up/Cancel) · 1 Membership Check (WF 382 `A59F151E-…`, conditional skip if Person already has MembershipDate) · 2 Prayer Team Application (WF 383 `B1DD2507-…`, has Opt Out path) · 3 Pastor Review (WF 384 `844C7F97-…`, Campus Pastor by Lava lookup) · 4 Send Welcome Email (UpdateConnectionRequest **no-op checkpoint** — historically named, actual approval email sent in WF 384) · 5 Add to Prayer Team (WF 303, picks one of 14 campus-specific BPT-Prayer/NHV-Prayer/etc. groups configured on CO 104) · 6 Sync to Planning Center (UpdateConnectionRequest, sets State=Connected — **NOT PCO-gated**; flips immediately, propagation handled by external 9embers job) |
| Key gating | Action 79 (Membership Check) uses a custom ProcessLogic that does `Skipped` when MembershipDate populated and `ReadyToProcess` when empty. All other actions use canonical wait-on-previous + StopProcessing pattern. |
| Stop-Processing sources | WF 381 Cancel Request (action 4786), WF 383 Opt Out (action 4778), WF 384 Decline (action 4802). |
| Follow Up | WF 381's Follow Up branch sets `FutureFollowUp` state but does NOT set StopProcessing — pipeline pauses on Step 1, review reactivates. |
| Docs | `11-Prayer-Team-Onboarding-Overview.docx`, `-Triggers.docx` |

### Pipeline 12 — Community Group Leader Onboarding (CGL)

| Attribute | Value |
|---|---|
| BemaPipelineTypeId | 12 |
| Connection Opportunity | 105 Community Group Leader |
| Connection Type | 14 Volunteer Onboarding |
| Entry | Auto via WF 325 (CO has `OnboardingProcess`). **Plus** scheduled job "Community Group Leader Onboarding - Create Connection" runs daily at 8 AM: for every Person whose `CommunityGroupLeadershipTraining` date is within last 14 days AND who has no open CGL CR, runs WF "Add CGL Connection" which creates the CR. Then WF 325 auto-launches the pipeline. |
| Action Map (post-6188-additions; 9 active actions, action IDs 85–93) | 0 Sign Ministry Leader Expectations (WF "1. Ministry Leader Expectations") · 1 CGL Expectations Conversation (WF "2. CGL Expectations Conversation", flagged by `PastoralConversationFlag(MLE)` set in step 0's opt-out) · 2 Membership Check · 3 Initiate BG Check (WF 449, age<18 exemption via Requirement) · 4 BG Completed (action 87 — sets state Active or skips on Fail) · 5 BG Fail Notification (added by `AddCglFailNotificationStep.sql` to mirror Kids/Security, shifts 5→9 by +1) · 6 BG Conversation (WF 431 "4. Background Check Conversation" — gated on `BackgroundCheckResult=='Fail'` per `FixBgcConversationProcessLogic.sql`; sets `StopProcessing=Yes` on completion) · 7 Submit Group Details (WF "5. Submit Group Details", assigned to applicant) · 8 Review Details & Interview (WF 435 "6. Review Details & Interview" — the most-touched form; has Approve / Needs Follow Up / Not Approved / Reload Form / Skip Group Creation buttons; ProcessLogic explicitly excludes action 88 from the wait loop) · 9 Create Group (WF "7. Create Group" — creates Group entity, sets leader, creates schedule + location, sends confirmation, sets CR Connected) |
| Key gating | All late actions honor `StopProcessing == 'Yes'`. Step 8 (Review Details & Interview) and Step 9 (Create Group) both explicitly `continue` past action 88 in their PreviousActions wait loop — staff can run the interview without waiting for the leader to fill out the separate Group Details form first. |
| Stop-Processing sources | Step 0 opt-out, Step 1 conversation outcome, Step 5 Fail Notification, Step 6 BG Conversation outcome, Step 8 review "Not Approved" path, Step 9 completion. |
| Pastoral Conversation flags | `PastoralConversationFlag(MLE)` triggers Step 1; `PastoralConversationFlag(BGC)` historically set by BG workflow (currently superseded by the `BackgroundCheckResult == 'Fail'` gate on Step 6 per the 6188 fix). |
| Doc | `12-Community-Group-Leader-Onboarding.docx` |

### Pipeline 13 — Media Volunteer Onboarding

| Attribute | Value |
|---|---|
| BemaPipelineTypeId / Guid | 13 / `D636FAE7-93A8-4E6F-8A36-1C2C7EF447E2` |
| Connection Opportunity | 106 Media Team (`EACBE3F7-…`) |
| Connection Type | 14 Volunteer Onboarding |
| Entry | **Direct ingress** WF 470 Create Media Team Connection Request (`86067F3E-…`). Coordinator launches with Person/Campus. WF 470 creates CR with Status 44 New, assigns Media Coordinator as Connector, calls `LaunchPipeline`. NO `OnboardingProcess` on CO 106. |
| Action Map (4 actions, ActionTypeIds 94–97) | 0 Media Team Application (WF 464 `54C9827B-…`, has Opt Out + Alternate Opt Out paths; Opt Out sets `StopProcessing=Yes`) · 1 Application Review (WF 468 `8C413F7D-…`, Decision dropdown: 0=Yes/1=Follow Up/2=No; **none of these set StopProcessing** — see note) · 2 Add to Media Team (WF 303, picks campus-specific Media group from 12 configured on CO 106) · 3 Sync to PCO (UpdateConnectionRequest, **gated** by inline `{% sql %}` against `_9embers_CommonSync_IdentifierMap`) |
| Key gating | All actions use canonical wait-on-previous + StopProcessing pattern. Action 97 layers SQL check for PCO sync on top. |
| Stop-Processing sources | **Only WF 464 Opt Out** (action 5638). WF 468's Cancel/Follow Up paths change CR state but do NOT set StopProcessing — meaning a "No" decision on step 1 leaves later actions still attempting to fire. Action 96 may try to add to a placement group; Action 97 will hold indefinitely waiting for PCO sync that won't happen. **This is a known wart** — worth flagging if work in this area surfaces it. |
| Doc | `13-Media-Volunteer-Onboarding-Overview.docx`, `-Triggers.docx` |

### Pipeline launch mechanisms — summary

Three distinct entry patterns are in use across these 8 pipelines. Knowing which one applies to a given pipeline tells you where to look when something goes wrong on entry:

| Pattern | Pipelines | How |
|---|---|---|
| **Auto via WF 325** | 4 Kids, 5 Security, 12 CGL (also covers Hospitality) | ConnectionType 14 has a Request-Started trigger → WF 325 reads CO's `OnboardingProcess` attr → launches pointed pipeline. Any CR creation in those opps triggers it. |
| **Direct from ingress workflow** | 11 Prayer (WF 380), 13 Media (WF 470) | Coordinator-launched workflow that takes Person/Campus args, creates CR, then calls `LaunchPipeline` (com.bemaservices.BemaPipeline.Workflow.Action.LaunchPipeline) directly. CO has no `OnboardingProcess`. |
| **Direct from public form** | 1 P&E (WF "P&E Interest Form"), 10 Residency (WF 360) | Public submission workflow creates CR + sets attrs + calls `LaunchPipeline`. |

If a CR was created but no pipeline started, check (a) is this CO supposed to use WF 325 (does it have `OnboardingProcess`)? (b) if direct ingress, did the launching workflow actually reach its `LaunchPipeline` action? (c) is the pipeline `IsActive`?

## Source-of-truth files in the build

When the rules in this skill aren't enough, read these (in `build/rock-source-<instance>-<tag>/com.bemaservices.BemaPipeline/`):
- `BemaPipelineActionTypes/BemaPipelineActionTypeComponent.cs` — abstract base; `ProcessState` (line 238) is the central dispatcher and `GetMergeFields` (line 415) is the merge-field setup.
- `BemaPipelineActionTypes/BemaPipelineActionLaunchWorkflow.cs` — overrides for `ProcessAction`, `Skipped`, `Completed`, `ActionLinks` (the launch buttons that the pipeline UI shows).
- `Migrations/003_BaseComponentAttributes.cs` — where the base attributes (ProcessLogic / DisplayLogic / etc.) get registered.
- `Migrations/002_CreateLaunchWorkflowActionType.cs` and `005_ActionTypeChanges.cs` — where each component class registers its own attribute copies (the per-class Attribute Ids in the map above are seeded here).
- `Model/BemaPipelineActionType.cs` — the model class.
- `Jobs/ProcessBemaPipelines.cs` — the background job that walks pipelines and calls `ProcessState` per action.
