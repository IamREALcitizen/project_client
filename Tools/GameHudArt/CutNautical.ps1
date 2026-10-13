# Cuts GameScene3's nautical concepts into Assets/Resources/GameHud/Nautical/*.png (+ .meta with 9-slice borders) for
# GameLayoutTabs and GameHudChat:
#   powershell -ExecutionPolicy Bypass -File Tools\GameHudArt\CutNautical.ps1
#   powershell -ExecutionPolicy Bypass -File Tools\GameHudArt\CutNautical.ps1 -Preview <folder>   # also each piece on green
# Reads Docs/UIConcepts/chat-nautical.png and night-protect-nautical.png. The pieces are cut at measured places
# (NauticalArt.cs), so a redrawn concept must be measured again. Safe to run again; existing GUIDs are kept.
param([string]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$concepts = Join-Path $projectRoot 'Docs/UIConcepts'
Add-Type -Path (Join-Path $PSScriptRoot 'HudArt.cs'), (Join-Path $PSScriptRoot 'NauticalArt.cs') -ReferencedAssemblies System.Drawing
[NauticalArt]::Run((Join-Path $concepts 'chat-nautical.png'), (Join-Path $concepts 'night-protect-nautical.png'),
  (Join-Path $projectRoot 'Assets/Resources/GameHud/Nautical'), $Preview)
