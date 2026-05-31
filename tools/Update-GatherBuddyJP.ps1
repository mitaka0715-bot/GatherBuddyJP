param(
    [switch]$Apply,
    [switch]$Build,
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$WorkRoot = Split-Path $RepoRoot -Parent
$ManagementRoot = Split-Path $WorkRoot -Parent
$LogDir = Join-Path $ManagementRoot "04_log"
$DeployDir = Join-Path $ManagementRoot "00_release\DalamudPlugin"
$StatusPath = Join-Path $LogDir "upstream_status.json"
$Branch = "jp-localization"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null

Set-Location $RepoRoot

$currentBranch = git branch --show-current
if ($currentBranch -ne $Branch) {
    git checkout $Branch
}

git fetch upstream main --tags

$head = git rev-parse HEAD
$upstream = git rev-parse upstream/main
$counts = (git rev-list --left-right --count HEAD...upstream/main) -split "\s+"
$ahead = [int]$counts[0]
$behind = [int]$counts[1]
$hasUpdate = $behind -gt 0

$status = [ordered]@{
    checkedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    branch = $Branch
    head = $head
    upstream = $upstream
    ahead = $ahead
    behind = $behind
    hasUpdate = $hasUpdate
    applied = $false
    built = $false
    deployed = $false
}

if ($Apply -and $hasUpdate) {
    git merge --no-edit upstream/main
    $status.applied = $true
    $status.head = git rev-parse HEAD
}

if ($Build) {
    $packageDir = Join-Path $RepoRoot "GatherBuddy\bin\Release\GatherBuddyJP"
    if (Test-Path -LiteralPath $packageDir) {
        Remove-Item -LiteralPath $packageDir -Recurse -Force
    }

    dotnet build .\GatherBuddy\GatherBuddy.csproj -c Release
    $status.built = $true
}

if ($Deploy) {
    $outputDir = Join-Path $RepoRoot "GatherBuddy\bin\Release"
    $dll = Join-Path $outputDir "GatherBuddyJP.dll"
    if (!(Test-Path $dll)) {
        throw "GatherBuddyJP.dll not found. Run this script with -Build first."
    }

    Get-ChildItem -LiteralPath $outputDir -File |
        Where-Object { $_.Extension -in ".dll", ".json", ".pdb" } |
        Copy-Item -Destination $DeployDir -Force
    $status.deployed = $true
}

$status | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $StatusPath

if ($hasUpdate) {
    Write-Host "Upstream update available: behind=$behind"
} else {
    Write-Host "No upstream update."
}

Write-Host "Status log: $StatusPath"
if ($Deploy) {
    Write-Host "Deploy dir: $DeployDir"
}
