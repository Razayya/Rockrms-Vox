# Rockrms-Vox — Claude context hub

This repo (`C:\Users\AdamC\source\repos\Rockrms-Vox`) is the **home for all Claude
context** for Vox Rock work: skills, memory, and conventions live here, even when
the shell cwd is a thin source checkout under `build\` (its own git root, so the
harness gives it an empty memory namespace and does not register these skills).

## Session bootstrap — do this first, every session

1. **Load memory.** Read the Rockrms-Vox memory index and pull anything relevant:
   `C:\Users\AdamC\.claude\projects\C--Users-AdamC-source-repos-Rockrms-Vox\memory\MEMORY.md`
   The harness will not auto-recall it from a `build\*` cwd — the namespace differs.

2. **Know the skills.** Project skills live at
   `C:\Users\AdamC\source\repos\Rockrms-Vox\.claude\skills\`. They are **not**
   auto-registered as `/slash` skills from a `build\*` cwd — read the relevant
   `SKILL.md` directly and follow it before doing DB exploration:

   | User says… | Read this skill |
   |---|---|
   | "project NNNN" / "review project NNNN" / "work on NNNN" / any Rock Request Id | `rock-project-review/SKILL.md` |
   | create a page / block / attribute | `rock-page-create/SKILL.md` |
   | build / deploy a workflow | `rock-workflow-deploy/SKILL.md` |
   | update Rock to a version | `rock-update/SKILL.md` |

## Conventions

- "ticket" / "project" / "request" = a **Rock Request (BBM PM)**, not Jira.
  `project 6535` → Rock Request Id 6535 → use `rock-project-review`.
- `build\rock-source-<instance>-<tag>\` is a thin Rock **source checkout** — read
  it when planning; don't commit Claude artifacts into it.
- "working dir" means `C:\Users\AdamC\source\repos\claudefiles\rock` (the
  workspace), not the shell cwd or the codebase.
