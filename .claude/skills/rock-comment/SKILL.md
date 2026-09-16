---
name: rock-comment
description: Use this skill for every comment that goes onto a Rock Request (BBM PM) project — "draft a comment", "reply to Everett/Bru on NNNN", "post an update", "closure comment", "let them know", or any moment where the answer to a project belongs in the thread rather than in chat. Covers the two-part deliverable (a lay-readable plain-text body + a generated .docx carrying the technical write-up), the Outlook draft mechanics, and the attachment upload path. Pairs with rock-project-review, which routes here whenever a project needs a reply.
---

# Rock Request comments — lay body, technical doc

## The rule

Every comment posted to a Rock Request ships **two things**:

1. **The body** — plain text in an Outlook draft, written so a ministry staffer can read it once
   and know what happened and what we need from them. No Rock internals.
2. **The technical doc** — a generated `.docx` attached to that draft, carrying every detail the
   body deliberately left out: ids, Guids, SQL, counts, evidence, risk, verification.

This is the default for **every** comment on **every** thread, including threads where Everett is the
only other participant. Threads get forwarded, staff get added, and the same comment gets re-read
six months later by someone who wasn't there. The body is what people act on; the doc is what people
check. Only Adam saying otherwise in that turn changes it.

## When this skill fires

Any time the deliverable is a comment on a project: a status update, an answer to a stakeholder
question, a proposal, a closure comment, a correction. `rock-project-review` routes here at its
step 5; so does any session that ends with "let them know" or "draft a reply".

It does **not** govern in-conversation answers to Adam — those stay markdown and technical.

## The only times the doc is skipped

Skip the `.docx` **only** when the comment carries no technical content whatsoever:

- a pure acknowledgement — "got it, will do", "thanks, that answers it"
- scheduling and logistics only — "I can look at this Thursday", "sending it after the 27th"
- a non-technical answer to a non-technical question — "yes, Bru owns that list"

Everything else gets one. That includes every status update, every closure comment, anything that
proposes a change, anything that asks a decision question, and anything that reports a number. If
you find yourself arguing that a comment is "too small" for a doc while it still contains a count, a
date-stamped finding, or a change you made to prod — it gets a doc. The skip list above is
exhaustive; it is not a template for inventing new exemptions.

---

## Part 1 — the body

### Shape

```
Hi <first name>,                       ("Hi Bru and Everett," when it's both)

<one line naming the shape of the comment>
  e.g. "Two answers, and one thing still open."
       "This one is done - one thing to check on your end."

<short paragraph per topic, each opening with what the topic is>

<the ask, last, as a direct question>
```

No sign-off — the comment posts under the Vox admin account and the thread shows who wrote it.
Match the thread's tone: conversational with Bru, terser with Everett.

Target 120–250 words. If it runs longer, the excess is almost always technical — move it to the doc.

### Voice

- **Symptom, not mechanism.** What someone would have noticed, not what caused it.
- **Numbers survive, ids don't.** "84 forms in the last 90 days" is exactly right. "WT 315 fired 84
  times" is not. Counts, dates and durations are plain English; object ids never are.
- **Own the problem.** "That's on us, not you" when it's ours. Don't narrate blame or process.
- **Say what you need, plainly.** The ask is a question the reader can answer in one sentence.
- **ASCII only.** Hyphens, not em-dashes; straight quotes. The body posts verbatim into the Note.

### The translation table

| Don't write | Write |
|---|---|
| person attribute 4936 `BaptismInterest` | the Baptism Interest mark on someone's profile |
| workflow type 324, action 6187 | the baptism form on the website |
| JourneyTrack calculation 86 | the step that marks Schedule Baptism complete on the Pathway |
| block 5820 `HeaderTemplate` / Lava | the Pathway panel in the app |
| ServiceJob 319 runs at 17:00 | the 5pm email |
| ConnectionOpportunity 89 | a Next Steps connection |
| Defined Value 3421 | the Get Baptized Info tick on the welcome card |
| `RegistrationTemplate.RegistrantWorkflowTypeId` | signing up for a baptism |
| dry run with `@Commit = 0` | I tested it without saving anything |
| backfilled 51 AttributeValue rows | filled in the 51 people whose sign-ups predate the fix |
| the attribute has no usable timestamp | there's no reliable date for when someone raised their hand |

Also banned from the body: Lava, Defined Value, FieldType, AttributeValue, WorkflowActionType, Guid,
"the action", "the workflow attribute", schema names, file names, code, and any bare id.

### Worked example

**Technical draft (wrong for the body):**

> Calc 86's `Active` AttributeValue is False but that flag is vestigial — `JourneyCalculation.IsActive`
> is 1 and the run log shows 1,567 updates since 08-17, so the OR on `BaptismInterest` is live. 93
> people have attr 4936 = True with no BaptismDate and no registration on templates 216/222/283.

