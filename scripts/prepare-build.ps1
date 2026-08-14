<#
.SYNOPSIS
  Build the SparkDev Rock tree pinned for an instance, overlay Vox plugins, inject sln entries.

.DESCRIPTION
  Reads overlay/instance-versions.json for the named instance, shallow-clones
  SparkDevNetwork/Rock at instances.<Instance>.tag into instances.<Instance>.clone_dir,
  junctions every directory under plugins/ into the clone, recursively overlays
  each plugin's RockWeb/ subtree (e.g. WebForms .ascx blocks under
  Plugins/<vendor>/<plugin>/) onto the cloned RockWeb/, then derives Project /
  ProjectConfigurationPlatforms / RockWeb-ProjectReferences sln entries from each
  plugin's csproj <ProjectGuid> and injects them into the upstream Rock.sln.

  Junctions (not symlinks) avoid the Windows Developer Mode / admin requirement.
  Edits in plugins/ are live in the cloned Rock tree, including .ascx files.

.PARAMETER Instance
  Instance name from overlay/instance-versions.json (e.g. prod, dev).

.PARAMETER RockRepo
  Upstream Rock repo URL. Default: https://github.com/SparkDevNetwork/Rock.git

.PARAMETER Force
  Wipe and re-clone if the instance's clone_dir already exists. Otherwise re-overlay
  plugins + re-inject sln in place.

.EXAMPLE
  .\scripts\prepare-build.ps1 -Instance prod
  .\scripts\prepare-build.ps1 -Instance dev -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Instance,
    [string]$RockRepo = 'https://github.com/SparkDevNetwork/Rock.git',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# ============================================================
# Junction-safe tree removal
# ============================================================
# Plugins are junctioned into the clone at TWO depths: the plugin dirs at the
# clone root (step 2) and each plugin's RockWeb/ subtree, nested arbitrarily deep
# under RockWeb/Plugins/<vendor>/<plugin> (step 2.5). Remove-Item -Recurse can
# traverse a junction and delete the TARGET's contents - i.e. real plugin source
# in plugins/ - so every reparse point must be unlinked before the recursive
# delete. This used to unlink only the top-level ones, which left the RockWeb
# overlays live inside the delete.
#
# The walk deliberately does not descend into a reparse point (that would
# enumerate the plugin's own files and, on some PowerShell versions, follow it).
function Get-ReparsePointsSafe {
    param([string]$Root)
    $found = New-Object System.Collections.Generic.List[string]
    $stack = New-Object System.Collections.Generic.Stack[string]
    $stack.Push($Root)
    while ($stack.Count -gt 0) {
        $dir = $stack.Pop()
        try { $subs = [IO.Directory]::EnumerateDirectories($dir) } catch { continue }
        foreach ($sub in $subs) {
            try { $attr = [IO.File]::GetAttributes($sub) } catch { continue }
            if ($attr -band [IO.FileAttributes]::ReparsePoint) { $found.Add($sub) }
            else { $stack.Push($sub) }
        }
    }
    return $found
}

function Remove-TreeSafely {
    param([string]$Path)
    $links = Get-ReparsePointsSafe -Root $Path
    foreach ($l in $links) {
        Write-Host "  unlink junction: $l"
        [IO.Directory]::Delete($l, $false)
    }
    $remaining = Get-ReparsePointsSafe -Root $Path
    if ($remaining.Count -gt 0) {
        throw "Refusing to delete $Path - $($remaining.Count) reparse point(s) still present; a recursive delete could destroy plugin source."
    }
    Remove-Item -Recurse -Force $Path
}

