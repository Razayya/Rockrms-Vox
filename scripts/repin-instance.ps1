<#
.SYNOPSIS
  Add or update an instance pin in overlay/instance-versions.json.

.DESCRIPTION
  Used by the rock-update skill when the user announces a Rock version on an instance.
  All inputs come from the user (System Information drawer + their knowledge of the
  deployed tag) — this script does not query SQL or otherwise validate the user's
  version statement. User input is king (see memory user-input-is-king-for-versions).

  Refuses to overwrite an existing instance unless -Force is passed (audit safety).

.PARAMETER Instance
  prod | dev | staging | ...

.PARAMETER Tag
  The SparkDev tag, e.g. 17.5.2.2 or 18.2.4.

.PARAMETER FriendlyVersion
  The 'Rock Version:' string from System Information, e.g. "Rock McKinley 17.5 (issue #6486)".

.PARAMETER FileVersion
  The file version in parentheses, e.g. "17.5.2.2".

.PARAMETER CloneDir
  Optional. Default: build/rock-source-<Instance>-<Tag>.

.PARAMETER Force
  Allow overwriting an existing pin for this instance.

.EXAMPLE
  .\scripts\repin-instance.ps1 -Instance prod -Tag 17.5.2.2 `
      -FriendlyVersion "Rock McKinley 17.5 (issue #6486)" -FileVersion 17.5.2.2
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)] [string]$Instance,
    [Parameter(Mandatory=$true)] [string]$Tag,
    [Parameter(Mandatory=$true)] [string]$FriendlyVersion,
    [Parameter(Mandatory=$true)] [string]$FileVersion,
    [string]$CloneDir,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel).Trim().Replace('/', '\')
$jsonPath = Join-Path $repoRoot 'overlay\instance-versions.json'

if (-not $CloneDir) { $CloneDir = "build/rock-source-$Instance-$Tag" }

# Load or create the JSON
if (Test-Path $jsonPath) {
    $json = Get-Content $jsonPath -Raw | ConvertFrom-Json
} else {
    $json = [pscustomobject]@{
        _schema   = "Vox Rock instance -> SparkDev version pointer. All fields are user-supplied (System Information drawer in Rock + the deployed SparkDev tag). No SQL validation."
        instances = New-Object psobject
    }
}
if (-not $json.instances) { $json | Add-Member -NotePropertyName instances -NotePropertyValue (New-Object psobject) }

$existing = $json.instances.$Instance
if ($existing -and -not $Force) {
    Write-Host "Existing pin for '$Instance':"
    $existing | ConvertTo-Json -Depth 5
    throw "Instance '$Instance' is already pinned. Pass -Force to overwrite (audit: capture the prior pin first)."
}

$entry = [pscustomobject]@{
    friendly_version = $FriendlyVersion
    file_version     = $FileVersion
    tag              = $Tag
    clone_dir        = $CloneDir
    pinned_at        = (Get-Date -Format 'yyyy-MM-dd')
    pinned_by        = 'user-supplied'
}

if ($json.instances.$Instance) {
    $json.instances.$Instance = $entry
} else {
    $json.instances | Add-Member -NotePropertyName $Instance -NotePropertyValue $entry
}

$json | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

Write-Host ''
Write-Host "Pinned $Instance -> tag $Tag"
Write-Host "Updated: $jsonPath"
Write-Host ''
Write-Host "Next:  .\scripts\prepare-build.ps1 -Instance $Instance"
