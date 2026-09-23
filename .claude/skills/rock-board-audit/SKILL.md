---
name: rock-board-audit
description: Use this skill to assess the whole slate of Rock Request projects assigned to Adam - "assess my projects", "what's on my plate", "what am I working on", "board audit", "my open tickets", "what's waiting on me", "run the board", "status of everything", "audit the board by priority". Produces two tables - prioritized by the Work Order attribute, then unprioritized - each row carrying current status and an explicit waiting-on call. For a single project use rock-project-review instead; this is the fleet view that routes you there.
---

# Rock Request board audit

The recurring "where does everything stand?" sweep across every open Rock Request assigned to
Adam. One pass, two tables, a waiting-on call per row, and a short closing recommendation.

**This is the fleet view.** It does not open, plan or execute any single project - when the user
picks one out of the table, hand off to `rock-project-review/SKILL.md`.

## Scope - get this right or the whole report is noise

Three filters, all mandatory:

1. **Assigned to Adam** - Razayya Rock Dev Admin, **PersonId 48259**, via the `ProjectAssignee`
   junction table joined through `PersonAlias`, never a bare alias Id. Co-assignees are normal
   (Everett and Roza are on most); "assigned to Adam" does not mean "Adam solo". Never surface or
   recommend action on other people's tickets.
2. **Open** - `IsActive = 1 AND CompletedDateTime IS NULL`.
3. **Rock Requests** - `ProjectTypeId = 2`. Other project types are not this board.

See `memory/feedback_board_audit_assignee_filter.md` and `memory/reference_admin_user_id.md`.

## Priority means Work Order, and nothing else

**Attribute Id 28481, Key `WorkOrder`**, Integer, scoped to `ProjectTypeId = 2`. Everett sets it
to rank Adam's queue; every Work Order value in the system lands on one of Adam's projects. Ties
are allowed.

**Do NOT report `ProjectBoardCard.[Order]` as priority** - that is vertical position in a board
column and means nothing. ProjectTypeId 2 has no Priority attribute; the Priority attributes that
do exist (25573, 30919, 30501, 35519) belong to other project types.

**No Work Order is not "low priority."** It means the project sits outside Everett's ranking. An
unranked project can be the most urgent thing on the board - 7303 was due in three days with no
Work Order. Report the two groups separately and never merge them into one ranked list.

See `memory/reference_rock_request_work_order.md`.

## The queries

Run them in order. Query 1 is the spine; the rest fill in the judgement calls.

### 1. The board

```sql
SET NOCOUNT ON;
WITH mine AS (
  SELECT p.Id, p.Name, p.State, p.IsBlocked, p.DueDate, p.RequestDate, p.ParentProjectId
  FROM _com_blueboxmoon_ProjectManagement_Project p
  WHERE p.IsActive = 1 AND p.CompletedDateTime IS NULL AND p.ProjectTypeId = 2
    AND EXISTS (SELECT 1
                FROM _com_blueboxmoon_ProjectManagement_ProjectAssignee a
                JOIN PersonAlias al ON a.PersonAliasId = al.Id
                WHERE a.ProjectId = p.Id AND al.PersonId = 48259)
),
lastnote AS (
  SELECT n.EntityId AS ProjectId, n.CreatedDateTime, n.CreatedByPersonAliasId,
         ROW_NUMBER() OVER (PARTITION BY n.EntityId ORDER BY n.CreatedDateTime DESC) AS rn
  FROM Note n
  JOIN NoteType nt ON n.NoteTypeId = nt.Id
  WHERE nt.EntityTypeId = 963
)
SELECT
  ISNULL(av.Value,'-')                         AS WorkOrder,
  m.Id,
  LEFT(m.Name,60)                              AS Name,
  m.State,
  m.IsBlocked                                  AS Blk,
  CONVERT(varchar(10), m.DueDate, 120)         AS Due,
  CONVERT(varchar(10), ln.CreatedDateTime,120) AS LastCmt,
  LEFT(ISNULL(pp.NickName + ' ' + pp.LastName,'(none)'),24) AS LastBy,
  (SELECT COUNT(*) FROM _com_blueboxmoon_ProjectManagement_Task t
    WHERE t.ProjectId = m.Id AND t.State = 'Active')        AS OpenTasks,
  (SELECT COUNT(*) FROM _com_blueboxmoon_ProjectManagement_Project c
    WHERE c.ParentProjectId = m.Id AND c.IsActive = 1 AND c.CompletedDateTime IS NULL) AS OpenKids,
  ISNULL(m.ParentProjectId,0)                  AS Parent
FROM mine m
LEFT JOIN AttributeValue av ON av.AttributeId = 28481 AND av.EntityId = m.Id AND av.Value <> ''
LEFT JOIN lastnote ln ON ln.ProjectId = m.Id AND ln.rn = 1
LEFT JOIN PersonAlias lpa ON ln.CreatedByPersonAliasId = lpa.Id
LEFT JOIN Person pp ON lpa.PersonId = pp.Id
ORDER BY CASE WHEN TRY_CAST(av.Value AS int) IS NULL THEN 1 ELSE 0 END,
         TRY_CAST(av.Value AS int), ln.CreatedDateTime DESC;
```