**Lay body (right):**

> One correction to what's in the request. I'd said this wasn't switched on yet — it is. The Pathway
> has been marking Schedule Baptism complete off the Baptism Interest mark since mid-August.
>
> That matters because the mark means two different things today. 93 people carry it who have never
> registered for a baptism and have never been baptized — they're reading as having scheduled one.

The ids, the run counts, the filter JSON and the query behind "93" all go in the doc.

### The body has to stand on its own

**Every decision the comment asks for must be answerable from the body alone.** The doc is optional
reading. Never park a question, a deadline, or a piece of bad news only in the attachment — if it
changes what the reader does, it belongs in the body in plain language.

State each open question as its own numbered item, and say what it unblocks:

```
Two things I need from you:

1. Who should be able to open the report - you, the Next Steps directors, the
   discipleship directors? Nothing changes for staff until I know who to give it to.
2. After we route with-kids families to the kids team, does that team follow up on
   their own? If not, those families end up with less contact, not more.
```

### Formatting

Plain text, always (`contentType: "text"`). No markdown — no `**bold**`, no `#`, no tables. Leading
`-` bullets and numbered lists are fine. The body becomes the Note verbatim, so anything else shows
as literal characters. (`memory/feedback_project_response_plaintext.md`.)

---

## Part 2 — the technical doc

### What belongs in it

Everything the body dropped, written for the next engineer — Everett, or us in six months:

- the objects by id, key and Guid, and which ones changed
- the queries that produced every number in the body, with the date they were run
- the evidence trail: what was verified against prod, what is inferred, what is still unknown
- what changed, what it replaces, and how to back it out
- verification steps someone else could re-run
- the open questions restated technically, with the options and their consequences

Be explicit about confidence. "Verified against prod 2026-09-16" and "inferred from the run log, not
directly observed" are different claims and the doc should keep them apart.

### Section skeleton

Adapt, don't pad. A doc with three real sections beats one with eight thin ones.

1. **What this covers** — the ask in one paragraph, and what the comment said in lay terms.
2. **What we found / what changed** — the substance, with ids and evidence.
3. **Evidence** — the queries and counts, dated.
4. **Risk and backout** — what could go wrong, how to reverse it, what's irreversible.
5. **Verification** — numbered steps, re-runnable by someone else.
6. **Open questions** — each one technically framed, with the options.

### Generating it

`gen_comment_doc.py` lives beside this file. Write a JSON spec, run it, get a styled `.docx`:

```bash
python "C:/Users/AdamC/source/repos/Rockrms-Vox/.claude/skills/rock-comment/gen_comment_doc.py" \
  "C:/Users/AdamC/source/repos/claudefiles/rock/projects/7607/comment-2026-09-16-calc-86.json"
```

