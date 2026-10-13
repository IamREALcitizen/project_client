param(
    [Parameter(Mandatory = $true)][string]$DesignId,
    [Parameter(Mandatory = $true)][string]$Name
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$characterFolder = Join-Path $projectRoot "Assets/09_Image/Characters/${Name}_SD"
$basePath = Join-Path $characterFolder "${Name}_BaseSheet.png"
$anglesPath = Join-Path $characterFolder "${Name}_AnglesSheet.png"
$skinPath = Join-Path $characterFolder "${Name}Skin.asset"
$prefabPath = Join-Path $projectRoot "Assets/03_Prefabs/Characters/${Name}_Chibi.prefab"
$templateMetaPath = Join-Path $projectRoot 'Assets/09_Image/Characters/PirateSpy_SD/PirateSpy_Standing.png.meta'
$skinTemplatePath = Join-Path $projectRoot 'Assets/09_Image/Characters/PirateSpy_SD/PirateSpySkin.asset'
$prefabTemplatePath = Join-Path $projectRoot 'Assets/03_Prefabs/Characters/PirateSpy_Chibi.prefab'

if (!(Test-Path -LiteralPath $basePath) -or !(Test-Path -LiteralPath $anglesPath)) {
    throw 'Both sprite sheets must be copied into the character folder first.'
}
if ((Test-Path -LiteralPath $skinPath) -or (Test-Path -LiteralPath $prefabPath)) {
    throw 'Skin or prefab already exists; refusing to replace stable Unity GUIDs.'
}

Add-Type -AssemblyName System.Drawing
$template = Get-Content -LiteralPath $templateMetaPath -Raw
$baseNames = @('Standing', 'SeatedFront', 'SeatedFrontBlink', 'SeatedFrontTalk', 'SeatedSide', 'SeatedBack')
$angleNames = @('Seated11OClock', 'Seated1OClock', 'Seated7OClock', 'Seated5OClock')

function Write-SheetMeta {
    param([string]$Path, [string[]]$Names, [int]$Columns, [int]$Rows, [long]$FirstId)

    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
    } finally {
        $bitmap.Dispose()
    }
    $cellWidth = [int][Math]::Floor($width / $Columns)
    $cellHeight = [int][Math]::Floor($height / $Rows)
    $guid = [guid]::NewGuid().ToString('N')
    $prefix = $template.Substring(0, $template.IndexOf('  internalIDToNameTable:'))
    $headerRest = $template.Substring($template.IndexOf('  externalObjects:'), $template.IndexOf('  spriteSheet:') - $template.IndexOf('  externalObjects:'))
    $footer = $template.Substring($template.IndexOf('  mipmapLimitGroupName:'))
    $headerRest = $headerRest.Replace('spriteMode: 1', 'spriteMode: 2').Replace('spritePixelsToUnits: 256', 'spritePixelsToUnits: 128')
    $header = ($prefix + '  internalIDToNameTable:' + "`n") -replace 'guid: [a-f0-9]{32}', "guid: $guid"
    $table = [System.Collections.Generic.List[string]]::new()
    $sprites = [System.Collections.Generic.List[string]]::new()
    $ids = @{}
    for ($i = 0; $i -lt $Names.Count; $i++) {
        $spriteName = "${Name}_$($Names[$i])"
        $id = $FirstId + $i
        $ids[$Names[$i]] = $id
        $col = $i % $Columns
        $row = [int][Math]::Floor($i / $Columns)
        $x = $col * $cellWidth
        $y = if ($row -eq ($Rows - 1)) { 0 } else { $height - (($row + 1) * $cellHeight) }
        $w = if ($col -eq ($Columns - 1)) { $width - $x } else { $cellWidth }
        $h = if ($row -eq ($Rows - 1)) { $height - ($row * $cellHeight) } else { $cellHeight }
        $spriteId = ('{0:x16}' -f $id) + '0800000000000000'
        $table.Add("  - first:`n      213: $id`n    second: $spriteName")
        $sprites.Add("    - serializedVersion: 2`n      name: $spriteName`n      rect:`n        serializedVersion: 2`n        x: $x`n        y: $y`n        width: $w`n        height: $h`n      alignment: 9`n      pivot: {x: 0.5, y: 0.02}`n      border: {x: 0, y: 0, z: 0, w: 0}`n      customData: `n      outline: []`n      physicsShape: []`n      tessellationDetail: -1`n      bones: []`n      spriteID: $spriteId`n      internalID: $id`n      vertices: []`n      indices: `n      edges: []`n      weights: []")
    }
    $nameTable = ($Names | ForEach-Object { "      ${Name}_${_}: $($ids[$_])" }) -join "`n"
    $sheet = "  spriteSheet:`n    serializedVersion: 2`n    sprites:`n" + ($sprites -join "`n") + "`n    outline: []`n    customData: `n    physicsShape: []`n    bones: []`n    spriteID: `n    internalID: 0`n    vertices: []`n    indices: `n    edges: []`n    weights: []`n    secondaryTextures: []`n    spriteCustomMetadata:`n      entries: []`n    nameFileIdTable:`n$nameTable`n"
    $meta = $header + ($table -join "`n") + "`n" + $headerRest + $sheet + $footer
    Set-Content -LiteralPath ($Path + '.meta') -Value $meta -Encoding utf8
    return @{ Guid = $guid; Ids = $ids; Width = $width; Height = $height }
}

