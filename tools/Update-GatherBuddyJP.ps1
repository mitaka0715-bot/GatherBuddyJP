param(
    [switch]$Apply,
    [switch]$Build,
    [switch]$Deploy,
    [switch]$Publish,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$WorkRoot = Split-Path $RepoRoot -Parent
$ManagementRoot = Split-Path $WorkRoot -Parent
$LogDir = Join-Path $ManagementRoot ([string]::Concat("04_", [char]0x30ED, [char]0x30B0))
$OfficialDirName = [string]::Concat("00_", [char]0x6B63, [char]0x898F, "EXE")
$OfficialDir = Join-Path $ManagementRoot (Join-Path $OfficialDirName "DalamudPlugin")
$DevDir = "C:\DalamudDevPlugins\GatherBuddyJP"
$StatusPath = Join-Path $LogDir "upstream_status.json"
$RunLogPath = Join-Path $LogDir ("upstream_update_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$Branch = "jp-localization"
$OriginBranch = "main"
$InternalName = "GatherBuddyJP"
$PluginName = "GatherBuddy JP"
$Author = "General Headquarters"
$RepoUrl = "https://github.com/mitaka0715-bot/GatherBuddyJP"
$IconUrl = "https://raw.githubusercontent.com/mitaka0715-bot/GatherBuddyJP/main/images/icon.png?v=20260601"
$ZipUrl = "https://raw.githubusercontent.com/mitaka0715-bot/GatherBuddyJP/main/latest.zip"
$Punchline = "Miner and botanist focused GatherBuddy JP fork."
$Description = "A Japanese-localized miner and botanist focused fork based on GatherBuddy Reborn. Supports gatherable item search, auto-gather lists, teleport-assisted movement, and vnavmesh navigation. Fishing UI entry points are hidden in this build."

New-Item -ItemType Directory -Force -Path $LogDir, $OfficialDir, $DevDir | Out-Null

function Write-RunLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    $line | Tee-Object -FilePath $RunLogPath -Append
}

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    Write-RunLog ("git " + ($Args -join " "))
    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Save-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Text
    )
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

