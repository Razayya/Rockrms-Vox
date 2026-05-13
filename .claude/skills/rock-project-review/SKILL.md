---
name: rock-project-review
description: Use this skill to assess, validate, and execute work on Rock Request projects (BlueBoxMoon Project Management). Covers fetching the project + comments + attachments, mapping comments to the underlying Rock workflow/pipeline/page changes, validating that recent edits address stakeholder feedback, running scoped DB cleanup or migration work tied to a project, and drafting closure comments. Pairs with rock-workflow-deploy for the deploy half of any workflow-related work.
---

# Rock Request project review + execution

When the user references a "project" by Id in the Vox Rock Request tracker (BlueBoxMoon Project Management plugin), or asks you to "review", "validate", "assess", or "do the work for" a project. The user names projects by Id (e.g. "let's look at 6435") — that Id maps to `_com_blueboxmoon_ProjectManagement_Project.Id`.

## When to use this skill

- "Review project NNNN" / "what are we going to do about NNNN"
- "Can you validate the changes I made to address NNNN's most recent comment"
- "Let's start working on NNNN"
- Any reference to a Rock Request, BBM PM ticket, or project Id in the Vox prod context.
- Triggers alongside `rock-workflow-deploy` when the project's resolution is a workflow change.

## High-level pattern

