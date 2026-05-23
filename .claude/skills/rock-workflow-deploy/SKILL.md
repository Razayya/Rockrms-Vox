---
name: rock-workflow-deploy
description: Use this skill to build, modify, or deploy Rock RMS workflow types, actions, and ServiceJobs via SQL against the Vox prod DB. Covers the Rock schema for Workflow*, Attribute, AttributeValue, and ServiceJob tables; the org.secc.Jobs.WorkflowLauncher pattern for fan-out; using Run SQL actions with Lava substitution; and the gotchas around encoding, escaping, indexed views, and CTE performance.
---

# Rock workflow + job deployment via SQL

When the user wants to create, modify, or wire up a Rock workflow type, workflow action, or scheduled job by writing SQL against the prod DB rather than clicking through the UI.

## When to use this skill

- "Build a workflow that…"
- "Create a job that fires WF X for each row…"
- "Add a Run SQL action to WF…"
- "I want a daily/weekly/monthly job that…"
- Any inserts/updates touching `WorkflowType`, `WorkflowActivityType`, `WorkflowActionType`, `Attribute`, `AttributeValue` (qualifying on a workflow), or `ServiceJob`.

## Session startup: read the Rock version pin

Rockrms-Vox is a thin-repo — upstream Rock code lives at `build/rock-source-<instance>-<tag>/`. Read the pin for the target instance from `overlay/instance-versions.json`:

```powershell
$pin = (Get-Content overlay\instance-versions.json | ConvertFrom-Json).instances.prod
$cloneDir = Join-Path (git rev-parse --show-toplevel) $pin.clone_dir
```