`OpenKids > 0` marks an umbrella project; `Parent <> 0` marks a child. Both go in the table with
that relationship stated - a hub is not "waiting on Adam", it closes when its children do.

### 2. The last two comments on every row

Two, not one. The newest comment alone loses the exchange - a reply from Adam reads as "ball with
them" until you see it answering a question that is still open.

```sql
SET NOCOUNT ON;
WITH mine AS ( /* same mine CTE as query 1, Id only */ ),
n AS (
  SELECT n.EntityId AS Pid, n.CreatedDateTime, n.Text, n.CreatedByPersonAliasId,
         ROW_NUMBER() OVER (PARTITION BY n.EntityId ORDER BY n.CreatedDateTime DESC) AS rn
  FROM Note n JOIN NoteType nt ON n.NoteTypeId = nt.Id
  WHERE nt.EntityTypeId = 963
)
SELECT '### ' + CAST(n.Pid AS varchar(10)) + ' | ' + CONVERT(varchar(10), n.CreatedDateTime,120)
       + ' | ' + ISNULL(pp.NickName + ' ' + pp.LastName,'?') + ' | #' + CAST(n.rn AS varchar(3))
       + CHAR(13)+CHAR(10)
       + LEFT(REPLACE(REPLACE(CAST(n.Text AS nvarchar(max)),CHAR(13),' '),CHAR(10),' '), 700)
FROM n JOIN mine m ON m.Id = n.Pid
LEFT JOIN PersonAlias lpa ON n.CreatedByPersonAliasId = lpa.Id
LEFT JOIN Person pp ON lpa.PersonId = pp.Id
WHERE n.rn <= 2
ORDER BY n.Pid, n.rn;
```

`LEFT(...,700)` truncates, and **the ask usually lives at the end of the comment**. For any row
where the waiting-on call is not obvious from the first 700 characters, re-pull just that row's
tail with `RIGHT(..., 900)`.

### 3. Tasks, descriptions, assignees

Open tasks with their assignee for every project where query 1 showed `OpenTasks > 0`:

```sql
SELECT t.ProjectId, LEFT(t.Name,70) AS Task,
       ISNULL(pp.NickName + ' ' + pp.LastName,'(unassigned)') AS AssignedTo,
       CONVERT(varchar(10), t.DueDate,120) AS Due, t.IsBlocked
FROM _com_blueboxmoon_ProjectManagement_Task t
LEFT JOIN PersonAlias pa ON t.AssignedToPersonAliasId = pa.Id
LEFT JOIN Person pp ON pa.PersonId = pp.Id
WHERE t.State = 'Active' AND t.ProjectId IN (<ids>)
ORDER BY t.ProjectId, t.[Order], t.Id;
```

**Task assignee decides the waiting-on call, not the task count.** Ten open tasks all sitting on
Everett means the project is not waiting on Adam. Tasks assigned to 48259 are owed work even when
the comment thread is silent - they are often inserted directly with no email and no thread trace
(7547 carried seven of these).

For any project with **no comments at all**, pull `Description` - that is the entire brief:

```sql
SELECT Id, Name,
       LEFT(REPLACE(REPLACE(CAST(Description AS nvarchar(max)),CHAR(13),' '),CHAR(10),' '),450)
FROM _com_blueboxmoon_ProjectManagement_Project WHERE Id IN (<ids>);
```

### 4. Prior-session notes

Glob `claudefiles/rock/projects/<id>/PENDING_*.md` for every Id on the board and read the head of
each. These are the highest-fidelity status source on the sweep - they record what is committed
versus staged, what is drafted versus sent, and what decision is outstanding. A PENDING note
regularly contradicts what the comment thread implies, and the note wins.

Watch for `> **CORRECTED <date>**` blocks at the top - a note whose title says "nothing shipped"
may have been corrected in place. Read the correction before the body.

## Calling "waiting on you"

Judgement, not a formula. In rough order of reliability:

| Signal | Call |
|---|---|
| Open task assigned to 48259 | **Yes**, regardless of thread activity |
| Latest comment ends in a question aimed at Adam | **Yes** |
| Latest comment ends in a question Adam aimed at someone else | **No** - name them and count the days |
| Adam's own last comment, no question either way | **Read it** - his comments carry self-directed action notes |
| PENDING note says built / staged / dry-run, not committed | **Yes** |
| PENDING note says comment DRAFTED, not sent | **Yes** |
| Work shipped, closure comment posted, project still open | **Yes - ready to close** |
| Only comment(s) are Everett's brief, no reply from Adam | **Yes** - state the days of silence |
| Umbrella with open children | **No** - it closes when the children do |
| Everett or Roza's build, Adam co-assigned | **No** - say whose lane it is |
| Declared on hold / awaiting stakeholders | **No - parked** |

**Adam's comment being last does not mean the project is closed or handed off.** See
`memory/feedback_adam_last_response_is_not_closure.md`.

