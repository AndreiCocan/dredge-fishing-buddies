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
	[int]$Clients = 1,
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
$roles = @('Host') + (1..$Clients | ForEach-Object { "Client$_" })

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
$winchConfigPath = Join-Path $GameDir 'WinchConfig.json'
$originalWinchConfig = Get-Content -Raw $winchConfigPath

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

	# Each game inherits these at launch: its own role, and the roster it waits for at every barrier
	$env:CFB_TEST_PLAYERS = $roles -join ','
	foreach ($role in $roles) {
		Write-Host "Starting $role..."
		$env:CFB_TEST_ROLE = $role
		# Winch names logs by the minute, so games started in the same minute fought over one file and one crashed.
		# Winch reads its config once at startup, so each game gets its own log folder.
		$logFolder = "Logs\test-$role"
		Remove-Item -Recurse -Force (Join-Path $GameDir $logFolder) -ErrorAction SilentlyContinue
		New-Item -ItemType Directory -Force (Join-Path $GameDir $logFolder) | Out-Null
		$winchConfig = $originalWinchConfig | ConvertFrom-Json
		$winchConfig.LogsFolder = $logFolder
		$winchConfig | ConvertTo-Json | Set-Content -Path $winchConfigPath -Encoding UTF8
		$processes += Start-Process (Join-Path $GameDir 'DREDGE.exe') $gameArgs -WorkingDirectory $GameDir -PassThru
		# Wait until the game has read the config before pointing it at the next game's folder
		Wait-For { Get-ChildItem (Join-Path $GameDir $logFolder) -Filter 'Winch-*.log' } "$role to start logging"
		Wait-For { Test-Path (Join-Path $resultsDir "$role.ready.txt") } "$role to load in and connect"
	}
	Remove-Item Env:CFB_TEST_ROLE, Env:CFB_TEST_PLAYERS

	function Read-Report($role) {
		$path = Join-Path $resultsDir "$role.json"
		# The game replaces this file while we poll; a failed read just means not ready yet
		try { Get-Content -Raw -ErrorAction Stop $path | ConvertFrom-Json } catch { $null }
	}
	while ((Get-Date) -lt $deadline) {
		$reports = $roles | ForEach-Object { Read-Report $_ }
		if (($reports | Where-Object { $_ -and $_.finished }).Count -eq $roles.Count) { break }
		if (($processes | Where-Object HasExited).Count -eq $roles.Count) { break }
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
	Set-Content -Path $winchConfigPath -Value $originalWinchConfig -Encoding UTF8 -NoNewline

	if (Test-Path $resultsDir) { Copy-Item -Recurse $resultsDir (Join-Path $runDir 'results') }
	foreach ($role in $roles) {
		Get-ChildItem (Join-Path $GameDir "Logs\test-$role") -Filter 'Winch-*.log' -ErrorAction SilentlyContinue |
			ForEach-Object { Copy-Item $_.FullName (Join-Path $runDir "$role.log") }
	}
	Write-Host "Saves restored. Run artifacts: $runDir"
}

$failed = $false
foreach ($role in $roles) {
	$report = Read-Report $role
	if (-not $report) { Write-Host "[$role] no report written" -ForegroundColor Red; $failed = $true; continue }
	foreach ($r in $report.results) {
		$status = if ($r.skipped) { 'SKIP' } elseif ($r.passed) { 'PASS' } else { 'FAIL' }
		$color = if ($r.skipped) { 'DarkGray' } elseif ($r.passed) { 'Green' } else { 'Red' }
		$line = "[$role] $status $($r.name) ($([math]::Round($r.seconds, 1))s)"
		if (-not $r.passed) { $line += "`n        $($r.error)"; $failed = $true }
		Write-Host $line -ForegroundColor $color
	}
	if (-not $report.finished) { Write-Host "[$role] did not finish all scenarios" -ForegroundColor Red; $failed = $true }
	$traffic = Join-Path $resultsDir "$role.traffic.txt"
	if (Test-Path $traffic) { Write-Host "[$role] traffic: $(Get-Content -Raw $traffic)" -ForegroundColor DarkGray }
}
if ($failed) { exit 1 }
Write-Host 'All game tests passed.' -ForegroundColor Green