That `clone_dir` is where to grep workflow-action source classes, Lava command implementations, ServiceJob base classes, and `org.secc.Jobs.WorkflowLauncher` (whose pattern you'll mirror). If the directory doesn't exist, run `.\scripts\prepare-build.ps1 -Instance <env>` once. The pin is authoritative — trust it (see `memory/feedback_user_input_is_king_for_versions.md`).

If the instance isn't pinned, route to the `rock-update` skill. Don't fabricate workflow/action signatures from training-data memory — confirm against the source for the pinned version (see `memory/feedback_read_rock_source_when_planning.md`).

## High-level pattern

1. **Inspect first.** Look up an existing workflow that does something similar, mirror its row shape (especially `IsPersisted`, `LoggingLevel`, `WorkTerm`).
2. **Generate, don't hand-write.** SQL bodies for action attributes (e.g. Run SQL's `SQLQuery`, Send Email's `Body`) get embedded in `INSERT … VALUES (N'…')`, which means single quotes need escaping. Use a `sql/generate_*.sh` script with `sed "s/'/''/g"` + heredoc + `awk` substitution. Source files use plain single apostrophes; the generator escapes once.
3. **Toggle commit.** Every migration starts with `DECLARE @Commit BIT = 0;` wrapping `BEGIN TRAN` and an `IF @Commit = 1 COMMIT ELSE ROLLBACK` at the bottom. Test with 0, persist with 1.
4. **Verify in-transaction.** `SELECT` the new rows just before the COMMIT decision. The user reads the verification output before approving the flip to 1.
5. **Jobs `IsActive = 0` at creation.** Always. The user enables them in the UI after review.

## The fan-out job pattern (org.secc.Jobs.WorkflowLauncher)

This SECC plugin job class runs a SQL query and **launches the configured Workflow Type once per row**, auto-mapping result columns to workflow attributes by key name. Reference job: `ServiceJob.Id = 261` ("Community Group Signee Communication").

Configured via three job attributes (qualified on `Class = 'org.secc.Jobs.WorkflowLauncher'`):
| Attribute Key | Field Type | Purpose |
|---|---|---|
| `Workflow` | Workflow Type | Guid of the WF to launch per row |
| `SQLQuery` | Code Editor | The SQL; columns auto-bind to workflow attributes |
| `CommandTimeout` | Integer | Seconds (default 3600) |

**Convention:** keep the job's SQL as a *selection* query (who to launch for); push per-row data fetching into the workflow itself via a Run SQL action. Reason: Rock attribute values have a length cap, and big JSON payloads from SQL-side aggregation can exceed it. Pattern proven on WT 492 (sql/19/21 → simplified to sql/25 + the per-leader fetch in sql/24a).

## Accessing entity-typed workflow attributes from Lava

For any workflow attribute whose FieldType is an entity-typed field (`Person`, `Group`, `GroupMember`, `ConnectionRequest`, etc.), **`{{ Workflow | Attribute:'Key','Object' }}` returns the underlying entity object directly** — letting you walk its properties and sub-attributes without writing an entity command (`{% person %}`, `{% groupmember %}`) and without enabling `RockEntity` in the Lava commands list.

```liquid
{% assign gm = Workflow | Attribute:'GroupMember','Object' %}
{% if gm.IsArchived or gm.GroupMemberStatus == 'Inactive' %}…{% endif %}
{{ gm.Person.NickName }} {{ gm.Person.LastName }}
{{ gm.Group.Name }}
{{ gm.Person | Attribute:'CellPhone' }}
```

The accessor variants (case-sensitive, second argument to `Attribute:`):
- `'Object'` — the full entity, with properties + nested entities + Attribute filter chainable on it.
- `'RawValue'` — the bare scalar (Guid string, Id, etc.). Use when you need just the identifier to pass into SQL or another action's config.
- *(no second arg)* — the formatted display string. Useful in emails / forms, useless for logic.

This is THE pattern for branching, sub-property reads, and cross-entity walks inside workflow Lava. Reach for entity commands (`{% groupmember where:'Guid == …' %}`) only when you don't have the workflow attribute as a starting point.

## Run SQL action with Lava substitution

The `Rock.Workflow.Action.RunSQL` action (EntityTypeId=177) runs SQL with **Lava preprocessed first**. Configure via attributes (qualified on `EntityTypeId = 177`):
| Attribute Id | Key | Purpose |
|---|---|---|
| 1224 | `SQLQuery` | The SQL body (with Lava `{{ … }}` tokens) |
| 1226 | `ResultAttribute` | Workflow attribute Guid that receives the first column of the first row |
| 2160 | `ContinueOnError` | Boolean |
| 1225 | `Active` | Boolean |

**Getting the PersonAlias guid out of a Person workflow attribute** — use Object + Property, not RawValue:
```liquid
{% assign leader = Workflow | Attribute:'Leader','Object' %}
DECLARE @LeaderGuid UNIQUEIDENTIFIER = TRY_CAST('{{ leader.PrimaryAlias.Guid }}' AS UNIQUEIDENTIFIER);
```
RawValue *should* return the bare guid in a fresh setup, but if the apostrophes inside the Lava expression get corrupted (see escaping gotcha below), Lava silently falls back to the default HTML rendering, which produces SQL with `&` and stray words like "of" → `Incorrect syntax near 'of'` / `Incorrect syntax near '&'` errors.

## Gotchas (priority order)

### 1. sqlcmd UTF-8 codepage
Always pass `-f 65001` to sqlcmd when the SQL file has any non-ASCII chars. Without it, em-dashes and curly quotes get mojibaked to `â€"` etc. (See `memory/feedback_sqlcmd_utf8.md`.) Stick to ASCII (`-` not `–`) when you can.

### 2. SET options for Attribute table
Inserting into `Attribute` (or anything Rock indexed) requires:
```sql
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
```
at the top of every migration. Without these you'll get error 1934.

### 3. Don't pre-escape apostrophes in source files
Source files fed through `sed "s/'/''/g"` must use **plain single apostrophes**, never `''`. If you write `Attribute:''Leader''` in the source, sed produces `Attribute:''''Leader''''`, the SQL parse stores `Attribute:''Leader''`, and Lava can't parse the doubled quotes — silent fallback breaks the action. (See `memory/feedback_dont_pre_escape_source.md`.)

### 4. CTE re-evaluation kills perf
SQL Server **re-evaluates CTEs every time they're referenced**. If you have:
```sql
;WITH lots_of_joins AS (…)
SELECT … FROM (SELECT DISTINCT col FROM lots_of_joins) g
LEFT JOIN (SELECT … FROM lots_of_joins WHERE …)  -- DOUBLE EVALUATION
```
you'll get multi-minute queries that look like they hung. Materialize into `#tempTable` once, then query the temp table.

### 5. Avoid full-table scans of large tables
`Attendance` (millions of rows in Vox) gets scanned if your subquery doesn't filter Person+Group precisely. Either skip it or restrict via `IN (SELECT … FROM small_set)`.

### 6. Action ordering
Workflow actions run in `[Order]` ascending order within an activity. When inserting a new action between existing ones, `UPDATE` the others' `Order` first. New actions default to `IsActionCompletedOnSuccess=1`, `IsActivityCompletedOnSuccess=0` (set to 1 if it's the last action in the activity).