$repoRoot = (& git rev-parse --show-toplevel).Trim().Replace('/', '\')
$pluginDir = Join-Path $repoRoot 'plugins'
$jsonPath  = Join-Path $repoRoot 'overlay\instance-versions.json'

if (-not (Test-Path $jsonPath)) { throw "Missing $jsonPath - run repin-instance.ps1 first" }
$json = Get-Content $jsonPath -Raw | ConvertFrom-Json
if (-not $json.instances.$Instance) {
    $known = @($json.instances.PSObject.Properties.Name) -join ', '
    throw "Instance '$Instance' not pinned. Known instances: $known. Use rock-update skill to add."
}
$pin = $json.instances.$Instance
$tag = $pin.tag
$cloneRel = $pin.clone_dir
if (-not $tag)      { throw "instance.$Instance.tag is empty" }
if (-not $cloneRel) { throw "instance.$Instance.clone_dir is empty" }

$TargetDir = Join-Path $repoRoot $cloneRel
Write-Host "Instance: $Instance"
Write-Host "Friendly: $($pin.friendly_version)"
Write-Host "Tag:      $tag"
Write-Host "Target:   $TargetDir"

if (-not (Test-Path $pluginDir)) { throw "Missing plugins directory: $pluginDir" }
$plugins = Get-ChildItem $pluginDir -Directory
if ($plugins.Count -eq 0) { throw "No plugin directories found under plugins/" }
Write-Host "Plugins to overlay: $($plugins.Count)"

# ============================================================
# 1. Clone (or re-use) upstream Rock at the pinned tag
# ============================================================
if (Test-Path $TargetDir) {
    if ($Force) {
        Write-Host "Removing existing $TargetDir (-Force)..."
        Remove-TreeSafely -Path $TargetDir
    } else {
        Write-Host "Re-using existing clone at $TargetDir (pass -Force to re-clone)."
    }
}

if (-not (Test-Path $TargetDir)) {
    $parent = Split-Path $TargetDir -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    Write-Host "Cloning $RockRepo @ $tag (shallow) ..."
    # ErrorActionPreference=Stop wraps git's normal stderr ("Cloning into...") as a
    # terminating error before the clone finishes. Drop to Continue locally and rely
    # on $LASTEXITCODE for the actual outcome.
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git clone --depth 1 --branch $tag $RockRepo $TargetDir
        if ($LASTEXITCODE -ne 0) { throw "git clone failed for tag '$tag'. Confirm the tag exists upstream." }
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}

# ============================================================
# 2. Junction every plugins/<dir> into the clone root
# ============================================================
Write-Host "Overlaying plugins via junctions..."
$pluginEntries = @()
foreach ($p in $plugins) {
    $linkPath = Join-Path $TargetDir $p.Name
    if (Test-Path $linkPath) {
        $item = Get-Item $linkPath -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            [IO.Directory]::Delete($linkPath, $false)
        } else {
            throw "Path collides with non-junction: $linkPath"
        }
    }
    New-Item -ItemType Junction -Path $linkPath -Target $p.FullName | Out-Null

    $csproj = Get-ChildItem $p.FullName -Filter '*.csproj' | Select-Object -First 1
    if ($null -eq $csproj) { Write-Warning "  $($p.Name): no csproj, skipping sln entry"; continue }
    $csprojContent = Get-Content $csproj.FullName -Raw
    if ($csprojContent -notmatch '<ProjectGuid>\{([A-F0-9-]+)\}</ProjectGuid>') {
        Write-Warning "  $($p.Name): csproj missing ProjectGuid, skipping sln entry"; continue
    }
    $guid = $matches[1].ToUpper()
    $csprojRel = "$($p.Name)\$($csproj.Name)"
    $pluginEntries += [pscustomobject]@{ Name = $p.Name; Guid = $guid; CsprojRel = $csprojRel }
    Write-Host "  $($p.Name) -> {$guid}"
}
if ($pluginEntries.Count -eq 0) { throw "No plugin sln entries derived; check csproj files." }

