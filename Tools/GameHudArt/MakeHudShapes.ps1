# Writes the white HUD shapes (HudRoundedFill, HudRoundedOutline, HudCircle) into Assets/Resources/GameHud (+ .meta).
#   powershell -ExecutionPolicy Bypass -File Tools\GameHudArt\MakeHudShapes.ps1
# HudShapes and NightFxSprites load these; a scene with the baked game layout (Tools/Game Scene/게임 화면 굽기) refers to
# them. Safe to run again; existing GUIDs are kept.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path (Join-Path $PSScriptRoot 'HudShapeArt.cs') -ReferencedAssemblies System.Drawing
[HudShapeArt]::Run((Join-Path $projectRoot 'Assets/Resources/GameHud'))
