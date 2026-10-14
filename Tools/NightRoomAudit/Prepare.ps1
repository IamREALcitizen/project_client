param([string]$OutputRoot = 'Logs/NightRoomAudit')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $root
$audit = Join-Path $root $OutputRoot
$project = Join-Path $audit 'Project'
$version = ((Get-Content ProjectSettings/ProjectVersion.txt | Select-Object -First 1) -split ': ')[1]
$editorData = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data"
foreach ($part in @('Assets/Plugins','Assets/Editor','Assets/Resources/NightScene','Assets/Characters','Assets/Fonts','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Force (Join-Path $project $part) | Out-Null
}
function Compile-AuditAssembly($name, $output) {
    $file = Get-ChildItem Library/Bee/artifacts -Recurse -Filter "$name.rsp" | Select-Object -First 1
    if ($null -eq $file) { throw 'Open and compile the original project in Unity first.' }
    $rsp = Get-Content -LiteralPath $file.FullName | Where-Object { $_ -notmatch '^-(out|refout):' }
    # Exclude source paths removed by a branch change; use the fresh game-core assembly below.
    $rsp = $rsp | Where-Object { $_ -notmatch '^"(.+\.cs)"$' -or (Test-Path -LiteralPath $Matches[1]) }
    if ($name -eq 'Assembly-CSharp') {
        $coreDll = (Join-Path $project 'Assets/Plugins/Game.Core.dll').Replace('\','/')
        $rsp = $rsp | ForEach-Object { if ($_ -match '^-r:.*Game.Core.ref.dll') { '-r:"' + $coreDll + '"' } else { $_ } }
    }
    $rsp += '-out:"' + (Join-Path $project "Assets/Plugins/$output.dll") + '"'
    $rspPath = Join-Path $audit "$name.rsp"
    Set-Content -LiteralPath $rspPath $rsp
    & "$editorData/NetCoreRuntime/dotnet.exe" "$editorData/DotNetSdkRoslyn/csc.dll" "@$rspPath"
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed." }
}
Compile-AuditAssembly 'Game.Core' 'Game.Core'
Compile-AuditAssembly 'Assembly-CSharp' 'NightRoom.Runtime'
foreach ($dll in @('Realtime.Core','UnityEngine.UI','Unity.TextMeshPro','Unity.InputSystem','UnityEditor.UI','UnityEditor.UI.Analytics','Unity.InternalAPIEngineBridge.004')) {
    Copy-Item "Library/ScriptAssemblies/$dll.dll" (Join-Path $project 'Assets/Plugins')
}
$nunit = Get-ChildItem Library/PackageCache -Recurse -Filter nunit.framework.dll | Select-Object -First 1
Copy-Item $nunit.FullName (Join-Path $project 'Assets/Plugins')
Copy-Item ProjectSettings/ProjectVersion.txt (Join-Path $project 'ProjectSettings')
Copy-Item (Join-Path $PSScriptRoot 'CaptureDeadRoom.cs') (Join-Path $project 'Assets/Editor')
Copy-Item Assets/02_Scripts/Game/Core/Tests/NightRoomRulesTests.cs (Join-Path $project 'Assets/Editor')
Copy-Item Assets/02_Scripts/Game/Core/Tests/GameTestData.cs (Join-Path $project 'Assets/Editor')
Copy-Item Assets/09_Image/SceneBackgrounds/Night/CREW_GHOST_Night.png* (Join-Path $project 'Assets/Resources/NightScene')
Copy-Item Assets/09_Image/SceneBackgrounds/Night/PIRATE_SHARED_Night.png* (Join-Path $project 'Assets/Resources/NightScene')
Copy-Item Assets/Fonts/malgun.ttf (Join-Path $project 'Assets/Fonts')
Copy-Item 'Assets/TextMesh Pro' (Join-Path $project 'Assets') -Recurse -Force
foreach ($name in @('PirateSpy_SD','CrewCaptain_SD','CrewDoctor_SD','CrewSailor_SD')) {
    $dest = Join-Path $project "Assets/Characters/$name"
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item "Assets/09_Image/Characters/$name/*.png*" $dest
}
Set-Content (Join-Path $project 'Packages/manifest.json') '{"dependencies":{"com.unity.modules.ui":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.physics2d":"1.0.0","com.unity.modules.uielements":"1.0.0","com.unity.modules.unitywebrequest":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0"}}'
Write-Output "Prepared $project"