# ============================================================
# 2.5 Overlay each plugin's RockWeb/ subtree onto <TargetDir>/RockWeb/
# ============================================================
# Convention: a plugin can ship RockWeb files (.ascx blocks, themes, content)
# under plugins/<name>/RockWeb/<relative-path>/. Mirror the tree by junctioning
# the deepest directory that contains files. Intermediate directories that
# contain only subdirs get plain-mkdir'd so multiple plugins (or SparkDev's
# own content) can coexist alongside each other under the same parent.
#
# This refuses to overlay a non-empty SparkDev-owned directory; if a plugin
# needs to add files into a directory SparkDev already populates, do it
# explicitly with a separate file-level copy step instead.
function Overlay-RockWebDir {
    param([string]$Src, [string]$Dst)
    $files = @(Get-ChildItem -LiteralPath $Src -File -Force -ErrorAction SilentlyContinue)
    $subdirs = @(Get-ChildItem -LiteralPath $Src -Directory -Force -ErrorAction SilentlyContinue)

    if ($files.Count -gt 0 -or $subdirs.Count -eq 0) {
        # This dir holds files (or is an intentional leaf) -> junction it.
        if (Test-Path -LiteralPath $Dst) {
            $item = Get-Item -LiteralPath $Dst -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                [IO.Directory]::Delete($Dst, $false)
            } else {
                $existing = Get-ChildItem -LiteralPath $Dst -Force -ErrorAction SilentlyContinue
                if ($existing.Count -eq 0) {
                    Remove-Item -LiteralPath $Dst -Force
                } else {
                    throw "RockWeb overlay collision (non-junction, non-empty): $Dst. SparkDev owns this path; resolve manually."
                }
            }
        }
        $parent = Split-Path $Dst -Parent
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        New-Item -ItemType Junction -Path $Dst -Target $Src | Out-Null
        Write-Host "  RockWeb overlay: $($Dst.Substring($TargetDir.Length+1)) -> $Src"
    } else {
        # Only subdirs -> mkdir locally and recurse so siblings can coexist.
        if (-not (Test-Path -LiteralPath $Dst)) { New-Item -ItemType Directory -Path $Dst -Force | Out-Null }
        foreach ($sub in $subdirs) {
            Overlay-RockWebDir -Src $sub.FullName -Dst (Join-Path $Dst $sub.Name)
        }
    }
}

$targetRockWeb = Join-Path $TargetDir 'RockWeb'
foreach ($p in $plugins) {
    $rockWebSrc = Join-Path $p.FullName 'RockWeb'
    if (-not (Test-Path -LiteralPath $rockWebSrc)) { continue }
    Write-Host "Overlaying $($p.Name)/RockWeb/ ..."
    foreach ($sub in Get-ChildItem -LiteralPath $rockWebSrc -Directory -Force) {
        Overlay-RockWebDir -Src $sub.FullName -Dst (Join-Path $targetRockWeb $sub.Name)
    }
}

# ============================================================
# 3. Inject plugin entries into upstream Rock.sln
# ============================================================
$slnPath = Join-Path $TargetDir 'Rock.sln'
if (-not (Test-Path $slnPath)) { throw "Missing $slnPath in upstream clone." }
Write-Host "Injecting plugin entries into Rock.sln..."

$sln = Get-Content $slnPath -Raw
$csprojTypeGuid = 'FAE04EC0-301F-11D3-BF4B-00C04F79EFBC'

# Project / EndProject blocks (skip plugins already present, in case sln already had them)
$missingProjects = $pluginEntries | Where-Object { $sln -notmatch [regex]::Escape("`"{$($_.Guid)}`"") }
if ($missingProjects.Count -gt 0) {
    $projectBlocks = foreach ($e in $missingProjects) {
        "Project(`"{$csprojTypeGuid}`") = `"$($e.Name)`", `"$($e.CsprojRel)`", `"{$($e.Guid)}`"`r`nEndProject"
    }
    $projectInsertion = ($projectBlocks -join "`r`n") + "`r`n"
    if ($sln -notmatch '(?m)^Global\s*$') { throw "Could not find 'Global' marker in Rock.sln" }
    $sln = $sln -replace '(?m)^(Global\s*)$', "$projectInsertion`$1"
}