### 7. Job IsActive=0 by default; HistoryCount=500, EnableHistory=1
Every new ServiceJob is created with `IsActive=0` so the user can smoke-test before letting the cron take over.

Also set `HistoryCount = 500` and `EnableHistory = 1` on every new ServiceJob. **`HistoryCount = 0` is the kill switch for `ServiceJobHistory` writes** — when zero, Rock writes nothing to history regardless of `EnableHistory`, leaving you with no audit trail of when the job ran or whether it succeeded. Confirmed against Vox prod: Job 280 (created with `0,0`) had empty `LastRunDateTime` and zero history rows after a successful run; Job 270 (created with `0,500`) wrote 17 history rows over its first three days. Default to `1, 500` on new jobs to avoid the trap. Older template scripts (`CreateUnstickJob.sql`, `CreateInactiveCrCleanupJob.sql`) still carry `0,0` — if you copy from them, fix the values; don't propagate the gap.

### 8. Action names lie — verify the target attribute
Workflow action `Name` is free-text and drifts from what the action actually does. An action called "Set Notification Person" may write into the `VoxKidsDirector` attribute; a Send Email named after one recipient may target a different workflow attribute Guid in its `To` field. Always resolve the action's configured attribute Guid (look up `Attribute.Guid` for the value stored in `AttributeValue.Value` against keys like `Attribute`, `To`, `CC`, `ResultAttribute`) before assuming the name reflects behavior. Corollary: an attribute that looks unused (e.g. `NotificationPerson` with nothing writing to it) may genuinely be vestigial — check every action's config, not just names.

### 9. Conditional workflow actions use the `Criteria*` columns, not the `Active`-AV-Guid hack
When an action should fire only when a workflow attribute holds a particular value (e.g. a Boolean flag from a preceding Lava action), wire it through the `WorkflowActionType.Criteria*` columns, not by stuffing the workflow-attribute Guid into the `Active` config AttributeValue.

```sql
UPDATE WorkflowActionType
SET CriteriaAttributeGuid           = '<workflow-attribute-guid>',
    CriteriaValue                   = N'true',     -- or 'false', or any string to compare
    CriteriaComparisonType          = 1,           -- see ComparisonType values below
    IsActionCompletedIfCriteriaUnmet = 0           -- 0 = skip when criteria fails (downstream still runs); 1 = "complete" the action without running
WHERE Id = <actionTypeId>;
```

`CriteriaComparisonType` is the `Rock.Model.ComparisonType` flags enum (verified against 17.5.2 source): `1` EqualTo · `2` NotEqualTo · `4` StartsWith · `8` Contains · `16` DoesNotContain · `32` IsBlank · `64` IsNotBlank · `128` GreaterThan · `256` GreaterThanOrEqualTo · `512` LessThan · `1024` LessThanOrEqualTo · `2048` EndsWith · `4096` Between · `8192` RegularExpression. **To gate on "attribute is empty / not empty", use `IsBlank` (32) / `IsNotBlank` (64)** — they take no `CriteriaValue`; don't hack it with `EqualTo ''`.

At runtime Rock evaluates `Criteria*` first — if the criteria fails, the action skips entirely, regardless of what `Active` says. The `Active` config-AV approach (setting `Value` to a workflow-attribute Guid so it resolves to True/False at runtime) does also work as a gate, but:
- Two gates fighting over the same intent invite drift when one is touched and the other isn't.
- The UI's "Filter Criteria" section reads from `Criteria*`, so a user inspecting the action in the editor won't see your gate if it's hidden in the `Active` AV.
- `IsActionCompletedIfCriteriaUnmet` controls downstream activity-completion behavior — there's no `Active`-AV equivalent.

For new migrations, include `CriteriaAttributeGuid` + `CriteriaValue` in the `WorkflowActionType` INSERT, and leave the action's `Active` config AV as the literal `True`.

### 10. `AttributeMatrixItem.AttributeMatrixTemplateId` is required by EF, despite the nullable column
When wiring up a BEMA `SetEntityProperty` action (or anything else that takes an AttributeMatrix), you have to INSERT each `AttributeMatrixItem` row with `AttributeMatrixTemplateId` populated. The column is nullable in the schema, but the BEMA plugin's EF model marks it `[Required]`, so a row with `AttributeMatrixTemplateId = NULL` fails at runtime with:

> The 'AttributeMatrixTemplateId' property on 'AttributeMatrixItem' could not be set to a 'null' value. You must set this property to a non-null value of type 'System.Int32'.

Set it to match the parent `AttributeMatrix.AttributeMatrixTemplateId`:

```sql
INSERT INTO AttributeMatrixItem
    (AttributeMatrixId, AttributeMatrixTemplateId, [Order], [Guid], CreatedDateTime, ModifiedDateTime)
VALUES
    (@MatrixId, @MatrixTemplateId, 0, NEWID(), SYSDATETIME(), SYSDATETIME());
```

Sibling gotcha — when there are only 1-2 properties to set, **prefer Rock core `Rock.Workflow.Action.SetEntityProperty` (one action per property) over BEMA's variant** (one action with N-row Matrix). Core takes `EntityType` + `EntityIdGuid` + `PropertyName` + `PropertyValue` + `EmptyValueHandling` as direct config AVs — no Matrix indirection, no template-id gotcha, each property is a visible action in the workflow editor. The Matrix shape only pays off past ~3 properties.

### 10. Rock Lava does NOT allow parenthetical grouping in `{% if %}`
Liquid (and therefore Rock Lava) does not support `( … )` to group boolean clauses inside an `{% if %}` tag. Writing `{% if (A and B) or C %}` is a parse error, and worse — Rock often silently falls back to the default rendering rather than raising an error, so the action looks "fine" but the predicate didn't evaluate the way you expected.

Operators are evaluated right-to-left, so `{% if A and B or C %}` actually parses as `A and (B or C)`. That is almost never what you want when mixing `and`/`or`.

**Pattern when you need grouping: use `{% elseif %}`** — Rock Lava supports `{% elseif EXPR %}` (note: `elseif`, not standard Liquid's `elsif`), which collapses the `{% else %}{% if %}{% endif %}{% endif %}` nesting into a flat chain. This is the preferred form:

```liquid
{% if A %}true
{% elseif B and C %}true
{% elseif D and E %}true
{% else %}false
{% endif %}
```

Each `elseif` branch is one OR clause; `and` chains freely within a branch because there's no precedence ambiguity once you're past the OR boundary. One `{% endif %}` closes the whole chain (vs N `{% endif %}` for explicit nesting).

The deeply-nested `{% else %}{% if %}{% endif %}{% endif %}` form is functionally identical and works fine if you prefer the explicit shape, but `elseif` is what's intended.

A second option — assign-flag pattern — works when many independent flags feed a single decision, but produces noisier output if you don't trim:

```liquid
{% assign result = false %}
{% if A %}{% assign result = true %}{% endif %}
{% if B and C %}{% assign result = true %}{% endif %}
{{ result }}
```

Prefer `elseif` chains for Lava that drives a workflow attribute (cleaner single-token output); use the assign-flag pattern only when the conditions are genuinely independent rather than alternative branches.

### 11. Copy WorkflowType shares the AttributeMatrix with the source — fix it

Rock's "Copy WorkflowType" UI translates workflow-attribute Guid refs in action config (the `EntityIdGuid` config AV that points at the workflow's `ConnectionRequest` attr, the `CriteriaAttributeGuid` on the action row, etc.) but **does NOT translate the `Matrix` config AV on `Set Entity Attributes` (BEMA matrix) actions**. After a copy, both the source action and the copy's clone reference the same `AttributeMatrix` row plus its items. Editing the matrix on the copy mutates production runtime behavior on the source.

Additionally, the source matrix's items have `Value` column = source workflow-attribute Guid. Even though the copy created its own workflow attrs with the same Keys, the items still point at the source's attrs — so when the copy runs against those items, the lookups resolve to the wrong workflow's attrs (or null).

Verified instance: WorkflowType 500 (copy of 435), action 6063 had:
```
Active        False
EntityType    36b0d0c7-…       (translated correctly)
EntityIdGuid  e9d59d95-…       (translated to type-500's ConnReq attr)
Matrix        ad0b6926-…       (UNCHANGED -- still points at prod matrix 29220)
```

**Fix recipe** — for each matrix-using action on the copy:

