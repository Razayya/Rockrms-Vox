---
name: rock-update
description: Use this skill when the user announces a Rock RMS version update on any Vox instance — phrases like "we're updating prod to X", "bumping dev to Y", "rock update to A.B.C", "verify rock X.Y.Z", or any change to the deployed Rock binaries. Drives the verification workflow end to end: collect version + instance inputs from the user (System Information drawer is the canonical source), repin overlay/instance-versions.json via scripts/repin-instance.ps1, materialize the matching SparkDev clone via scripts/prepare-build.ps1, diff the new clone against the prior pinned tree to surface upstream changes in subsystems Vox plugins touch (workflow actions, data filters, attribute field types, migrations, block contracts), build every Vox plugin against the new Rock and capture compile errors, walk the SparkDev migration delta for schema changes affecting Vox plugin models/queries, and produce a pre-deploy checklist. The user announces updates explicitly; this skill is never triggered by drift detection (there is none — pins are trusted).
---

# Rock update verification

This skill runs when the user announces a Rock version change on prod, dev, or any other instance. Rock updates are **user-initiated events** (see `memory/feedback_rock_update_user_initiated.md`) — never trigger this from drift detection alone; always confirm "is this a Rock update?" with the user before invoking.

## When to use this skill

- "We're updating prod to 18.0.1"
- "Dev got bumped to 18.2.4"
- "Verify Rock 18.2.4 before we deploy"
- The user wants to add a new instance to `overlay/instance-versions.json` (staging, sandbox)

## High-level pattern

1. **Collect inputs.** Friendly version + file version (System Information drawer, Version Info tab in Rock) + SparkDev tag + instance name. Never guess these — ask. The friendly version isn't queryable from SQL (it's an assembly attribute in `Rock.Version.dll`, see `feedback_read_rock_source_when_planning.md`). **User input is king** — once the user states the version, do not SQL-validate or second-guess it (see `memory/feedback_user_input_is_king_for_versions.md`).
2. **Capture the prior pin** for audit before overwriting. Show the user what's changing.
3. **Repin** via `scripts/repin-instance.ps1` — writes `overlay/instance-versions.json` with the user-supplied tag + friendly + file version.
4. **Materialize** via `scripts/prepare-build.ps1 -Instance <env>` — shallow-clones SparkDev at the new tag into `build/rock-source-<instance>-<tag>/`, junctions plugins in, injects sln.
5. **Diff against the prior clone.** Surface upstream changes in subsystems Vox plugins use. Bullet the deltas.
6. **Build every Vox plugin** against the new Rock. Capture compile errors.
7. **Walk the migration delta** between prior and new tag — flag schema changes that touch tables Vox plugins read or write.
8. **Pre-deploy checklist.** Single document for the user to review before they touch the live environment.

## Inputs to collect from the user (never invent these)

| Field | Source | Example |
|---|---|---|
| Instance | user | `prod` / `dev` / `staging` |
| Friendly version | System Info drawer, Version Info tab | `Rock McKinley 17.5 (issue #6486)` |
| File version | System Info drawer (the parenthetical) | `17.5.2.2` |
| SparkDev tag | maps to file version; confirm with user | `17.5.2.2` |
| Connection | claudefiles/rock/<instance>/web.ConnectionStrings.config (or explicit) | — |

If the user can't supply the friendly version, the live page footer of any Rock page also shows it. If you can't see either, **stop and ask** — do not pin a guess.

## Step-by-step

### 1. Capture the prior pin (if any)

```powershell
if (Test-Path overlay\instance-versions.json) {
    (Get-Content overlay\instance-versions.json | ConvertFrom-Json).instances.<Instance>
}
```

Write the prior pin to `claudefiles/rock/projects/rock-update-<date>-<instance>/00-prior-pin.json` so the audit trail survives the overwrite.

### 2. Repin

```powershell
.\scripts\repin-instance.ps1 `
    -Instance prod `
    -Tag 18.0.1 `
    -FriendlyVersion "Rock Humphreys 18.0 (issue #NNNN)" `
    -FileVersion 18.0.1 `
    [-Force]  # required if the instance is already pinned