$base = Write-SheetMeta -Path $basePath -Names $baseNames -Columns 3 -Rows 2 -FirstId 2100000000000000001
$angles = Write-SheetMeta -Path $anglesPath -Names $angleNames -Columns 2 -Rows 2 -FirstId 2200000000000000001
$skinGuid = [guid]::NewGuid().ToString('N')
$skin = Get-Content -LiteralPath $skinTemplatePath -Raw
$skin = $skin.Replace('PirateSpySkin', "${Name}Skin").Replace('PIRATE_SPY', $DesignId)
$references = @{
    standing = @($base.Guid, $base.Ids['Standing'])
    seatedFront = @($base.Guid, $base.Ids['SeatedFront'])
    seatedFrontBlink = @($base.Guid, $base.Ids['SeatedFrontBlink'])
    seatedFrontTalk = @($base.Guid, $base.Ids['SeatedFrontTalk'])
    seatedSide = @($base.Guid, $base.Ids['SeatedSide'])
    seatedBack = @($base.Guid, $base.Ids['SeatedBack'])
    seated1OClock = @($angles.Guid, $angles.Ids['Seated1OClock'])
    seated5OClock = @($angles.Guid, $angles.Ids['Seated5OClock'])
    seated7OClock = @($angles.Guid, $angles.Ids['Seated7OClock'])
    seated11OClock = @($angles.Guid, $angles.Ids['Seated11OClock'])
}
foreach ($field in $references.Keys) {
    $g = $references[$field][0]
    $id = $references[$field][1]
    $skin = [regex]::Replace($skin, "(?m)^  ${field}: .*?$", "  ${field}: {fileID: $id, guid: $g, type: 3}")
}
Set-Content -LiteralPath $skinPath -Value $skin -Encoding utf8
Set-Content -LiteralPath ($skinPath + '.meta') -Value "fileFormatVersion: 2`nguid: $skinGuid`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 11400000`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n" -Encoding utf8

$prefab = Get-Content -LiteralPath $prefabTemplatePath -Raw
$prefab = $prefab.Replace('PirateSpy_Chibi', "${Name}_Chibi")
$prefab = $prefab.Replace('a97a21190d3745db8e42f21daecfac56', $skinGuid)
$prefab = $prefab.Replace('fileID: -7172026105246684885, guid: 975448e01fa743b19b6d4229fe9e6ab1', "fileID: $($base.Ids['Standing']), guid: $($base.Guid)")
Set-Content -LiteralPath $prefabPath -Value $prefab -Encoding utf8
$prefabGuid = [guid]::NewGuid().ToString('N')
Set-Content -LiteralPath ($prefabPath + '.meta') -Value "fileFormatVersion: 2`nguid: $prefabGuid`nPrefabImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n" -Encoding utf8

$folderMeta = $characterFolder + '.meta'
if (!(Test-Path -LiteralPath $folderMeta)) {
    $folderGuid = [guid]::NewGuid().ToString('N')
    Set-Content -LiteralPath $folderMeta -Value "fileFormatVersion: 2`nguid: $folderGuid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n" -Encoding utf8
}

Write-Output "$Name imported: $($base.Width)x$($base.Height) base, $($angles.Width)x$($angles.Height) angles, skin $skinGuid, prefab $prefabGuid"

# Trim each pose to the character and put its pivot where it sits, so the skin fits every seat like the others.
& (Join-Path $PSScriptRoot 'ChibiSkinFit/FitSkins.ps1') -Name $Name
