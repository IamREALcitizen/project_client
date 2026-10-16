param([string]$OutputRoot = 'Logs/GameHudAudit')
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $taskRoot
$auditRoot = Join-Path $taskRoot $OutputRoot
$auditProject = Join-Path $auditRoot 'Project'
$version = ((Get-Content ProjectSettings/ProjectVersion.txt | Select-Object -First 1) -split ': ')[1]
$editorData = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data"
foreach ($part in @('Assets/Plugins','Assets/Editor','Assets/Fonts','Assets/Portraits','Assets/Resources','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $auditProject $part) | Out-Null
}
foreach ($name in @('Game.Core','Assembly-CSharp')) {
    $file = Get-ChildItem Library/Bee/artifacts -Recurse -Filter "$name.rsp" | Select-Object -First 1
    if ($null -eq $file) { throw 'Compile this project in Unity first.' }
    $rsp = Get-Content -LiteralPath $file.FullName | Where-Object { $_ -notmatch '^-(out|refout):' }
    $rsp = $rsp | Where-Object { $_ -notmatch '^"(.+\.cs)"$' -or (Test-Path -LiteralPath $Matches[1]) }
    if ($name -eq 'Assembly-CSharp') {
        $coreDll = (Join-Path $auditProject 'Assets/Plugins/Game.Core.dll').Replace('\','/')
        $rsp = $rsp | ForEach-Object { if ($_ -match '^-r:.*Game.Core.ref.dll') { '-r:"' + $coreDll + '"' } else { $_ } }
    }
    $output = if ($name -eq 'Assembly-CSharp') { 'GameHud.Runtime' } else { $name }
    $rsp += '-out:"' + (Join-Path $auditProject "Assets/Plugins/$output.dll") + '"'
    $rspPath = Join-Path $auditRoot "$name.rsp"
    Set-Content -LiteralPath $rspPath $rsp
    & "$editorData/NetCoreRuntime/dotnet.exe" "$editorData/DotNetSdkRoslyn/csc.dll" "@$rspPath"
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed." }
}
foreach ($dll in @('Assembly-CSharp-firstpass','Realtime.Core','UnityEngine.UI','Unity.TextMeshPro','Unity.InputSystem','UnityEditor.UI','UnityEditor.UI.Analytics','Unity.InternalAPIEngineBridge.004')) {
    Copy-Item "Library/ScriptAssemblies/$dll.dll" (Join-Path $auditProject 'Assets/Plugins')
}
Copy-Item Assets/Plugins/Demigiant/DOTween/DOTween.dll (Join-Path $auditProject 'Assets/Plugins')
Copy-Item ProjectSettings/ProjectVersion.txt (Join-Path $auditProject 'ProjectSettings')
Copy-Item Assets/Fonts/malgun.ttf (Join-Path $auditProject 'Assets/Fonts')
Copy-Item 'Assets/TextMesh Pro' (Join-Path $auditProject 'Assets') -Recurse -Force
Copy-Item Assets/Resources/GameHud (Join-Path $auditProject 'Assets/Resources') -Recurse -Force
Copy-Item Assets/Resources/Sprites (Join-Path $auditProject 'Assets/Resources') -Recurse -Force
foreach ($design in @('CREW_GHOST','CREW_DOCTOR','PIRATE_RAIDER','CREW_CAPTAIN')) {
    Copy-Item "Assets/09_Image/Portrait/$design.png*" (Join-Path $auditProject 'Assets/Portraits')
}
Copy-Item (Join-Path $PSScriptRoot 'CaptureGameHud.cs') (Join-Path $auditProject 'Assets/Editor')
Set-Content (Join-Path $auditProject 'Packages/manifest.json') '{"dependencies":{"com.unity.modules.ui":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.physics2d":"1.0.0","com.unity.modules.uielements":"1.0.0","com.unity.modules.unitywebrequest":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0"}}'
Write-Output "Prepared $auditProject"