Spec shape (full block reference is in the script's docstring):

```json
{
  "project_id": 7607,
  "title": "Baptism Interest split - technical notes",
  "subtitle": "Rock Request 7607 (child of 7411)",
  "prepared": "2026-09-16",
  "sections": [
    {"heading": "What we found", "blocks": [
      {"type": "p",       "text": "prose paragraph"},
      {"type": "bullets", "items": ["...", "..."]},
      {"type": "numbers", "items": ["step one", "step two"]},
      {"type": "kv",      "rows": [["Real switch", "JourneyCalculation.IsActive = 1"]]},
      {"type": "table",   "columns": ["Bucket", "People"], "rows": [["Baptized", "254"]],
                          "caption": "verified against prod 2026-09-16"},
      {"type": "code",    "text": "SELECT ...", "caption": "the query behind the counts"},
      {"type": "note",    "text": "the one thing a reader must not miss"}
    ]}
  ]
}
```

`kv` is the borderless two-column grid for object facts; `table` is a headed data table; `note` is
the shaded callout — use at most one or two per doc or it stops meaning anything.

Write the spec as a file and run the script. Don't hand-roll python-docx per comment, and don't
build the docx inline in a shell one-liner — the spec file is the record of what was in the doc.

### Naming and where it lands

- Spec: `claudefiles/rock/projects/<projectId>/comment-YYYY-MM-DD-<slug>.json`
- Doc: same stem, `.docx` — the script writes it beside the spec by default.
- **Attachment name = the spec's `title` + `.docx`.** That string becomes a link in the comment and
  is the only name staff ever see, so it reads as a title, not a filename:
  `Baptism Interest split - technical notes.docx`, never `comment-2026-09-16-calc-86.docx`.

One doc per comment, not per project. A second comment on the same topic gets its own dated spec —
never silently re-attach a stale doc.

### What must not go in it

The attachment lands in Rock as a file anyone on that thread can open. No passwords, no connection
strings, no `web.ConnectionStrings.config` contents, no server names paired with credentials, and no
personal data beyond what the thread already discusses.

---

## Part 3 — delivery

### The draft

- **To:** `project-comment@mg.voxchurch.org`
- **Subject:** must contain the token `(#<projectId>/48347)`. Anything around it is free text —
  `Baptism Interest split - correction on calculation 86 (#7607/48347)`. Alias `48347` = Razayya Rock
  Dev Admin; attribution comes from that token, not from the sending mailbox.
- **From:** adam.beard@Razayya.com — the only account the ms365 MCP has. A fresh draft is fine; there
  is no need to reply to the notification email (those live in the rock@razayya.com shared mailbox,
  which the MCP cannot reach).
- `create-draft-email` with `contentType: "text"`.

### Attaching the doc

**Use an upload session, not `add-mail-attachment`.** A ~40 KB docx is ~53 KB of base64 and gets
truncated in transit — it fails with `UnableToDeserializePostBody`, or worse, succeeds with a corrupt
file. The upload path works at any size:

1. `create-mail-attachment-upload-session` with
   `{AttachmentItem: {attachmentType: 'file', name: '<Title>.docx', contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', size: <exact bytes>}}`
2. PUT the bytes to the returned (pre-authenticated) `uploadUrl`:
   ```powershell
   $b = [IO.File]::ReadAllBytes($path)
   Invoke-WebRequest -Uri $url -Method Put -Headers @{"Content-Range"="bytes 0-$($b.Length-1)/$($b.Length)"} `
     -Body $b -ContentType "application/octet-stream" -UseBasicParsing
   ```
   → 201.
3. Verify: `list-mail-attachments` (its reported `size` runs ~1% high — MIME overhead, not
   corruption), then `download-bytes-to-file` on
   `/me/messages/{id}/attachments/{attId}/$value` and SHA256-compare against the local file.

### Verify before handing it over

- [ ] The doc opens in Word without a repair prompt. Word COM works on this machine:
      `$w=New-Object -ComObject Word.Application; $d=$w.Documents.Open($path,$false,$true)`
      (Outlook COM does **not** — `REGDB_E_CLASSNOTREG`.)
- [ ] Body has no markdown, no em-dashes, no object ids, no jargon from the banned list.
- [ ] Every question in the comment is answerable without opening the doc.
- [ ] Every number in the body appears in the doc with the query and date behind it.
- [ ] Subject token matches the project — a wrong id posts the comment on the wrong request.
- [ ] Attachment is named as a title, and its SHA256 matches the local file.
- [ ] Show the full body in-conversation so Adam can read it before sending.

### Never send it

The draft stays in Drafts. Adam reviews and sends from Outlook. This is not negotiable, and it
applies to the attachment too — don't "just send it" because the draft looks finished.

If ms365 isn't connected, **say so up front** and fall back to a `.txt` next to the spec — never a
silent fallback (`memory/feedback_surface_ms365_outage_immediately.md`).

---

## Gotchas

**1. An Outlook draft updated after Adam opened it resends the old copy.** If you revise a draft that
has already been opened in the Outlook client, the client can hold and send the stale version. When a
draft changes materially, say so explicitly and confirm the posted Note afterwards
(`memory/reference_outlook_draft_update_stale.md`).

**2. A drafted comment is not a posted comment.** Don't record a thread as answered, or write
"replied" into a PENDING note, until the Note exists on the project. Verify with the Note query in
`rock-project-review`.

**3. The doc is not cover for a vague body.** Moving an uncomfortable detail into the attachment so
the body reads cleaner is the failure mode this skill exists to prevent. If it changes what the
reader decides, it goes in the body.

**4. Staged comments still get their doc.** When a closure comment is parked in a `PENDING_*` runbook
to post after a validation date, generate the doc then and reference both files in the runbook —
don't leave it as a to-do for the future session.

**5. Don't ask whether to build the draft.** "Draft a comment" means the Outlook draft with its
attachment, built now — not a `.txt` and an offer
(`memory/feedback_project_comment_draft_to_outlook.md`).

**6. Whether a comment is warranted at all is a separate question.** If the thread's latest comment
already prompts the next action, don't add a redundant one
(`memory/feedback_no_redundant_comment_recs.md`); closure comments stay brief
(`memory/feedback_closure_comments_brief.md`).

---

## Related

- `rock-project-review/SKILL.md` — project context, the closure-comment decision, sub-project sweep.
- `memory/reference_rock_comment_via_email.md` — the email posting mechanics and their validation.
- `memory/feedback_project_response_plaintext.md` — plain text, no markdown.
- `memory/feedback_project_comment_draft_to_outlook.md` — the draft is the deliverable.
- `memory/feedback_staff_deliverables_xlsx_not_csv.md` — when the attachment is data, it's xlsx with
  Rock links, not a sqlcmd CSV. The technical doc doesn't replace that; a comment can carry both.
- `memory/feedback_adam_is_a_contractor_not_vox_staff.md` — never assign Adam church-operational
  actions in the body.
