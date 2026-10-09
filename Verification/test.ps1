param([string]$FixtureJson = (Join-Path $PSScriptRoot '..\..\camera-research\camera-fixtures.json'))
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $FixtureJson) {
$taskRows = Get-Content -LiteralPath $FixtureJson -Raw | ConvertFrom-Json
$taskFixtures = @($taskRows | Where-Object { $_.asset -eq 'sharedassets1.assets' -and $_.path -match '^[^/]+/DriveTrigger/3rdCamera$' })
$taskLines = foreach ($taskFixture in $taskFixtures) {
    $taskVehicle = $taskFixture.path.Split('/')[0]
    $taskTrigger = $taskRows | Where-Object { $_.asset -eq 'sharedassets1.assets' -and $_.path -eq ($taskVehicle + '/DriveTrigger') }
    $taskCamera = $taskRows | Where-Object { $_.asset -eq 'sharedassets1.assets' -and $_.path -eq ($taskFixture.path + '/Camera') }
    if (!($taskCamera.components | Where-Object {$_.class -eq 20})) {throw "Missing real camera in $taskVehicle fixture"}
    if (!($taskTrigger.components | Where-Object {$_.fsm -eq 'Camera'}) -or !($taskTrigger.components | Where-Object {$_.fsm -eq 'Drive'})) {throw "Missing real drive FSMs in $taskVehicle fixture"}
    $taskNumbers = @($taskTrigger.pos) + @($taskFixture.pos)
    $taskVehicle + ',' + (($taskNumbers | ForEach-Object {([double]$_).ToString('R',[Globalization.CultureInfo]::InvariantCulture)}) -join ',')
}
$taskFixtureCsv = Join-Path $PSScriptRoot 'prefab-anchors.csv'
Set-Content -LiteralPath $taskFixtureCsv -Value $taskLines -Encoding Ascii
} else {
    $taskFixtureCsv = Join-Path $PSScriptRoot 'prefab-anchors.csv'
    if (!(Test-Path -LiteralPath $taskFixtureCsv)) {throw 'Camera anchor fixture was not found'}
}
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskSource = Split-Path $PSScriptRoot -Parent
if (Test-Path -LiteralPath (Join-Path $taskSource 'Source\CameraMath.cs')) { $taskSource = Join-Path $taskSource 'Source' }
$taskExe = Join-Path ([IO.Path]::GetTempPath()) ('ApocaChaseCamera-checks-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
$taskCheckArgs = @('/nologo','/target:exe','/langversion:5','/warn:4',('/out:' + $taskExe))
$taskCheckArgs += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName }
$taskCheckArgs += @('CameraQueries.cs','CameraMath.cs','CameraBinding.cs','ChaseView.cs','ApocaplayerBridge.cs','HudMath.cs','DrivingReadings.cs','DrivingHud.cs','GaugeFace.cs') | ForEach-Object { Join-Path $taskSource $_ }
& $taskCompiler @taskCheckArgs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $taskExe $taskFixtureCsv
if ($LASTEXITCODE -ne 0) { throw 'Camera verification failed' }
} finally { if (Test-Path -LiteralPath $taskExe) { Remove-Item -LiteralPath $taskExe -Force } }


