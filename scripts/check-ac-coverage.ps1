<#
.SYNOPSIS
  Story-to-test traceability gate (foundation section 14.3): every acceptance criterion of the 13 stories owned by BC-03
  must have at least one test tagged [Trait("Story","US-...")] + [Trait("AC","AC-nn")].

.DESCRIPTION
  Reads the AC ids from the story markdown files (**AC-nn - ...** headings), scans tests/**/*.cs for Trait pairs that sit on the
  same test method, and fails (exit code 1) when a criterion has no test. Also lists tags that point at unknown ACs.
  Falls back to scripts/owned-story-acs.json (a snapshot of the story files) when the stories folder is not present.

.EXAMPLE
  pwsh scripts/check-ac-coverage.ps1
  pwsh scripts/check-ac-coverage.ps1 -StoriesDir "D:\Akash Personal\AiBootCamp\Pipeline-Output\stories"
#>
param(
    [string]$StoriesDir = (Join-Path $PSScriptRoot '..\..\Pipeline-Output\stories'),
    [string]$TestsDir = (Join-Path $PSScriptRoot '..\tests'),
    [string]$SnapshotFile = (Join-Path $PSScriptRoot 'owned-story-acs.json')
)

$ownedStories = 'US-3.1.1-01', 'US-3.1.1-02', 'US-3.1.2-01', 'US-3.1.2-02', 'US-3.1.3-01', 'US-3.1.3-02', 'US-3.1.4-03',
    'US-3.1.5-01', 'US-3.1.5-02', 'US-3.1.5-03', 'US-3.1.5-04', 'US-3.4.3-04', 'US-4.1-04'

# ---- required criteria
$required = @{}
if (Test-Path $StoriesDir) {
    foreach ($story in $ownedStories) {
        $file = Get-ChildItem -Path $StoriesDir -Filter "$story-*.md" | Select-Object -First 1
        if (-not $file) { Write-Error "Story file for $story not found in $StoriesDir"; exit 2 }
        $required[$story] = @(Select-String -Path $file.FullName -Pattern '^\*\*(AC-\d+)' | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique)
    }
    Write-Host "Read acceptance criteria from $StoriesDir"
}
elseif (Test-Path $SnapshotFile) {
    $json = Get-Content $SnapshotFile -Raw | ConvertFrom-Json
    foreach ($story in $ownedStories) { $required[$story] = @($json.$story) }
    Write-Host "Stories folder not found; using snapshot $SnapshotFile"
}
else { Write-Error 'Neither the stories folder nor the snapshot file exists.'; exit 2 }

# ---- tagged tests: Trait("Story", ...) and Trait("AC", ...) in the same attribute block above a test method
$covered = @{}
Get-ChildItem -Path $TestsDir -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | ForEach-Object {
    $stories = @(); $acs = @()
    foreach ($line in Get-Content $_.FullName) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^\[Trait\("Story",\s*"([^"]+)"\)\]') { $stories += $Matches[1]; continue }
        if ($trimmed -match '^\[Trait\("AC",\s*"([^"]+)"\)\]') { $acs += $Matches[1]; continue }
        if ($trimmed.StartsWith('[')) { continue }
        if ($trimmed -match '^public\s+(async\s+)?(Task|void)\s+\w+\(') {
            foreach ($s in $stories) { foreach ($a in $acs) { $covered["$s|$a"] = ($covered["$s|$a"] + 1) } }
        }
        if ($trimmed -ne '') { $stories = @(); $acs = @() }
    }
}

$missing = @()
foreach ($story in $ownedStories) {
    foreach ($ac in $required[$story]) {
        if (-not $covered.ContainsKey("$story|$ac")) { $missing += "$story $ac" }
    }
}
$unknown = @($covered.Keys | Where-Object { $s, $a = $_ -split '\|'; -not ($required.ContainsKey($s) -and ($required[$s] -contains $a)) })

$total = ($required.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
Write-Host ("Acceptance criteria: {0}, covered: {1}, missing: {2}" -f $total, ($total - $missing.Count), $missing.Count)
if ($unknown.Count -gt 0) { Write-Warning ("Tags that match no acceptance criterion: " + ($unknown -join ', ')) }
if ($missing.Count -gt 0) {
    $missing | ForEach-Object { Write-Host "  MISSING  $_" }
    exit 1
}
Write-Host 'Every acceptance criterion of the owned stories has at least one tagged test.'
exit 0