1. Confirm the bug premise: action's `Matrix` config AV equals the source matrix's Guid (if it doesn't, someone already fixed it — bail).
2. Confirm every source matrix item's source-wf-attr Key has a counterpart on the copy's workflow (by Key). Abort with a translation-gap report if any are missing.
3. Create a new `AttributeMatrix` row tied to the same `AttributeMatrixTemplateId`.
4. Clone each source `AttributeMatrixItem` into the new matrix — new Item Guid, same `[Order]`, same `AttributeMatrixTemplateId`. Track source→new id pairs (an `OUTPUT INTO` table-var, or a temp-table MERGE).
5. For each new item, copy the 2 `AttributeValue` config rows:
   - **AttributeKey** column (literal CR-attr key): copy verbatim.
   - **Value** column (wf-attr Guid): translate via `JOIN Attribute src_attr ON Guid = src_av.Value` → `JOIN Attribute tgt_attr ON tgt_attr.Key = src_attr.Key AND tgt_attr.<scoped to target workflow>`.
6. UPDATE the matrix-action's `Matrix` config AV (`Attribute.Key = 'Matrix'` qualified by the BEMA action's EntityType 635) to the new matrix's Guid.

Reference implementation: `claudefiles/rock/projects/6250/03-fix-copied-workflow-500-matrix.sql`. Uses `MERGE … OUTPUT INTO` to clone the items + capture the id-map in one statement, then two `INSERT…SELECT` for the AttributeValue copies (one verbatim, one Guid-translated). Pre-flight gates on premise + translation completeness; post-flight verifies zero unresolved Guids.

Affects: `com.bemaservices.WorkflowExtensions.Workflow.Action.SetEntityAttribute` (EntityTypeId 635). Core `Rock.Workflow.Action.SetEntityAttribute` (557) has no matrix ref and is not affected. See `memory/reference_rock_workflow_copy_matrix_bug.md`.

### 12. Audit a workflow attribute before building on it — and flag dead ones

An attribute existing on a WorkflowType says nothing about whether it's *populated* or *read*. Two questions — run both before you compute from / map / gate on an attribute, and run them proactively when reviewing or tightening an existing workflow.

**Is it populated?** Workflow 414 carries both `TerminationDate` (Date) and `TerminationDateandTime` (Date Time). Building on `TerminationDate` looked right by name — but a completed-instance audit showed it populated in **0 of 19**; `TerminationDateandTime` was the live one (19/19). The dead attribute would have produced silent blanks.

```sql
SELECT COUNT(*) AS Instances,
       SUM(CASE WHEN av.Value > '' THEN 1 ELSE 0 END) AS Populated
FROM Workflow w
LEFT JOIN AttributeValue av ON av.EntityId = w.Id AND av.AttributeId = <attrId>
WHERE w.WorkflowTypeId = <wtId> AND w.CompletedDateTime IS NOT NULL;
```

**Is anything reading it?** A workflow attribute is consumed in four places — scan all four, by Key *and* by Guid:
- **Form fields** — `WorkflowActionFormAttribute WHERE AttributeId = <id>`
- **Action config** — its Guid stored in another action's config AV (`Attribute` / `To` / `CC` / `ResultAttribute`, etc.): `AttributeValue.Value LIKE '%<guid>%'`
- **Lava** — `Attribute:'<Key>'` inside email bodies, form Pre/PostHtml, RunSQL / RunLava bodies
- **Launch mappings** — the `<Key>` inside a `WorkflowAttributeKey` config AV (this workflow as target, or as source elsewhere)

**Recommend, don't just avoid.** An attribute that is neither populated nor referenced anywhere is dead weight — surface it: *"`X` isn't set or read anywhere — drop it?"* That's a cheap, high-value cleanup lever when tightening previously-built workflows. Mirror the four-source cross-reference discipline from `rock-project-review`'s cleanup scan.

Extends gotcha #8 — names lie about behavior; instance data and reference scans are the only proof of what an attribute actually does.

### 13. A WorkflowActionForm is invisible until an action references it — build the form and its action together

A `WorkflowActionForm` row is only a form *definition*. It does not appear in the workflow editor and never renders until a `WorkflowActionType` (a User Entry Form action) points at it via `WorkflowFormId`. A committed-but-unreferenced form is a dead orphan — split the form and its action across separate steps and you get a confusing intermediate state (clear cache, see nothing, assume the work failed).