1. **Load full context up front.** Pull the project record, every comment, **download every attached file** (audit docs, screenshots, specs), **enumerate every sub-project and per-project task**, AND check `claudefiles/rock/projects/<projectId>/` for any `PENDING_*.md` notes left from prior sessions — read those first so you pick up where the last session left off. (See `memory/feedback_project_review_fetch_attachments.md`. Sub-project handling: gotcha #7.)
2. **Anchor scope to title + description, not attachments.** Audits and specs often raise more than the project asks for; surface that explicitly as out-of-scope rather than folding it in. (See `memory/feedback_project_scope_from_title_not_attachments.md`.)
3. **Map comments to system state.** For each substantive comment, find what part of Rock it concerns (workflow, pipeline, page, attribute, etc.) and inspect current values + ModifiedDateTime to see whether the work has been done.
4. **Step-by-step execution with check-ins.** When doing work, pause for confirmation between each planned step. (See `memory/feedback_project_work_check_in_each_step.md`.)
5. **Per-project SQL files.** Migrations and queries land in `claudefiles/rock/projects/<projectId>/<NN-description>.sql`. (See `memory/reference_project_id_sql_folders.md`.)
6. **Closure comment at the end.** When work is functionally complete, draft a status comment for the user to post — but never recommend interim comments when the latest comment already prompts the next action. (See `memory/feedback_recommend_comment_on_project_completion.md` and `memory/feedback_no_redundant_comment_recs.md`.)

## Schema map — finding a project, its notes, and attachments

**Project record:**
```sql
SELECT Id, Name, Description, RequestDate, DueDate, State, IsActive,
       ProjectTypeId, CategoryId, CompletedDateTime
FROM _com_blueboxmoon_ProjectManagement_Project
WHERE Id = <projectId>;
```

**Comments (and "system" notes like "completed project" / "re-opened project"):**

Comments are in core Rock's `Note` table, attached via `NoteType.EntityTypeId = 963` (= `com.blueboxmoon.ProjectManagement.Model.Project`) and `Note.EntityId = <projectId>`. **Always sort by `CreatedDateTime DESC` so the most recent comment is at the top — that's the one that drives the next action.**

```sql
SELECT TOP 20
       n.Id, n.CreatedDateTime,
       p.NickName + ' ' + p.LastName AS Author,
       n.Text
FROM Note n
INNER JOIN NoteType nt        ON n.NoteTypeId = nt.Id
LEFT  JOIN PersonAlias pa     ON n.CreatedByPersonAliasId = pa.Id
LEFT  JOIN Person p           ON pa.PersonId = p.Id
WHERE nt.EntityTypeId = 963
  AND n.EntityId      = <projectId>
ORDER BY n.CreatedDateTime DESC;
```

**Attachments:** comments and the project description embed file URLs as Markdown image/file links — `![name.png](https://voxchurch.org/GetFile.ashx?Id=NNNNN)` or bare `https://voxchurch.org/GetFile.ashx?Id=NNNNN`. Pass each URL to WebFetch up front; on auth failure, ask the user to share the contents directly.

**Sub-projects (`Project.ParentProjectId`):** the BBM PM plugin links projects in a parent/child tree. A "main project" like 6188 ("Fully Test/Validate Pipeline Logic") can have N child projects (5434 VoxKids onboarding, 5506 Ryan Kuziel reactivation, 6198 Rock Serve Card Connections, etc.). Each child is itself a full Project row with its own comment thread, attachments, sub-projects, and tasks. **You cannot answer "are all the notes/actions addressed?" on a parent without recursing into the children.** (Gotcha #7.)

```sql
-- Immediate sub-projects
SELECT Id, Name, State, IsActive, IsBlocked,
       RequestDate, DueDate, CompletedDateTime
FROM _com_blueboxmoon_ProjectManagement_Project
WHERE ParentProjectId = <projectId>
ORDER BY State, Id;

-- Full sub-project tree (any depth) — anchor at the project, recurse on ParentProjectId
WITH tree AS (
  SELECT Id, Name, ParentProjectId, State, IsActive, IsBlocked,
         CompletedDateTime, 0 AS Depth
  FROM _com_blueboxmoon_ProjectManagement_Project
  WHERE Id = <projectId>
  UNION ALL
  SELECT p.Id, p.Name, p.ParentProjectId, p.State, p.IsActive, p.IsBlocked,
         p.CompletedDateTime, t.Depth + 1
  FROM _com_blueboxmoon_ProjectManagement_Project p
  INNER JOIN tree t ON p.ParentProjectId = t.Id
)
SELECT Depth, Id, ParentProjectId, Name, State, IsActive, IsBlocked, CompletedDateTime
FROM tree
ORDER BY Depth, Id;
```

For each Active sub-project, also pull its latest comment to see who's prompted to act next — the parent isn't truly "done" while a child still has an open stakeholder ask.

**Tasks (per-project to-do list, `_com_blueboxmoon_ProjectManagement_Task`):** distinct from sub-projects. Tasks are lightweight checklist items *within* a project (Name, Description, AssignedToPersonAliasId, DueDate, `State` ∈ {`Active`, `Completed`, `Cancelled`}, IsBlocked, [Order], RecurringScheduleContent). Many projects have none; some (e.g. 6495 employee-offboarding) have a structured checklist.

```sql
SELECT Id, Name, [Order], State, IsActive, IsBlocked, DueDate,
       CompletedDateTime, CancelledDateTime, AssignedToPersonAliasId
FROM _com_blueboxmoon_ProjectManagement_Task
WHERE ProjectId = <projectId>
ORDER BY [Order], Id;
```

Task-to-task dependencies live in `_com_blueboxmoon_ProjectManagement_TaskBlocker (TaskId, BlockedTaskId)`. Project-to-project dependencies live in `_com_blueboxmoon_ProjectManagement_ProjectBlocker (ProjectId, BlockedProjectId)` — both empty for 6188 but worth checking on any project where a sub-project looks stalled.

## Mapping comments → Rock subsystem

The same project tracker covers many subsystems. Disambiguate via the comment URLs and language:

| Signal | Subsystem | Tables |
|---|---|---|
| `/page/NNNN?WorkflowTypeId=` or "workflow" | Workflow Type | `WorkflowType`, `WorkflowActivityType`, `WorkflowActionType`, `WorkflowActionForm*`, `Attribute`, `AttributeValue` |
| `/page/NNNN?PipelineTypeId=` or "connection pipeline" or "Bema pipeline" | BEMA Connection Pipeline | `_com_bemaservices_BemaPipeline_BemaPipelineType`, `_com_bemaservices_BemaPipeline_BemaPipelineActionType`, `_com_bemaservices_BemaPipeline_BemaPipeline*` |
| "Page", "site", "intake form" | Pages + blocks | `Page`, `Block`, `BlockType`, `Site`, `Layout` |
| "DB cleanup", "_tmp", "_restored", database size | Database admin | `sys.tables`, `sys.sql_modules`, `AttributeValue`, `HtmlContent`, `ServiceJob`, `WorkflowActionType` |

## Validating workflow changes against a comment

For workflow-related projects, this is the typical flow when validating edits:

1. **Identify the workflow type by name or Id.**
2. **Walk the activities and actions** in `[Order]` ascending: `WorkflowActivityType.WorkflowTypeId` → `WorkflowActionType.ActivityTypeId`, joined to `EntityType.FriendlyName` for action class.
3. **For user-entry forms:** `WorkflowActionType.WorkflowFormId` → `WorkflowActionForm`. Form fields with their PreHtml/PostHtml are in `WorkflowActionFormAttribute`. Post-submit response copy lives in `WorkflowActionForm.Actions`, pipe-delimited: `ButtonName^ButtonGuid^ActivateActivityGuid^ResponseMessage|`.
4. **For action configuration:** `AttributeValue` joined to `Attribute` where `EntityType.Name = 'Rock.Model.WorkflowActionType'` and `av.EntityId = <action id>`. Many config values like `To`, `CC`, `FromName`, `Attribute`, `ResultAttribute` are workflow-attribute Guids — resolve via `Attribute.Guid` to get the human-readable name.
5. **Use `av.ModifiedDateTime`** to confirm a change was made recently. Cross-reference with comment dates to verify the change addresses the comment.

## Cross-reference scan pattern (DB cleanup projects)

When a project asks to remove DB objects (temp tables, views, jobs, etc.), confirm zero references before dropping. Use **single-pass scans**, not correlated subqueries — a 48-table set with N-pass subqueries can take minutes; a single-pass scan takes seconds.

```sql
-- BAD: N correlated subqueries, each scanning AttributeValue
SELECT t.name, (SELECT COUNT(*) FROM AttributeValue WHERE Value LIKE '%' + t.name + '%') AS Hits
FROM @Targets t;

-- GOOD: one scan of AttributeValue, then JOIN against the small target set
WITH cand AS (
  SELECT CAST(Value AS NVARCHAR(MAX)) AS V
  FROM AttributeValue
  WHERE CAST(Value AS NVARCHAR(MAX)) LIKE '%_tmp%'
     OR CAST(Value AS NVARCHAR(MAX)) LIKE '%_restored%'
     OR CAST(Value AS NVARCHAR(MAX)) LIKE '%_restore%'
)
SELECT t.name, COUNT(*) AS Hits
FROM @Targets t
INNER JOIN cand c ON c.V LIKE '%' + t.name + '%'
GROUP BY t.name;
```

Always scan **all four** sources before declaring a name "unreferenced":

| Source | Catches |
|---|---|
| `sys.sql_modules` | stored procedures, views, functions, triggers |
| `AttributeValue` | Block configs (Dynamic Data Query), Lava-with-SQL, dashboard widgets, all workflow/job attribute values |
| `HtmlContent` | HTML content blocks |
| `ServiceJob` + `WorkflowActionType` AV (subset of AttributeValue, but worth running tightly to surface job/workflow specifically) | jobs and workflow-action SQL bodies |

## Migration shape for project SQL

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Commit BIT = 0;

DECLARE @Targets TABLE ([Name] sysname PRIMARY KEY);
INSERT INTO @Targets VALUES (N'…'), …;

BEGIN TRAN;

-- Pre-flight 1: existence
-- Pre-flight 2: incoming FKs (sys.foreign_keys where referenced_object_id matches)
-- Pre-flight 3: any project-specific invariants (e.g. blocks/pages already absent)

-- Snapshot: rows + size, current state of any cleanup targets

-- Action: DROP / DELETE / UPDATE statements

-- Post-action verification: target set should be empty

IF @Commit = 1 COMMIT TRAN; ELSE ROLLBACK TRAN;
```

Any failed pre-flight `RAISERROR(..., 16, 1)` then `ROLLBACK TRAN; RETURN;`. Run with `@Commit = 0` first; flip to 1 after reading the verification SELECTs.

**Run via sqlcmd:** `sqlcmd … -f 65001 -I -b -i <file>`. The `-f 65001` is required when the file has any non-ASCII characters (em-dashes in PRINT comments mojibake to `�?` without it). See `memory/feedback_sqlcmd_utf8.md`.

## File organization

```
claudefiles/rock/projects/
  └── <projectId>/                  # e.g. 6435/
        ├── 01-<step-description>.sql
        ├── 02-<step-description>.sql
        ├── 02-<step-description>.csv     # generated outputs
        ├── 02-<step-description>.xlsx
        ├── 04-<helper>.ps1               # supporting scripts
        └── …
```

Naming: `NN-description.<ext>`, lowercase, dash-separated. Mixed extensions are fine — SQL, CSVs, xlsx, and PS scripts coexist in one project folder. Pair create/backout where the change is reversible (mirrors the QA Pipeline family). Project folders live at the working-tree root (peer to `docs/`), not under `docs/sql/`.

## Working a project — typical session shape

**1. Open: load context.**
- Project record + most-recent comments.
- Fetch every attached file (description + each comment).
- **Enumerate sub-projects and tasks.** Run the `ParentProjectId` recursive CTE from the schema map; for every Active child, pull its latest comment in the same pass. Run the Task query and note any `State = 'Active'` rows. Carry this list into the assessment summary — don't drop it.
- **Check for prior-session notes:** if `claudefiles/rock/projects/<projectId>/` exists, glob for `PENDING_*.md` and read each. These capture state from prior sessions: what's done, what's pending, decisions awaiting user input, and resume instructions. Treat them as the highest-priority context — they often supersede what you'd infer from the comment thread alone.
- Identify the latest comment and *who's prompted to act next* (you, a stakeholder, the manager).

**2. Assess + check in on the ask.**
- State the project's actual scope (pulled from title + description, not the audit).
- Summarize back to the user: *the latest ask*, *who it's from*, *what action items it implies*, and *any deadline cues*. Then **stop and ask the user how they want to proceed** before doing further investigation.
- Do NOT start probing the schema, querying for the underlying setting, or drafting a plan in the same turn as the load-context output. The user may want to reframe, scope down, defer, or hand the action item to someone else — none of which you can predict from the comment thread alone.
- Map any "review", "validate", or "is this done" question to specific Rock objects with ModifiedDateTime evidence — but only after the user confirms that's the path forward.
- Surface attachments' out-of-scope content explicitly as "would be a separate Rock Request".

**3. Plan.**
- Phased plan, one step at a time, with cost/risk per step.
- Pause for user buy-in on the plan and again before each step.

**4. Execute, step by step.**
- Each step ends with a verification + a check-in.
- Migrations are dry-run (`@Commit = 0`) first; user reads the verification output, then approves flipping to 1.
- For UI work the user has to do (block deletion, plugin uninstall, job tuning), document precisely what to click and stand by — don't try to do it through SQL when the UI is the right tool.

**5. Close.**
- Final inventory or verification confirming the work landed.
- **Sub-project + task sweep (mandatory).** Re-run the sub-project tree and Task query. Before drafting any closure language, list every still-Active child and every Active task with its latest comment / assignee. If anything is still open, the parent isn't done — say so and stop here. (See gotcha #7.)
- **Draft a closure comment** for the user to post on the project (per `feedback_recommend_comment_on_project_completion.md`).

**5b. Pause (instead of close) — leave a PENDING note.**
If the session ends mid-stream — work in progress, a decision waiting on the user, an investigation branched but unresolved — write a `PENDING_<short-name>.md` file into `claudefiles/rock/projects/<projectId>/` capturing:
- **What's done** so far (committed migrations, generated artifacts) with file references.
- **What's pending** — the specific decision needed or next migration to write, and the data/findings that prompted it.
- **Resume instructions** — one short paragraph telling the next session exactly what to do first when this project is reopened.

Always prefix the filename with `PENDING_` so the load-context step (point 1 above) picks it up. Delete or rename to `RESOLVED_<…>.md` once the pending item is acted on.

## Gotchas

### 1. Action names lie — verify the configured target attribute
A workflow action named "Set Notification Person" may write to a different workflow attribute (`VoxKidsDirector`, etc.). Always resolve the action's configured `Attribute` / `To` / `CC` / `ResultAttribute` Guid against `Attribute.Guid` to get the real target. An attribute that looks unused (no action writes to it) may genuinely be vestigial. (Cross-references the same gotcha in `rock-workflow-deploy/SKILL.md`.)

### 2. Computed/derived values cycle and drop their Ids
Person attributes like `core_TimesCheckedIn16Wks`, `core_Era*`, `Family*`, `GivingUnit*`, and `ReportingFieldsUpdateDate` are recomputed by Rock cleanup/era jobs. Rows get deleted and recreated regularly, so an old AttributeValue snapshot will show a large set of "missing in live by Id" rows that aren't real data loss. Don't use that as evidence the live system lost data — confirm with the source code (e.g., look at attribute Key prefix `core_` for system-generated, computed-job-managed values).

### 3. Block deletion does not cascade AttributeValue cleanup
Deleting a Page or Block in the Rock UI removes those rows but leaves their `AttributeValue` rows orphaned (pointing at the now-deleted Block.Id). Rock's cleanup job catches these eventually. For project work, clean them up explicitly:
```sql
DELETE av
FROM AttributeValue av
INNER JOIN Attribute a ON av.AttributeId = a.Id
WHERE a.EntityTypeId = (SELECT Id FROM EntityType WHERE Name = N'Rock.Model.Block')
  AND av.EntityId IN ( <deleted block ids> );
```

### 4. Audits run on dev may not match prod — re-validate headline numbers
A database efficiency audit run against `rockdev` will agree with prod on most steady-state tables (Communication, BinaryFileData, History) but can wildly differ on transient tables. Project 6435's audit flagged ExceptionLog as 1.9 GB / 303K rows in dev — prod was 232 MB / 21K rows. The audit's #1 urgent recommendation didn't apply. Always re-run the headline size queries against prod before treating an audit's priorities as truth.

### 5. ModifiedDateTime can update on save without value change
Saving a workflow action attribute through the UI bumps `ModifiedDateTime` on every attribute even if only one value changed. A recent timestamp on a `To` field doesn't necessarily mean "this was the change Tania asked for" — it could just mean the action was saved. Cross-check by comparing against neighboring action attributes' ModifiedDateTime; if a whole batch updated together, that's a save event, not a targeted edit.

### 6. Manager pings ≠ stakeholder feedback
A "Gina, please respond to Adam" comment from a manager looks like activity but is not progress. When the latest comment is a ping rather than a stakeholder answer, the project is **blocked on input**. Status reports should say so explicitly. Don't queue more dev work until the input arrives.

### 7. Sub-projects can hide unresolved threads — never declare a parent "done" without recursing
The BBM PM plugin links projects via `Project.ParentProjectId`. A main project like 6188 may look complete on its own comment thread while child projects (e.g. 5506 "Activating inactive/archived people (Ryan Kuziel)", 6198 "Rock Serve Card Connections") are still Active with stakeholder asks open. Project 6188's 2026-05-04 thread surfaced exactly this — Everett asked "does this cover all of the sub-projects, e.g. the Ryan Kuziel one?" and the answer required walking each child individually.

**Mandatory before closing or telling the user "all notes/actions are addressed":**
1. Run the sub-project tree query above.
2. For every child where `IsActive = 1` and `CompletedDateTime IS NULL`, pull its latest comment and identify who's prompted to act next (you, a stakeholder, the manager).
3. Report each child explicitly in the status summary — `Done`, `Active – waiting on <person>`, `Blocked – <reason>`. Never aggregate them away as "the rest is handled".
4. If a child is Active with no recent comment activity, surface it as a gap to confirm with the user before signing off on the parent.

The same rule applies to per-project tasks: any `Task` row with `State = 'Active'` and no `CompletedDateTime` is an open item on the project even if the comment thread has gone quiet.

**One sub-project at a time** — when 2+ Active children exist, enumerate them with latest-comment context as a list of avenues and **stop**. Do not start investigating, planning, or executing across the set. The user picks which to take. After one is closed or paused, re-present the remaining list rather than auto-advancing. (See `memory/feedback_subprojects_one_at_a_time.md`.)

## Time tracking — Jira worklogs

Time spent on Rock Request project work is logged against a per-month Jira ticket in the **VDR** project on **razayyafinancial.atlassian.net** (cloudId `fc43df40-d0b4-4b49-8888-dfa934ccedea`).

### Finding the right ticket

Tickets are named **`<year>_<MonAbbr>`** — 3-letter English month abbreviation. Examples:
- May 2026 → `2026_May` → `VDR-25`
- Jun 2026 → `2026_Jun`
- Jan 2027 → `2027_Jan`

To resolve the current month's ticket, use `mcp__atlassian__searchJiraIssuesUsingJql` with JQL like `project = VDR AND summary ~ "2026_Jun"` rather than hardcoding the key — month tickets are pre-created but the user may not remember the next key.

### When to log

**One worklog per project per session**, booked at the natural end of that project's work block — e.g. after committing the last migration or after drafting the closure comment. Don't batch multiple projects into one worklog; don't log per-step within a single project.

### What to log

- **`timeSpent`** — Ask the user for the value every time before calling `addWorklogToJiraIssue`. Don't guess wall-clock time; the agent can't reliably tell how long the session actually took. A typical exchange: *"Wrapping up 4388. How much time should I log against VDR-25?"* — accept the answer verbatim (`2h`, `45m`, etc.) and pass it through.
- **`commentBody`** — Project Id + a one-line summary of what was accomplished. Example: `"4388 — Room Capacity dashboard chronological sort fix"`. Match the existing month's worklog style (the previous worklog reads `"Special Needs Intake Adjustments"`).
- **`started`** — Omit unless backdating; the API books at "now" by default.

### Call shape

```
mcp__atlassian__addWorklogToJiraIssue({
  cloudId: "fc43df40-d0b4-4b49-8888-dfa934ccedea",
  issueIdOrKey: "VDR-25",       // resolve from current month via JQL
  timeSpent: "<user-provided>",  // e.g. "2h 30m"
  commentBody: "<projectId> — <one-line summary>"
})
```

### Gotchas

- **One worklog per call** — `addWorklogToJiraIssue` creates a *new* worklog by default. To update one that's already booked (wrong time, typo in comment), pass `worklogId`.
- **Cross-month sessions:** if a session spans midnight on a month boundary, log against the month the work was *done in*, not the month the session started.
- **The monthly ticket is just a time bucket** — don't transition its status, don't comment on it via `addCommentToJiraIssue`, don't link Rock Request project Ids as issue links. The Note thread in Rock is the source of truth for the project work itself; Jira is purely the timesheet.

## Reference — pointers to related skills and memories

- **Workflow deploy half:** `rock-workflow-deploy` skill — for projects whose resolution is a workflow change you build/deploy.
- **QA Pipeline SQL:** `memory/reference_qa_pipeline_sql.md` → `claudefiles/rock/docs/sql/CLAUDE_NOTES.md`.
- **Pipeline docs cadence:** `memory/feedback_pipeline_docs_one_at_a_time.md` — one BemaPipeline DOCX per turn; skip pipelines 2/3/6/8.
- **MD = Ministry Defender:** `memory/reference_md_ministry_defender.md` — the abbreviation that comes up in volunteer-pipeline projects.
- **sqlcmd UTF-8:** `memory/feedback_sqlcmd_utf8.md` — pass `-f 65001` when migration files have non-ASCII chars.
- **No pre-escaping:** `memory/feedback_dont_pre_escape_source.md` — for sed-based generators.

## Closure comment template

When the work is done, draft something the user can paste.

**Audience first — keep it non-technical but informative.** Project comments are read by ministry staff (HR, kids, admin), not developers. Default to plain language: describe the *symptom*, what *behavior* changed, and what they should *see now*. Skip the implementation detail. The thread is the wrong place to teach Lava, Defined Values, attribute Guids, action configuration, FieldTypes, or any internal Rock plumbing — that belongs in the migration file's header comment or the skill's own notes.

Avoid: "Lava", "RawValue / Value accessor", "Defined Value Guid", "FieldTypeId", "AttributeValue", "WorkflowActionType", "the action", "the workflow attribute", action/attribute Ids, schema names, code snippets.

Use: the field name as it appears in the UI ("Employment Type field on the HR tab"), the page name ("Trigger Day Automations"), behavior verbs ("populates", "fills in", "shows up"), and what the user should look for to confirm.

Make exceptions only if the thread is already developer-to-developer (the participants are all Rock admins talking shop) — then technical detail is welcome because that's the thread's tone.

**Shape:**

> [Brief 1-line: what's now working / what changed in plain terms.]
>
> [Optional 1-2 sentences: what was happening before, in symptom terms, not implementation terms.]
>
> [If anything from the audit/spec is out of scope:] The [audit/spec] also raised X and Y, which I treated as out of scope here — happy to spin those out as separate Rock Requests if you'd like.
>
> [Verification ask:] Could you check the next [thing they'll see] and confirm it's working as expected? Let me know if anything still looks off.

Match the project thread's existing tone (formal vs. casual) and the requester's voice — if they're conversational, you're conversational; if they're terse, match that.
