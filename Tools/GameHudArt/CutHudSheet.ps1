# Cuts the game HUD asset sheet into Assets/Resources/GameHud/*.png (+ .meta with 9-slice borders) for GameHudLayout.
#   powershell -ExecutionPolicy Bypass -File Tools\GameHudArt\CutHudSheet.ps1                      # Tools\GameHudArt\HudSheet.png
#   powershell -ExecutionPolicy Bypass -File Tools\GameHudArt\CutHudSheet.ps1 -Sheet new_sheet.webp # a new sheet (png or webp)
# A new sheet replaces HudSheet.png. Pieces are named by where they sit on the 1536x1024 sheet (HudArt.cs, Spots), so a
# redrawn sheet must keep the same layout. Safe to run again; existing GUIDs are kept.
param([string]$Sheet)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stored = Join-Path $PSScriptRoot 'HudSheet.png'
if ($Sheet) {
  $Sheet = (Resolve-Path $Sheet).Path
  if ($Sheet -match '\.webp$') {
    # System.Drawing cannot read webp; WPF (Windows Imaging Component) can.
    Add-Type -AssemblyName PresentationCore, WindowsBase
    $in = [System.IO.File]::OpenRead($Sheet)
    try {
      $frame = [System.Windows.Media.Imaging.BitmapDecoder]::Create($in, 'PreservePixelFormat', 'OnLoad').Frames[0]
      $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
      $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($frame))
      $out = [System.IO.File]::Create($stored); $encoder.Save($out); $out.Close()
    } finally { $in.Close() }
  } else { Copy-Item $Sheet $stored -Force }
}
Add-Type -Path (Join-Path $PSScriptRoot 'HudArt.cs') -ReferencedAssemblies System.Drawing
[HudArt]::Run($stored, (Join-Path $projectRoot 'Assets/Resources/GameHud'))