```

If the instance is already pinned, the script refuses without `-Force` — that's intentional, so a Rock update is an explicit overwrite.

### 3. Materialize

```powershell
.\scripts\prepare-build.ps1 -Instance prod -Force
```

Force re-clones — important if the prior clone was at an earlier tag for the same instance.

### 4. Diff against the prior clone

Compare the new `build/rock-source-<instance>-<new_tag>/` against the prior `build/rock-source-<instance>-<old_tag>/` (kept on disk for one update cycle for exactly this reason).

Focus the diff on subsystems Vox plugins touch — don't waste context on UI strings, JS, or upstream-internal refactors:

| Subsystem | Diff anchor | What to flag |
|---|---|---|
| Workflow actions | `Rock/Workflow/Action/**/*.cs` | Renamed / removed actions Vox plugins extend or reference |
| Data filters | `Rock/Reporting/DataFilter/**/*.cs` | Filter selection format changes (Vox plugins parse these in SQL) |
| Field types | `Rock/Field/Types/**/*.cs` | Field type class renames / value format changes |
| Block contracts | `Rock/Blocks/**/*.cs`, `RockWeb/Blocks/**/*.cs` | Base class signature changes for blocks Vox plugins inherit |
| Cache APIs | `Rock/Web/Cache/Entities/*.cs` | Cache invalidation contract changes |
| Models | `Rock/Model/*.cs` | New/removed columns on tables Vox plugins query |
| Migrations | `Rock.Migrations/Migrations/*.cs` between prior tip and new tip | Schema changes (covered in step 7 separately) |

Suggested commands:

```powershell
$old = "$repoRoot\build\rock-source-<instance>-<old_tag>"
$new = "$repoRoot\build\rock-source-<instance>-<new_tag>"

# Quick name-status across the focus areas
git -C $old diff --no-index --name-status `
    "$old\Rock\Workflow\Action" "$new\Rock\Workflow\Action" 2>$null
```

`git diff --no-index` works across two non-git trees; pipe output through `head`/`Select-Object -First N` to keep the report scannable.

### 5. Build every Vox plugin

```powershell
$slnPath = "build\rock-source-<instance>-<new_tag>\Rock.sln"
foreach ($p in Get-ChildItem plugins -Directory) {
    $csproj = Get-ChildItem $p.FullName -Filter *.csproj | Select-Object -First 1
    msbuild $csproj.FullName /t:Build /p:Configuration=Debug /nologo /v:minimal
}
```

Capture failures into `claudefiles/rock/projects/rock-update-<date>-<instance>/01-build-errors.txt`. Don't try to fix them inline — surface them, then ask the user how to proceed (some failures are intentional API removals upstream, others are real bugs).

### 6. Migration delta

```powershell
# Migrations present in new tag but not in old tag
$oldMigs = Get-ChildItem "$old\Rock.Migrations\Migrations" -Filter '20*.cs' | Sort-Object Name
$newMigs = Get-ChildItem "$new\Rock.Migrations\Migrations" -Filter '20*.cs' | Sort-Object Name
$diff = Compare-Object $oldMigs.Name $newMigs.Name | Where-Object SideIndicator -eq '=>'
```

For each new migration, grep its body for:
- Tables Vox plugins read/write (your plugins' `[Table]` attributes — survey `plugins/**/Model/*.cs` for these names).
- Columns added/dropped on those tables.
- New stored procedures / views / indexes that Vox plugins depend on.

Save the annotated list to `claudefiles/rock/projects/rock-update-<date>-<instance>/02-migration-delta.md`.

### 7. Pre-deploy checklist

Single markdown file the user reviews before touching the live instance. Suggested template:

```markdown
# Rock update: <instance> <old_tag> -> <new_tag>

## Builds
- [ ] All Vox plugins compile against <new_tag> (see 01-build-errors.txt)

## API delta (impact on Vox plugins)
- [ ] Workflow actions: <summary>
- [ ] Data filters: <summary>
- [ ] Field types: <summary>
- [ ] Block contracts: <summary>
- [ ] Models / cache: <summary>

## Migration delta
- [ ] Reviewed N new migrations (see 02-migration-delta.md)
- [ ] No new schema changes affect Vox plugin tables, OR Vox migrations queued to align

## Plugin smoke checks
- [ ] BemaPipeline: pipeline runs, action types resolve
- [ ] CustomPersonAttributeSyncEngine: ServiceJob runs, calculation types resolve
- [ ] WebsiteExtensions: site renders
- [ ] (others)

## Production cutover
- [ ] Database backup taken
- [ ] Deploy window scheduled
- [ ] Rollback plan: <reference>
```

## Gotchas

### 1. The friendly version is NOT in SQL
`Rock.Version.dll` has `AssemblyInformationalVersion("Rock McKinley 18.2")` and `AssemblyFileVersion("17.5.2.2")` baked in at build time. These are assembly metadata, not DB state. Don't try to derive them via SQL — ask the user (System Information drawer is the canonical source). See `Rock.Version/AssemblySharedInfo.cs` for the mechanism.

### 2. Don't SQL-validate the user's version statement
User input is king. The pin file is authoritative between updates. Skills should not query SQL to "verify" a version the user stated. See `memory/feedback_user_input_is_king_for_versions.md` — this skill exists *because* version changes are announced explicitly; if SQL ever "disagrees", the answer is to ask the user about a new update, not auto-correct.

### 3. Keep the prior clone on disk for one cycle
The diff in step 4 needs both trees. Don't delete `build/rock-source-<instance>-<old_tag>/` until the next update lands. Disk is cheap; context is not.

### 4. Tag may not match file_version exactly
Most SparkDev tags are `<major>.<minor>.<patch>` (`18.2.4`) but some have suffixes (`18.2.4-beta2`). The user supplies the tag; don't construct it from the file_version alone.

### 5. Per-instance pinning, not global
prod and dev can be on different Rock versions. Every Rock skill reads its own pin from `overlay/instance-versions.json` for the env it's about to touch. Don't assume one source-of-truth clone covers both.

## File organization

```
overlay/
  instance-versions.json              # canonical pins
scripts/
  repin-instance.ps1                  # update the JSON (used by this skill)
  prepare-build.ps1                   # materialize a clone (used by this skill)
build/
  rock-source-<instance>-<tag>/       # per-instance clones, gitignored
claudefiles/rock/projects/
  rock-update-<date>-<instance>/      # this update's audit + reports
    00-prior-pin.json
    01-build-errors.txt
    02-migration-delta.md
    03-pre-deploy-checklist.md
```

## Reference

- `memory/feedback_rock_update_user_initiated.md` — never auto-pin from drift.
- `memory/feedback_read_rock_source_when_planning.md` — read the new clone during the delta phase, not as a fallback.
- `memory/reference_vox_sql_connection.md` — prod SQL endpoint.
- `Rock.Version/VersionInfo.cs` (in any rock-source clone) — the version display mechanism.
- `Rock.Version/AssemblySharedInfo.cs` — the bake-in for friendly + file version strings.
