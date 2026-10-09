param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$OutputFile = (Join-Path $PSScriptRoot '..\ApocaChaseCamera-build-references.zip')
)
$ErrorActionPreference = 'Stop'
$taskManaged = Join-Path $GameDir 'Apocalypter_Data\Managed'
$taskCore = Join-Path $GameDir 'BepInEx\core'
$taskReferences = @('mscorlib','System','System.Core','netstandard','Assembly-CSharp','UnityEngine','UnityEngine.CoreModule',
    'UnityEngine.PhysicsModule','UnityEngine.TerrainModule','UnityEngine.TerrainPhysicsModule','UnityEngine.InputLegacyModule',
    'UnityEngine.UIModule','UnityEngine.TextRenderingModule','UnityEngine.UI','PlayMaker','NWH.VehiclePhysics2','NWH.Common') |
    ForEach-Object { Join-Path $taskManaged ($_ + '.dll') }
$taskReferences += @((Join-Path $taskCore 'BepInEx.dll'), (Join-Path $taskCore '0Harmony.dll'),
    (Join-Path $taskCore 'MonoMod.Utils.dll'), (Join-Path $taskCore 'Mono.Cecil.dll'),
    (Join-Path $GameDir 'BepInEx\plugins\Apocasetter\Apocasetter.dll'))
foreach ($taskReference in $taskReferences) {
    if (!(Test-Path -LiteralPath $taskReference)) { throw "Missing build reference: $taskReference" }
}
if (Test-Path -LiteralPath $OutputFile) { throw "Output already exists: $OutputFile" }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip = [IO.Compression.ZipFile]::Open([IO.Path]::GetFullPath($OutputFile), [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskReference in $taskReferences) {
        $taskRelative = $taskReference.Substring($GameDir.TrimEnd([char[]]@('\', '/')).Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip, $taskReference, $taskRelative) | Out-Null
    }
} finally { $taskZip.Dispose() }
Write-Output ('Collected only the build reference DLLs in ' + [IO.Path]::GetFullPath($OutputFile))