**Adam is a contractor.** Never assign him church-operational actions - running a service,
emailing a congregation, chasing a ministry decision. Those belong to Vox staff. See
`memory/feedback_adam_is_a_contractor_not_vox_staff.md`.

**System notes are not activity.** `completed task <span class="pm-comment-reference-task">` and
`re-opened task` rows are the plugin's own audit trail. A project whose two newest comments are
both task-system notes has had no human exchange - say so rather than reporting a recent date.

**A manager ping is not progress.** "Adam, please look at this" from Everett means the ball is
with Adam; "X, please respond to Adam" means the project is blocked on input.

## Output shape

Two tables, prioritized first, then unprioritized. Same columns on both:

`Work Order | Id | Project | Current status | Waiting on you?`

(The unprioritized table drops the Work Order column.)

- **Current status** is two to four sentences of substance - what shipped and when, the live
  numbers, what is staged, what the last exchange actually said. Not "in progress". Not a date
  alone. Concrete figures pulled from the thread carry more than adjectives.
- **Waiting on you** is Yes / No / Partly / Split, then the specific owed item, or the named
  person and how long they have had it.
- Bold dates that fall inside a week or have already passed.
- Order the prioritized table by Work Order ascending. Order the unprioritized table by how live
  the work is - commitments due first, then active threads, then unstarted, then parked.

**Close with a short recommendation**, two to four sentences: the commitment that is due, the
largest block of silence, and the finished work still counted as open. Lead with the
recommendation, do not bury it. Never attach hour estimates - describe scope relatively. See
`memory/feedback_project_assessment_lead_with_recommendation.md` and
`memory/feedback_time_estimates_overestimate.md`.

## After the table - stop

Present both tables and the recommendation, then **stop**. Do not start investigating a row, do
not draft a comment, do not open a project folder. The user picks what to take next; hand that one
to `rock-project-review/SKILL.md`.

When the user picks one, re-present the remaining list afterwards rather than auto-advancing. See
`memory/feedback_check_in_after_assess.md` and `memory/feedback_subprojects_one_at_a_time.md`.

## Running it

Connection params per `memory/reference_vox_sql_connection.md` - read the password from
`claudefiles/rock/prod/web.ConnectionStrings.config` with a case-insensitive grep
(`grep -io 'password='`); the key is lowercase.

```
sqlcmd -S voxfndtn-prod-rock-sql.database.windows.net -d RockDB -U rockuser -P "<pwd>" \
       -f 65001 -I -b -y 0 -i <file>.sql
```

- **`cd` into the SQL file's directory and pass a bare filename.** sqlcmd's `-i` splits a Windows
  absolute path on the drive colon and fails with `Access is denied`.
- `-f 65001` for UTF-8; `-y 0` for the wide `Note.Text` column. `-y 0` conflicts with `-W` and
  `-h`. Never pipe SQL through stdin - PowerShell prepends a BOM.
- Writing these query files with a large bash heredoc can trip the shell parser; use the Write
  tool for anything long.
- Avoid a bare `rm` table alias anywhere in these queries - the sandbox guard kills the whole
  command before it runs. See `memory/reference_sandbox_rm_token_guard.md`.

Scratch SQL for this sweep is throwaway - keep it in the session scratchpad, not in
`claudefiles/rock/projects/`.

## Gotchas

**1. Umbrella projects distort a flat count.** 7260 (Next Steps) and 7511 (2026 workflow audit)
each front a family. 7511 itself is not assigned to Adam - its three folder children (7547, 7553,
7559) are, and they appear as top-level rows with `Parent = 7511`. State the parentage so the
board does not read as more independent work than it is.

**2. Zero comments is a real state, not a gap.** Several projects exist only as a description
Everett wrote. They are unstarted work owed by Adam, not stalled threads - and that description is
the only brief that will ever exist. Read it.

**3. Everett's comments may be agent output.** From 2026-05 onward his comments are often
MCP-driven. Read them as briefs rather than as conversation, and expect them to be long,
structured, and to contain their own corrections. See `memory/project_everett_mcp_changes.md`.

**4. Shipped is not closed.** Projects routinely sit Active for weeks after the work landed and
the closure comment posted, waiting only on a snapshot table drop or a stakeholder confirmation.
These are the cheapest wins on the board - surface them as a group.

**5. Note times are Eastern, `GETDATE()` is UTC.** Do not compute "days since last comment" by
subtracting `GETDATE()` from `Note.CreatedDateTime`. Use a date boundary, or compare against
today's date as given in the session context. See
`memory/reference_rock_note_time_is_et_not_utc.md`.

**6. Do not log time for this sweep.** Time tracking runs as its own end-of-day pass via the
`log-time` skill. See `rock-project-review/SKILL.md`.

## Related

- `rock-project-review/SKILL.md` - the single-project deep dive this routes into.
- `rock-comment/SKILL.md` - required before drafting any comment that comes out of the sweep.
- `memory/reference_rock_request_work_order.md` - the priority attribute.
- `memory/feedback_board_audit_assignee_filter.md` - the assignee filter.