# ProjectConfigurationPlatforms entries (skip ones already present)
$cfgLines = @()
foreach ($e in $pluginEntries) {
    foreach ($cfg in 'Debug','Release') {
        $line1 = "`t`t{$($e.Guid)}.$cfg|Any CPU.ActiveCfg = $cfg|Any CPU"
        $line2 = "`t`t{$($e.Guid)}.$cfg|Any CPU.Build.0 = $cfg|Any CPU"
        if ($sln -notmatch [regex]::Escape($line1.TrimStart())) { $cfgLines += $line1 }
        if ($sln -notmatch [regex]::Escape($line2.TrimStart())) { $cfgLines += $line2 }
    }
}
if ($cfgLines.Count -gt 0) {
    $cfgInsertion = ($cfgLines -join "`r`n") + "`r`n"
    $cfgPattern = '(?ms)(GlobalSection\(ProjectConfigurationPlatforms\) = postSolution\r?\n.*?)(\r?\n\tEndGlobalSection)'
    if ($sln -notmatch $cfgPattern) { throw "Could not find ProjectConfigurationPlatforms section in Rock.sln" }
    $sln = [regex]::Replace($sln, $cfgPattern, {
        param($m) "$($m.Groups[1].Value)`r`n$cfgInsertion".TrimEnd("`r","`n") + $m.Groups[2].Value
    })
}

# RockWeb ProjectReferences (idempotent)
$refsToAdd = foreach ($e in $pluginEntries) { "{$($e.Guid.ToLower())}|$($e.Name).dll" }
$refPattern = '(ProjectReferences = ")(.*?)(")'
if ($sln -notmatch $refPattern) {
    Write-Warning "RockWeb ProjectReferences not found in this Rock.sln; plugins will still load at runtime but won't be IIS-precompiled together."
} else {
    $sln = [regex]::Replace($sln, $refPattern, {
        param($m)
        $existing = $m.Groups[2].Value
        $missing = @()
        foreach ($r in $refsToAdd) { if ($existing -notmatch [regex]::Escape($r)) { $missing += $r } }
        if ($missing.Count -eq 0) { return $m.Value }
        $newExisting = $existing
        if (-not $newExisting.EndsWith(';')) { $newExisting += ';' }
        $newExisting += ($missing -join ';') + ';'
        return "$($m.Groups[1].Value)$newExisting$($m.Groups[3].Value)"
    }, 1)
}

Set-Content -LiteralPath $slnPath -Value $sln -Encoding UTF8 -NoNewline

# ============================================================
# 4. NuGet restore (PackageReference + packages.config in one pass)
# ============================================================
# Ask vswhere for the newest install rather than globbing a hard-coded year:
# this used to look only under 'Visual Studio\2022\', so on a box running any
# other VS build it silently warned and skipped the restore, leaving the tree
# unbuildable in a way that only surfaced later as missing-assembly errors.
$msbuild = $null
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath 2>$null
    if ($vsPath) {
        $candidate = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe'
        if (Test-Path $candidate) { $msbuild = $candidate }
    }
}
if (-not $msbuild) {
    # Fallback: any Visual Studio version, newest path last.
    $msbuild = Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\MSBuild.exe' `
        -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1 -ExpandProperty FullName
}

if (-not $msbuild) {
    Write-Warning "MSBuild not found (tried vswhere and a Visual Studio glob); skipping restore. Run manually before building."
} else {
    Write-Host "Restoring NuGet packages using $msbuild ..."
    Push-Location $TargetDir
    try {
        & $msbuild 'Rock.sln' -t:Restore -p:RestorePackagesConfig=true -p:Configuration=Debug -m -nologo -v:minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Restore exited with code $LASTEXITCODE - inspect output above and rerun manually if needed."
        }
    } finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host "Build tree ready: $TargetDir"
Write-Host "Open: $slnPath"
