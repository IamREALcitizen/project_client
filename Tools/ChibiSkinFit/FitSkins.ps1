# Trims every pose sprite of the chibi skins to the character and puts its pivot where it sits (bottom, centre of mass),
# so DayRoundTableView / ChibiCharacterView can swap skins without changing size or position.
#   powershell -ExecutionPolicy Bypass -File Tools\ChibiSkinFit\FitSkins.ps1            # all skins
#   powershell -ExecutionPolicy Bypass -File Tools\ChibiSkinFit\FitSkins.ps1 -Name CrewCaptain   # one skin folder (<Name>_SD)
#   ... -DryRun                                                                           # report only
# Safe to run again. Sheet pixels are not changed (only .meta rects/pivots); single-sprite PNGs are cropped losslessly.
param([string]$Name, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$characters = Join-Path $projectRoot 'Assets/09_Image/Characters'
Add-Type -Path (Join-Path $PSScriptRoot 'ChibiSkinFit.cs') -ReferencedAssemblies System.Drawing
[ChibiSkinFit]::Index($characters)
$filter = if ($Name) { "${Name}_SD" } else { '*_SD' }
foreach ($folder in Get-ChildItem $characters -Directory -Filter $filter) {
  foreach ($skin in Get-ChildItem $folder.FullName -Filter '*Skin.asset') {
    "== $($skin.Name)"
    foreach ($fit in [ChibiSkinFit]::Measure($skin.FullName)) {
      if ($DryRun) { "{0,-17} box {1}x{2} in cell {3}x{4} {5}" -f $fit.Field, $fit.Box.Width, $fit.Box.Height, $fit.Cell.Width, $fit.Cell.Height, $fit.Note }
      else { [ChibiSkinFit]::Apply($fit) }
    }
  }
}
