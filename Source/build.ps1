param(
    [string]$GameDir = 'D:\Steam\steamapps\common\Apocalypter',
    [string]$OutputDir = (Join-Path $PSScriptRoot 'build')
)
$ErrorActionPreference = 'Stop'
$taskManaged = Join-Path $GameDir 'Apocalypter_Data\Managed'
$taskCore = Join-Path $GameDir 'BepInEx\core'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$taskReferences = @('mscorlib','System','System.Core','netstandard','Assembly-CSharp','UnityEngine','UnityEngine.CoreModule',
    'UnityEngine.PhysicsModule','UnityEngine.TerrainModule','UnityEngine.InputLegacyModule',
    'UnityEngine.UIModule','UnityEngine.TextRenderingModule','UnityEngine.UI','PlayMaker','NWH.VehiclePhysics2','NWH.Common') |
    ForEach-Object { Join-Path $taskManaged ($_ + '.dll') }
$taskReferences += @((Join-Path $taskCore 'BepInEx.dll'), (Join-Path $taskCore '0Harmony.dll'),
    (Join-Path $GameDir 'BepInEx\plugins\Apocasetter\Apocasetter.dll'))
foreach ($taskReference in $taskReferences) {
    if (!(Test-Path -LiteralPath $taskReference)) { throw "Missing build reference: $taskReference" }
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$taskArgs = @('/nologo','/noconfig','/nostdlib+','/target:library','/langversion:5','/optimize+','/warn:4',
    ('/out:' + (Join-Path $OutputDir 'ApocaChaseCamera.dll')))
$taskArgs += $taskReferences | ForEach-Object { '/reference:' + $_ }
$taskArgs += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName }
& $taskCompiler @taskArgs
if ($LASTEXITCODE -ne 0) { throw "Mod compilation failed with exit code $LASTEXITCODE" }
Write-Output ('Built ' + (Join-Path $OutputDir 'ApocaChaseCamera.dll'))
