# Run one editor method in batch mode and show the lines of the log that matter.
#   .\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeProjectSetup.Run
#   .\tools\unity_batch.ps1 -Method X.Y.Z -Name capture -Extra '-roeOut','E:\x' -Graphics
# -Graphics keeps the GPU (needed to render); without it the editor starts with -nographics.
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$Name = '',
    [string[]]$Extra = @(),
    [switch]$Graphics,
    [int]$TimeoutMinutes = 60
)

$unity = 'E:\tools\Unity\Hub\Editor\6000.4.12f1\Editor\Unity.exe'
$project = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $project '_work\logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
if (-not $Name) { $Name = ($Method -split '\.')[-2] + '_' + ($Method -split '\.')[-1] }
$log = Join-Path $logDir ("{0}_{1}.log" -f (Get-Date -Format 'MMdd_HHmmss'), $Name)

$lock = Join-Path $project 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lock) {
    # the Hub runs a helper that is also called "unity"; only the editor binary counts
    $running = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $unity }
    if ($running) { Write-Output "Unity is already running (pid $($running.Id -join ', ')) - the project may be open; aborting."; exit 3 }
}

$unityArgs = @('-batchmode', '-projectPath', $project, '-executeMethod', $Method, '-quit', '-logFile', $log)
if (-not $Graphics) { $unityArgs += '-nographics' }
$unityArgs += $Extra

$t0 = Get-Date
$p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
if (-not $p.WaitForExit($TimeoutMinutes * 60000)) { $p.Kill(); Write-Output "TIMEOUT after $TimeoutMinutes min - killed" }
$secs = [int]((Get-Date) - $t0).TotalSeconds
Write-Output "exit code $($p.ExitCode) after $secs s, log $log"

$pattern = '\[ROE\]|error CS\d+|warning CS\d+.*RoeFighter|Shader error|Shader warning in ''ROE|Exception:|Aborting batchmode|executeMethod class|Scripts have compiler errors|No valid Unity Editor license|Assertion failed|Crash!!!'
Select-String -LiteralPath $log -Pattern $pattern | ForEach-Object { $_.Line } | Select-Object -Unique -First 80 |
    ForEach-Object { if ($_.Length -gt 300) { $_.Substring(0, 300) } else { $_ } }
exit $p.ExitCode
