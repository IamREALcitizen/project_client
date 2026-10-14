$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $projectRoot
$auditRoot = Join-Path $projectRoot 'Logs/DayAmbientAudit'
$auditProject = Join-Path $auditRoot 'Project'
$version = ((Get-Content ProjectSettings/ProjectVersion.txt | Select-Object -First 1) -split ': ')[1]
$editorData = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data"
foreach ($part in @('Assets/Plugins','Assets/Editor','Assets/Resources/DayTable','Assets/Characters','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $auditProject $part) | Out-Null
}
$responseFile = Get-ChildItem Library/Bee/artifacts -Recurse -Filter Assembly-CSharp.rsp | Select-Object -First 1
if ($null -eq $responseFile) { throw 'Open and compile this project in Unity first (the audit uses its compiler references).' }
$rsp = Get-Content -LiteralPath $responseFile.FullName | Where-Object { $_ -notmatch '^-(out|refout):' }
$rsp += '-out:"Logs/DayAmbientAudit/Project/Assets/Plugins/DayAmbient.Runtime.dll"'
if ($rsp -notcontains '"Assets/02_Scripts/GameUI/DayOceanMotion.cs"') { $rsp += '"Assets/02_Scripts/GameUI/DayOceanMotion.cs"' }
Set-Content (Join-Path $auditRoot 'runtime.rsp') $rsp
& "$editorData/NetCoreRuntime/dotnet.exe" "$editorData/DotNetSdkRoslyn/csc.dll" '@Logs/DayAmbientAudit/runtime.rsp'
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
foreach ($dll in @('Game.Core','Realtime.Core','UnityEngine.UI','Unity.TextMeshPro','Unity.InputSystem','UnityEditor.UI','UnityEditor.UI.Analytics','Unity.InternalAPIEngineBridge.004')) {
    Copy-Item "Library/ScriptAssemblies/$dll.dll" (Join-Path $auditProject "Assets/Plugins/$dll.dll")
}
Copy-Item ProjectSettings/ProjectVersion.txt (Join-Path $auditProject 'ProjectSettings')
Copy-Item Assets/Resources/DayTable/*.png* (Join-Path $auditProject 'Assets/Resources/DayTable')
Copy-Item Assets/Resources/DayTable/DayOcean.shader* (Join-Path $auditProject 'Assets/Resources/DayTable')
foreach ($character in @('PirateSpy_SD','CrewCaptain_SD','CrewDoctor_SD','CrewSailor_SD')) {
    $dest = Join-Path $auditProject "Assets/Characters/$character"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item "Assets/09_Image/Characters/$character/*.png*" $dest
}
Copy-Item 'Assets/TextMesh Pro' (Join-Path $auditProject 'Assets') -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'CaptureDayAmbient.cs') (Join-Path $auditProject 'Assets/Editor')
Set-Content (Join-Path $auditProject 'Packages/manifest.json') '{"dependencies":{"com.unity.modules.ui":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.physics2d":"1.0.0","com.unity.modules.uielements":"1.0.0","com.unity.modules.unitywebrequest":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0"}}'
Write-Output "Prepared $auditProject"
