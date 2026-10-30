<#
.SYNOPSIS
Builds the mod, launches a host and a client DREDGE instance on this machine, runs the in-game
scenarios in CosmicHorrorFishingBuddies/Testing/Scenarios.cs, and exits non-zero on any failure.

.DESCRIPTION
Needs DREDGE with Winch installed. Your saves and settings are backed up before the run and
restored afterwards; the test uses its own save slot seeded from -Save (default: your slot 0).
Each run leaves logs and results in .test-runs/<timestamp>/.
#>
param(
	[string]$GameDir = $(if ($env:DREDGE_DIR) { $env:DREDGE_DIR } else { "${env:ProgramFiles(x86)}\Steam\steamapps\common\DREDGE" }),
	[string]$Save = "",
	[int]$Slot = 2,
	[int]$TimeoutSeconds = 480,
	[string]$Configuration = "Debug",
	[switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$dataDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Black Salt Games\DREDGE'
$savesDir = Join-Path $dataDir 'saves'
$modDir = Join-Path $GameDir 'Mods\xen.cosmichorrorfishingbuddies'
$patcherDir = Join-Path $GameDir 'Mods\xen.steamapipatcher'
$resultsDir = Join-Path $modDir 'test-results'
$runDir = Join-Path $repo ".test-runs\$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')"
$gameArgs = '-screen-fullscreen 0 -screen-width 960 -screen-height 540 skipintro'

if (Get-Process DREDGE -ErrorAction SilentlyContinue) { throw 'Close DREDGE first: the harness picks host/client by counting game processes.' }
if (-not (Test-Path (Join-Path $GameDir 'Winch.dll'))) { throw "Winch is not installed in $GameDir (https://github.com/DREDGE-Mods/Winch/releases)." }

if (-not $NoBuild) {
	# Windows PowerShell turns native stderr into terminating errors under 'Stop'
	$ErrorActionPreference = 'Continue'
	& dotnet build (Join-Path $repo 'CosmicHorrorFishingBuddies.sln') -c $Configuration --nologo -v quiet -clp:ErrorsOnly 2>&1 | Out-Host
	$ErrorActionPreference = 'Stop'
	if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

New-Item -ItemType Directory -Force $modDir, $patcherDir, $runDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $repo "CosmicHorrorFishingBuddies\bin\$Configuration\*") $modDir
Copy-Item -Recurse -Force (Join-Path $repo "SteamAPIPatcher\bin\$Configuration\*") $patcherDir

$saveBackup = Join-Path $runDir 'save-backup'
Copy-Item -Recurse $savesDir (Join-Path $saveBackup 'saves')
Get-ChildItem $dataDir -Filter 'dredge-settings.bin*' | Copy-Item -Destination $saveBackup
$optionsPath = Join-Path $modDir 'debug_options.json'
$originalOptions = if (Test-Path $optionsPath) { Get-Content -Raw $optionsPath } else { $null }

$processes = @()
$started = Get-Date
try {
	if (-not $Save) { $Save = Join-Path $savesDir 'dredge-save0.bin' }
	if (-not (Test-Path $Save)) { throw "No save to seed the test slot with: $Save" }
	Copy-Item -Force $Save (Join-Path $savesDir "dredge-save$Slot.bin")

	@{ DebugEnabled = $true; QuickLoad = $true; QuickLoadSlot = $Slot; TestMode = $true; Timeout = 25 } |
		ConvertTo-Json | Set-Content -Path $optionsPath -Encoding UTF8
	if (Test-Path $resultsDir) { Remove-Item -Recurse -Force $resultsDir }

	$deadline = $started.AddSeconds($TimeoutSeconds)
	function Wait-For([scriptblock]$condition, [string]$what) {
		while (-not (& $condition)) {
			if ((Get-Date) -gt $deadline) { throw "Timed out waiting for $what" }
			if ($processes | Where-Object HasExited) { throw "A game instance exited while waiting for $what" }
			Start-Sleep -Seconds 2
		}
	}

	Write-Host 'Starting host...'
	$processes += Start-Process (Join-Path $GameDir 'DREDGE.exe') $gameArgs -WorkingDirectory $GameDir -PassThru
	Wait-For { Test-Path (Join-Path $resultsDir 'Host.ready.txt') } 'the host to load in and start hosting'

	Write-Host 'Starting client...'
	$processes += Start-Process (Join-Path $GameDir 'DREDGE.exe') $gameArgs -WorkingDirectory $GameDir -PassThru

	function Read-Report($role) {
		$path = Join-Path $resultsDir "$role.json"
		if (Test-Path $path) { Get-Content -Raw $path | ConvertFrom-Json } else { $null }
	}
	while ((Get-Date) -lt $deadline) {
		$reports = 'Host', 'Client' | ForEach-Object { Read-Report $_ }
		if (($reports | Where-Object { $_ -and $_.finished }).Count -eq 2) { break }
		if (($processes | Where-Object HasExited).Count -eq 2) { break }
		Start-Sleep -Seconds 2
	}
}
finally {
	$processes | Where-Object { -not $_.HasExited } | ForEach-Object { Stop-Process -Id $_.Id -Force }
	Start-Sleep -Seconds 2

	$backedUp = Get-ChildItem (Join-Path $saveBackup 'saves') | ForEach-Object Name
	Get-ChildItem $savesDir | Where-Object { $backedUp -notcontains $_.Name } | Remove-Item -Force
	Copy-Item -Force (Join-Path $saveBackup 'saves\*') $savesDir
	Get-ChildItem $saveBackup -Filter 'dredge-settings.bin*' | Copy-Item -Force -Destination $dataDir
	if ($null -ne $originalOptions) { Set-Content -Path $optionsPath -Value $originalOptions -Encoding UTF8 -NoNewline } else { Remove-Item -Force $optionsPath }

	if (Test-Path $resultsDir) { Copy-Item -Recurse $resultsDir (Join-Path $runDir 'results') }
	Get-ChildItem (Join-Path $GameDir 'Logs') -Filter 'Winch-*.log' | Where-Object LastWriteTime -ge $started |
		Copy-Item -Destination $runDir
	Write-Host "Saves restored. Run artifacts: $runDir"
}

$failed = $false
foreach ($role in 'Host', 'Client') {
	$report = Read-Report $role
	if (-not $report) { Write-Host "[$role] no report written" -ForegroundColor Red; $failed = $true; continue }
	foreach ($r in $report.results) {
		$color = if ($r.passed) { 'Green' } else { 'Red' }
		$line = "[$role] $(if ($r.passed) { 'PASS' } else { 'FAIL' }) $($r.name) ($([math]::Round($r.seconds, 1))s)"
		if (-not $r.passed) { $line += "`n        $($r.error)"; $failed = $true }
		Write-Host $line -ForegroundColor $color
	}
	if (-not $report.finished) { Write-Host "[$role] did not finish all scenarios" -ForegroundColor Red; $failed = $true }
}
if ($failed) { exit 1 }
Write-Host 'All game tests passed.' -ForegroundColor Green