Build them in **one migration**: the `WorkflowActionForm` + its `WorkflowActionFormAttribute` field rows + the `WorkflowActionType` that carries `WorkflowFormId`. If a migration sequence must stage them separately, say so explicitly up front so the orphan window is expected, not alarming.

## Schema cheat sheet (Vox DB-specific IDs as of 2026-04-29; verify with a quick SELECT before relying on them)

**EntityType IDs:**
- 29 = Rock.Model.ServiceJob
- 113 = Rock.Model.Workflow
- 115 = Rock.Model.WorkflowActionType
- 157 = Rock.Workflow.Action.SendEmail
- 177 = Rock.Workflow.Action.RunSQL

**FieldType IDs:**
- 1 = Text
- 3 = Boolean
- 7 = Integer
- 18 = Person
- 21 = Memo
- 36 = Workflow Type
- 51 = Code Editor
- 65 = Workflow Text Or Attribute

**Required NOT NULL columns (often forgotten):**
- `WorkflowType`: IsSystem, Name, Order, WorkTerm, IsPersisted, LoggingLevel, Guid, IsFormBuilder, IsLoginRequired
- `WorkflowActivityType`: WorkflowTypeId, Name, IsActivatedWithWorkflow, Order, Guid
- `WorkflowActionType`: ActivityTypeId, Name, Order, EntityTypeId, IsActionCompletedOnSuccess, IsActivityCompletedOnSuccess, Guid, CriteriaComparisonType, IsActionCompletedIfCriteriaUnmet
- `Attribute`: IsSystem, FieldTypeId, Key, Name, Order, IsGridColumn, IsMultiValue, IsRequired, Guid, AllowSearch, IsIndexEnabled, IsAnalytic, IsAnalyticHistory, IsActive, EnableHistory, ShowOnBulk, IsPublic, IsDefaultPersistedValueDirty, IsSuppressHistoryLogging
- `ServiceJob`: IsSystem, Name, Class, CronExpression, NotificationStatus, Guid, EnableHistory, HistoryCount

**Workflow attribute qualifier:** `EntityTypeId=113`, `EntityTypeQualifierColumn='WorkflowTypeId'`, `EntityTypeQualifierValue='<wt id as string>'`.

**Workflow action attribute qualifier:** `EntityTypeId=115`, `EntityTypeQualifierColumn='EntityTypeId'`, `EntityTypeQualifierValue='<action component entity type id as string>'`.

**ServiceJob attribute qualifier:** `EntityTypeId=29`, `EntityTypeQualifierColumn='Class'`, `EntityTypeQualifierValue='<job class string>'`.

## Generator script template

```bash
#!/usr/bin/env bash
# sql/generate_<name>.sh — emits sql/<n>_apply.sql with embedded SQL bodies escaped.
set -euo pipefail
cd "$(dirname "$0")"

BODY="$(sed "s/'/''/g" <body_source>.sql)"

cat > <n>_apply.sql <<EOF
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Commit BIT = 0;
BEGIN TRAN;

-- … inserts/updates, with N'__BODY_PLACEHOLDER__' where the SQL body goes …

-- verification SELECTs

IF @Commit = 1 COMMIT TRAN; ELSE ROLLBACK TRAN;
EOF

awk -v b="$BODY" '{ gsub(/__BODY_PLACEHOLDER__/, b); print }' <n>_apply.sql > <n>_apply.sql.tmp
mv <n>_apply.sql.tmp <n>_apply.sql
```

Run with `sqlcmd … -f 65001 -I -b -i <n>_apply.sql`.

## Reference — what was built in this skill's defining session (Project 5456)

- **Categorization:** created sub-categories `Ministry Defender` (1013) and `Archive` (1014) under Safety & Security (162); moved 5+5 workflows.
- **WorkflowType 492** "BGC Refresh - Leader Email" with two actions:
  - `Fetch Leader Rows` (Run SQL) — fetches per-leader rows JSON via Lava (`Object → PrimaryAlias.Guid`)
  - `Send Email` — uses RowsJson workflow attribute
- **ServiceJob 268** quarterly cron `0 0 8 1 2,5,8,11 ? *`, launches WT 492
- **ServiceJob 269** daily cron `0 0 9 1/1 * ? *`, launches WT 489 (Background Check Volunteer Refresh)
- Both jobs created with `IsActive = 0`.
