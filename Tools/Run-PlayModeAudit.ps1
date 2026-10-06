param(
    [string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.0.49f1/Editor/Unity.exe',
    [string]$Filter = 'NightShift.Tests.GameplayAuditTests'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$auditRoot = Join-Path ([IO.Path]::GetTempPath()) ('NightShiftPlayModeAudit-' + [guid]::NewGuid().ToString('N'))
$outputRoot = Join-Path $projectRoot ('Logs/PlayModeAudit-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $auditRoot, $outputRoot | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $folder) -Destination $auditRoot -Recurse
}
$settingsPath = Join-Path $auditRoot 'ProjectSettings/ProjectSettings.asset'
$settingsText = Get-Content -LiteralPath $settingsPath -Raw
if ($settingsText -notmatch '(?m)^  companyName:') { throw 'companyName missing; isolation cannot be established.' }
$settingsText = $settingsText -replace '(?m)^  companyName:.*$', '  companyName: NightShiftPlayModeAudit'
Set-Content -LiteralPath $settingsPath -Value $settingsText -Encoding utf8
Set-Content -LiteralPath (Join-Path $outputRoot 'project-copy.txt') -Value $auditRoot
$argumentList = @('-batchmode', '-projectPath', ('"' + $auditRoot + '"'), '-runTests', '-testPlatform', 'PlayMode',
    '-testFilter', ('"' + $Filter + '"'), '-testResults', ('"' + (Join-Path $outputRoot 'results.xml') + '"'),
    '-logFile', ('"' + (Join-Path $outputRoot 'unity.log') + '"'))
Write-Output "Isolated test project: $auditRoot"
Write-Output "Results: $outputRoot"
$process = Start-Process -FilePath $UnityPath -ArgumentList $argumentList -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(10000)) { Write-Output 'Unity PlayMode audit running...' }
if (Test-Path (Join-Path $auditRoot 'Logs/AuditConnection150.png')) {
    Copy-Item -LiteralPath (Join-Path $auditRoot 'Logs/AuditConnection150.png') -Destination $outputRoot
}
if (Test-Path (Join-Path $auditRoot 'Logs/AutoPlay')) {
    Copy-Item -LiteralPath (Join-Path $auditRoot 'Logs/AutoPlay') -Destination $outputRoot -Recurse
}
$resultFile = Join-Path $outputRoot 'results.xml'
if (-not (Test-Path $resultFile)) { throw "Unity produced no results.xml. Inspect $outputRoot/unity.log" }
[xml]$results = Get-Content -LiteralPath $resultFile
$results.'test-run' | Select-Object total, passed, failed, skipped, duration | Format-List
foreach ($test in $results.SelectNodes('//test-case[@result="Failed"]')) {
    Write-Output $test.name
    Write-Output $test.failure.InnerText
}
exit $process.ExitCode
