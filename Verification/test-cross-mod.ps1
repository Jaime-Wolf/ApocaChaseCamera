param(
    [Parameter(Mandatory = $true)][string]$ApocaplayerSource,
    [string]$GameDir = 'D:\Steam\steamapps\common\Apocalypter',
    [string]$CameraSource = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Source'),
    [ValidateSet('', 'holder', 'discovery')][string]$Scenario = ''
)
$ErrorActionPreference = 'Stop'
$taskExternal = Join-Path $ApocaplayerSource 'ThirdPerson.cs'
if (!(Test-Path -LiteralPath $taskExternal)) { throw 'Supply a separate, unchanged Apocaplayer source directory.' }
$taskExternalHash = (Get-FileHash -LiteralPath $taskExternal).Hash
$taskSource = $CameraSource
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskCore = Join-Path $GameDir 'BepInEx\core'
$taskTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$taskWork = Join-Path $taskTempRoot ('ApocaChaseCamera-cross-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskWork | Out-Null
try {
    # Use the installed Harmony and its dependencies, privately for this run.
    # They are never placed in the mod's source or installation archives.
    Get-ChildItem -LiteralPath $taskCore -Filter '*.dll' -File | Copy-Item -Destination $taskWork
    $taskExe = Join-Path $taskWork 'CrossMod.exe'
    $taskArgs = @('/nologo','/target:exe','/langversion:5','/optimize-','/warn:4',
        '/define:REAL_APOCAPLAYER;REAL_HARMONY',('/out:' + $taskExe),
        ('/reference:' + (Join-Path $taskWork '0Harmony.dll')),
        ('/reference:' + (Join-Path $taskWork 'MonoMod.Utils.dll')),
        ('/reference:' + (Join-Path $taskWork 'Mono.Cecil.dll')),
        '/reference:System.Numerics.dll','/reference:System.Numerics.Vectors.dll')
    $taskArgs += @('Stubs.cs','RealPlayerStubs.cs','RealPlayerChecks.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    $taskArgs += Get-ChildItem -LiteralPath $taskSource -Filter '*.cs' -File | Where-Object Name -ne 'Plugin.cs' | ForEach-Object FullName
    $taskArgs += $taskExternal
    & $taskCompiler @taskArgs
    if ($LASTEXITCODE -ne 0) { throw 'Cross-mod test compilation failed.' }
    & $taskExe $Scenario
    if ($LASTEXITCODE -ne 0) { throw 'Cross-mod verification failed.' }
    if ((Get-FileHash -LiteralPath $taskExternal).Hash -ne $taskExternalHash) { throw 'External source changed during verification.' }
} finally {
    $taskResolved = [IO.Path]::GetFullPath($taskWork)
    if ($taskResolved.StartsWith((Join-Path $taskTempRoot 'ApocaChaseCamera-cross-'), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
}
