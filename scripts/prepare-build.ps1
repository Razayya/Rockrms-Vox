<#
.SYNOPSIS
  Build the SparkDev Rock tree pinned for an instance, overlay Vox plugins, inject sln entries.

.DESCRIPTION
  Reads overlay/instance-versions.json for the named instance, shallow-clones
  SparkDevNetwork/Rock at instances.<Instance>.tag into instances.<Instance>.clone_dir,
  junctions every directory under plugins/ into the clone, then derives Project /
  ProjectConfigurationPlatforms / RockWeb-ProjectReferences sln entries from each
  plugin's csproj <ProjectGuid> and injects them into the upstream Rock.sln.

  Junctions (not symlinks) avoid the Windows Developer Mode / admin requirement.
  Edits in plugins/ are live in the cloned Rock tree.

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
        Get-ChildItem $TargetDir -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
            $_.Attributes -band [IO.FileAttributes]::ReparsePoint
        } | ForEach-Object { [IO.Directory]::Delete($_.FullName, $false) }
        Remove-Item -Recurse -Force $TargetDir
    } else {
        Write-Host "Re-using existing clone at $TargetDir (pass -Force to re-clone)."
    }
}

if (-not (Test-Path $TargetDir)) {
    $parent = Split-Path $TargetDir -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    Write-Host "Cloning $RockRepo @ $tag (shallow) ..."
    & git clone --depth 1 --branch $tag $RockRepo $TargetDir
    if ($LASTEXITCODE -ne 0) { throw "git clone failed for tag '$tag'. Confirm the tag exists upstream." }
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
$msbuild = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $msbuild) {
    Write-Warning "MSBuild not found under VS 2022; skipping restore. Run manually before building."
} else {
    Write-Host "Restoring NuGet packages (PackageReference + packages.config) ..."
    Push-Location $TargetDir
    try {
        & $msbuild.FullName 'Rock.sln' -t:Restore -p:RestorePackagesConfig=true -p:Configuration=Debug -m -nologo -v:minimal
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