function Set-JsonMetadata {
    param([string]$Path)
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    $json.Author = $Author
    $json.Name = $PluginName
    $json.InternalName = $InternalName
    $json.RepoUrl = $RepoUrl
    $json.Description = $Description
    $json.Punchline = $Punchline
    $json.IconUrl = $IconUrl
    $json.AcceptsFeedback = $true
    if ($null -eq $json.TestingDalamudApiLevel -and $null -ne $json.DalamudApiLevel) {
        $json | Add-Member -NotePropertyName TestingDalamudApiLevel -NotePropertyValue $json.DalamudApiLevel
    }
    Save-Utf8NoBom -Path $Path -Text (($json | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
}

function Ensure-JpMetadata {
    Set-JsonMetadata (Join-Path $RepoRoot "manifest.json")
    Set-JsonMetadata (Join-Path $RepoRoot "GatherBuddy\GatherBuddyReborn.json")

    $csprojPath = Join-Path $RepoRoot "GatherBuddy\GatherBuddy.csproj"
    $csproj = Get-Content -LiteralPath $csprojPath -Raw
    $csproj = [regex]::Replace($csproj, "<AssemblyName>.*?</AssemblyName>", "<AssemblyName>$InternalName</AssemblyName>")
    $csproj = [regex]::Replace($csproj, "<Product>.*?</Product>", "<Product>$PluginName</Product>")
    $csproj = [regex]::Replace($csproj, "<Name>.*?</Name>", "<Name>$PluginName</Name>")
    $csproj = [regex]::Replace($csproj, "<Author>.*?</Author>", "<Author>$Author</Author>")
    $csproj = [regex]::Replace($csproj, "<Description>.*?</Description>", "<Description>$Description</Description>")
    $csproj = [regex]::Replace($csproj, "<Punchline>.*?</Punchline>", "<Punchline>$Punchline</Punchline>")
    Save-Utf8NoBom -Path $csprojPath -Text $csproj
}

function Update-RepoJson {
    $manifestPath = Join-Path $RepoRoot "manifest.json"
    $repoPath = Join-Path $RepoRoot "repo.json"
    $csprojPath = Join-Path $RepoRoot "GatherBuddy\GatherBuddy.csproj"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $csprojXml = [xml](Get-Content -LiteralPath $csprojPath -Raw)
    $assemblyVersion = $manifest.AssemblyVersion
    if ([string]::IsNullOrWhiteSpace($assemblyVersion)) {
        $assemblyVersion = $csprojXml.Project.PropertyGroup.Version | Select-Object -First 1
    }
    $entry = [ordered]@{
        Author = $Author
        Name = $PluginName
        InternalName = $InternalName
        AssemblyVersion = $assemblyVersion
        TestingAssemblyVersion = $null
        RepoUrl = $RepoUrl
        ApplicableVersion = $manifest.ApplicableVersion
        DalamudApiLevel = $manifest.DalamudApiLevel
        TestingDalamudApiLevel = $manifest.TestingDalamudApiLevel
        Punchline = $Punchline
        Description = $Description
        Tags = $manifest.Tags
        CategoryTags = $manifest.CategoryTags
        IsHide = $false
        IsTestingExclusive = $false
        DownloadCount = 0
        DownloadLinkInstall = $ZipUrl
        DownloadLinkUpdate = $ZipUrl
        DownloadLinkTesting = $ZipUrl
        LastUpdate = [DateTimeOffset]::Now.ToUnixTimeSeconds()
        IconUrl = $IconUrl
        AcceptsFeedback = $true
    }
    Save-Utf8NoBom -Path $repoPath -Text ((ConvertTo-Json -InputObject @($entry) -Depth 8) + [Environment]::NewLine)
}

function New-CleanPluginZip {
    $releaseDir = Join-Path $RepoRoot "release"
    $zipPath = Join-Path $RepoRoot "latest.zip"
    $packagedDir = Join-Path $RepoRoot "GatherBuddy\bin\Release\$InternalName"

    if (Test-Path -LiteralPath $releaseDir) {
        Remove-Item -LiteralPath $releaseDir -Recurse -Force
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

    $packagedZip = Join-Path $packagedDir "latest.zip"
    $packagedManifest = Join-Path $packagedDir "$InternalName.json"
    if (!(Test-Path -LiteralPath $packagedZip) -or !(Test-Path -LiteralPath $packagedManifest)) {
        throw "DalamudPackager output not found in $packagedDir"
    }

    Copy-Item -LiteralPath $packagedZip -Destination $zipPath -Force
    Copy-Item -LiteralPath $packagedManifest -Destination (Join-Path $releaseDir "$InternalName.json") -Force
    $outputDir = Join-Path $RepoRoot "GatherBuddy\bin\Release"
    Get-ChildItem -LiteralPath $outputDir -File |
        Where-Object { $_.Extension -in ".dll", ".json", ".pdb" -or $_.Name -eq "$InternalName.deps.json" } |
        Copy-Item -Destination $releaseDir -Force

    Copy-Item -LiteralPath (Join-Path $RepoRoot "README.md") -Destination (Join-Path $releaseDir "README.md") -Force
    Copy-Item -LiteralPath (Join-Path $RepoRoot "images\icon.png") -Destination (Join-Path $releaseDir "icon.png") -Force
}

function Deploy-Plugin {
    $releaseDir = Join-Path $RepoRoot "release"
    foreach ($dir in @($OfficialDir, $DevDir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        Get-ChildItem -LiteralPath $releaseDir -File | Copy-Item -Destination $dir -Force
        Copy-Item -LiteralPath (Join-Path $RepoRoot "latest.zip") -Destination (Join-Path $dir "latest.zip") -Force
        Copy-Item -LiteralPath (Join-Path $RepoRoot "repo.json") -Destination (Join-Path $dir "repo.json") -Force
    }
}

function Test-PublicFiles {
    $forbiddenPattern = ("Black" + "Ash|{0}|{1}|{2}" -f [char]0x8B17, [char]0x8768, [char]0xFFE0)
    $forbidden = rg -n $forbiddenPattern -S README.md repo.json manifest.json "GatherBuddy\GatherBuddyReborn.json" "GatherBuddy\GatherBuddy.csproj" images 2>$null
    if ($LASTEXITCODE -eq 0) {
        throw "Forbidden or mojibake text found:`n$forbidden"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Join-Path $RepoRoot "latest.zip"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    $entries = $archive.Entries
    try {
        if ($entries.FullName -contains "latest.zip" -or ($entries.FullName | Where-Object { $_ -like "*\latest.zip" })) {
            throw "latest.zip contains a nested latest.zip"
        }
        if (-not ($entries.FullName -contains "$InternalName.dll")) {
            throw "latest.zip does not contain $InternalName.dll at the root"
        }
        if (-not ($entries.FullName -contains "$InternalName.json")) {
            throw "latest.zip does not contain $InternalName.json at the root"
        }
    } finally {
        $archive.Dispose()
    }
}

$status = [ordered]@{
    checkedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    branch = $Branch
    head = $null
    upstream = $null
    ahead = 0
    behind = 0
    hasUpdate = $false
    applied = $false
    built = $false
    deployed = $false
    published = $false
    result = "started"
    log = $RunLogPath
}

try {
    Set-Location $RepoRoot
    $currentBranch = git branch --show-current
    if ($currentBranch -ne $Branch) {
        Invoke-Git checkout $Branch
    }

    $dirty = git status --porcelain
    if ($dirty -and -not $Force) {
        throw "Working tree has local changes. Commit them or rerun with -Force."
    }

    Invoke-Git fetch upstream main --tags
    Invoke-Git fetch origin $OriginBranch

    $status.head = git rev-parse HEAD
    $status.upstream = git rev-parse upstream/main
    $counts = (git rev-list --left-right --count HEAD...upstream/main) -split "\s+"
    $status.ahead = [int]$counts[0]
    $status.behind = [int]$counts[1]
    $status.hasUpdate = $status.behind -gt 0

    if ($Apply -and ($status.hasUpdate -or $Force)) {
        Invoke-Git merge --no-edit upstream/main
        Ensure-JpMetadata
        $status.applied = $true
        $status.head = git rev-parse HEAD
    }

    if ($Build -and ($status.hasUpdate -or $Force -or $status.applied)) {
        dotnet build .\GatherBuddy\GatherBuddy.csproj -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
        Ensure-JpMetadata
        Update-RepoJson
        New-CleanPluginZip
        Test-PublicFiles
        $status.built = $true
    }

    if ($Deploy -and ($status.built -or $Force)) {
        Deploy-Plugin
        $status.deployed = $true
    }

    if ($Publish -and ($status.built -or $status.applied -or $Force)) {
        $changes = git status --porcelain
        if ($changes) {
            Invoke-Git add manifest.json repo.json latest.zip README.md images/icon.png GatherBuddy/GatherBuddy.csproj GatherBuddy/GatherBuddyReborn.json tools/Update-GatherBuddyJP.ps1
            $shortUpstream = (git rev-parse --short upstream/main)
            Invoke-Git commit -m "Update GatherBuddy JP for upstream $shortUpstream"
        }
        Invoke-Git push origin HEAD:$OriginBranch
        $status.published = $true
    }

    if (-not $status.hasUpdate -and -not $Force) {
        Write-RunLog "No upstream update."
    }

    $status.result = "success"
} catch {
    $status.result = "failed"
    $status.error = $_.Exception.Message
    Write-RunLog ("FAILED: " + $_.Exception.Message)
    throw
} finally {
    Save-Utf8NoBom -Path $StatusPath -Text (($status | ConvertTo-Json -Depth 5) + [Environment]::NewLine)
    Write-RunLog "Status log: $StatusPath"
}
