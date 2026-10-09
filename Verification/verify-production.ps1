param(
    [Parameter(Mandatory = $true)][string]$Dll,
    [string]$GameDir = 'D:\Steam\steamapps\common\Apocalypter',
    [string]$ExpectedVersion = '0.2.4'
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$taskPlayer = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'BepInEx\plugins\Apocaplayer\Apocaplayer.dll'))
$taskMod = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($Dll))
$taskGame = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Apocalypter_Data\Managed\Assembly-CSharp.dll'))
$taskCount = 0
try {
    $taskBridge = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Source\ApocaplayerBridge.cs') -Raw
    $taskTypes = @{third='Apocaplayer.ThirdPerson'; game='Apocaplayer.Game'; plugin='Apocaplayer.Plugin'; cave='Apocaplayer.Cave'; occlusion='Apocaplayer.OcclusionCutaway'}
    $taskNames = @{bool='System.Boolean'; float='System.Single'; int='System.Int32'; string='System.String'; Camera='UnityEngine.Camera'; Transform='UnityEngine.Transform'; Vector3='UnityEngine.Vector3'; Quaternion='UnityEngine.Quaternion'; 'ConfigEntry<bool>'='BepInEx.Configuration.ConfigEntry`1<System.Boolean>'; 'ConfigEntry<float>'='BepInEx.Configuration.ConfigEntry`1<System.Single>'}
    foreach($taskMatch in [regex]::Matches($taskBridge, 'Field\((third|game|plugin|cave), "([^"]+)", typeof\(([^)]+)\), (true|false)\)')) {
        $taskOwner=$taskPlayer.MainModule.Types | Where-Object FullName -eq $taskTypes[$taskMatch.Groups[1].Value]
        $taskField=$taskOwner.Fields | Where-Object Name -eq $taskMatch.Groups[2].Value
        $taskExpected=$taskNames[$taskMatch.Groups[3].Value]
        if(!$taskField -or !$taskField.IsStatic -or $taskField.FieldType.FullName -ne $taskExpected -or ($taskMatch.Groups[4].Value -eq 'true' -and $taskField.IsInitOnly)) {throw ('Installed field mismatch: '+$taskMatch.Value)}
        $taskCount++
    }
    foreach($taskContract in @(
        @('Apocaplayer.Game','DrawnWeapon','System.String'),@('Apocaplayer.Game','InCar','System.Boolean'),
        @('Apocaplayer.Game','Ready','System.Boolean'),@('Apocaplayer.Game','Dead','System.Boolean'),
        @('Apocaplayer.OcclusionCutaway','Available','System.Boolean'))){
        $taskOwner=$taskPlayer.MainModule.Types | Where-Object FullName -eq $taskContract[0]
        $taskProperty=$taskOwner.Properties | Where-Object Name -eq $taskContract[1]
        if(!$taskProperty -or !$taskProperty.GetMethod.IsStatic -or $taskProperty.PropertyType.FullName -ne $taskContract[2]) {throw ('Installed property mismatch: '+$taskContract[1])}
        $taskCount++
    }
    foreach($taskContract in @(
        @('Apocaplayer.ThirdPerson','PreCull','UnityEngine.Camera'),@('Apocaplayer.ThirdPerson','ComputeView','UnityEngine.Camera'),
        @('Apocaplayer.ThirdPerson','Tick',''),@('Apocaplayer.ThirdPerson','Zoom',''),
        @('Apocaplayer.OcclusionCutaway','Prepare','UnityEngine.Camera,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Transform'))){
        $taskOwner=$taskPlayer.MainModule.Types | Where-Object FullName -eq $taskContract[0]
        $taskMethod=$taskOwner.Methods | Where-Object { $_.Name -eq $taskContract[1] -and $_.IsStatic -and $_.ReturnType.FullName -eq 'System.Void' -and (($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join ',') -eq $taskContract[2] }
        if(!$taskMethod) {throw ('Installed method mismatch: '+$taskContract[1])}
        $taskCount++
    }
    $taskMouseLook=$taskGame.MainModule.Types | Where-Object FullName -eq 'HutongGames.PlayMaker.Actions.MouseLook'
    foreach($taskFieldName in @('rotationX','rotationY')) {
        $taskField=$taskMouseLook.Fields | Where-Object Name -eq $taskFieldName
        if(!$taskField -or $taskField.IsStatic -or $taskField.IsInitOnly -or $taskField.FieldType.FullName -ne 'System.Single') {throw ('Installed native aim field mismatch: '+$taskFieldName)}
        $taskCount++
    }
    $taskAxes=$taskMouseLook.NestedTypes | Where-Object Name -eq 'RotationAxes'
    foreach($taskAxis in @(@('MouseXAndY',0),@('MouseX',1),@('MouseY',2))) {
        $taskField=$taskAxes.Fields | Where-Object Name -eq $taskAxis[0]
        if(!$taskField -or !$taskField.HasConstant -or $taskField.Constant -ne $taskAxis[1]) {throw ('Installed native aim axis mismatch: '+$taskAxis[0])}
        $taskCount++
    }
    $taskPlugin=$taskMod.MainModule.Types | Where-Object FullName -eq 'ApocaChaseCamera.Plugin'
    $taskAttribute=$taskPlugin.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'}
    if($taskAttribute.ConstructorArguments[0].Value -ne 'local.apocalypter.chasecamera' -or $taskAttribute.ConstructorArguments[2].Value -ne $ExpectedVersion) {throw 'Wrong production plugin GUID or version.'}
    if($taskMod.MainModule.AssemblyReferences | Where-Object {$_.Name -match '^ap232$|^Apocaplayer$'}) {throw 'Apocaplayer must remain optional, without an assembly reference.'}
    if($taskMod.MainModule.Types | Where-Object {$_.Namespace -notin @('','ApocaChaseCamera')}) {throw 'Unexpected external or simulated implementation embedded in production.'}
    if($taskMod.MainModule.GetMemberReferences() | Where-Object {$_.DeclaringType.Namespace -eq 'System.Reflection.Emit'}) {throw 'Production must not call Unity unsupported Reflection.Emit methods.'}
    if(!($taskMod.MainModule.GetMemberReferences() | Where-Object {$_.DeclaringType.FullName -eq 'MonoMod.Utils.DMDGenerator`1<MonoMod.Utils.DMDCecilGenerator>' -and $_.Name -eq 'Generate'})) {throw 'Typed field access must explicitly use the Cecil backend.'}
    Write-Output 'PASS: production has no Reflection.Emit calls; typed field access explicitly uses the Cecil backend.'
    Write-Output ('PASS: '+$taskCount+' installed Apocaplayer/native aim interface contracts; production GUID/version, optional dependency and source-only type ownership verified.')
} finally { $taskPlayer.Dispose(); $taskMod.Dispose(); $taskGame.Dispose() }
