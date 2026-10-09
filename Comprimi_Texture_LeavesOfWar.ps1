# ============================================================================
#  Leaves of War - riduzione texture (stesso metodo di The Portrait of Hortensia)
#
#  COME SI USA
#   1. Chiudi Unity.
#   2. Apri PowerShell nella cartella Assets del progetto
#      (in Esplora file: tasto destro dentro Assets > "Apri nel terminale").
#   3. Lancia:   powershell -ExecutionPolicy Bypass -File "PERCORSO\Comprimi_Texture_LeavesOfWar.ps1"
#   4. Riapri Unity e lascia reimportare.
#
#  COSA FA
#   - Mostra il peso delle texture PRIMA.
#   - Fa il BACKUP di tutte le immagini in ..\textures_before_resize_backup
#     (fuori da Assets, mantenendo le cartelle).
#   - Ridimensiona i pixel dei file: lato massimo $maxDim, proporzioni mantenute.
#     png / jpg / jpeg con System.Drawing (incluso in Windows),
#     tga / exr / tif / psd con ImageMagick (se installato, altrimenti saltati).
#   - Salta le cartelle in $excludeFolders (UI che si rovinerebbe, come il menu di Hortensia).
#   - Mostra il peso DOPO.
#
#  RIPRISTINO (se qualcosa non va):
#   robocopy "..\textures_before_resize_backup" "." /E /IS /IT
# ============================================================================

$maxDim = 512   # PSX: 256 o 512. Su Hortensia 128 rovinava la UI, 512 andava bene.

# Cartelle (relative ad Assets) da NON toccare: UI, logo, cursore, font.
$excludeFolders = @(
    "Resources\UI",
    "Resources\MainMenu\Logo",
    "Resources\Fonts",
    "TextMesh Pro"
)

$root = (Get-Location).Path
if ((Split-Path $root -Leaf) -ne "Assets") {
    Write-Host "Lancia lo script DENTRO la cartella Assets del progetto. Cartella attuale: $root" -ForegroundColor Red
    exit 1
}

function Test-Excluded([string]$fullPath) {
    $relative = $fullPath.Substring($root.Length).TrimStart('\')
    foreach ($ex in $excludeFolders) {
        if ($relative.StartsWith($ex, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Show-Weights([string]$title) {
    $exts = @("*.png", "*.jpg", "*.jpeg", "*.tga", "*.exr", "*.tif", "*.tiff", "*.psd")
    $grand = 0
    Write-Host "`n--- $title ---" -ForegroundColor Cyan
    foreach ($ext in $exts) {
        $sum = (Get-ChildItem -Path . -Filter $ext -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        if ($sum -gt 0) {
            $grand += $sum
            "{0,-7} {1,10:N2} MB" -f $ext, ($sum / 1MB)
        }
    }
    "TOTALE  {0,10:N2} MB" -f ($grand / 1MB)
}

Show-Weights "PESO TEXTURE PRIMA"

# ---------------------------------------------------------------- BACKUP
$backupFolder = Join-Path $root "..\textures_before_resize_backup"
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null

$allImages = Get-ChildItem -Path . -Include "*.png","*.jpg","*.jpeg","*.tga","*.exr","*.tif","*.tiff","*.psd" -Recurse -File |
    Where-Object { -not (Test-Excluded $_.FullName) }

Write-Host "`nBackup di $($allImages.Count) immagini in $backupFolder ..."
foreach ($file in $allImages) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\')
    $dest = Join-Path $backupFolder $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    Copy-Item $file.FullName $dest -Force
}
Write-Host "Backup completato."

# ---------------------------------------------------------------- PNG / JPG / JPEG
Add-Type -AssemblyName System.Drawing
$processed = 0
$skipped = 0
$errors = 0

foreach ($file in ($allImages | Where-Object { $_.Extension -match '^\.(png|jpe?g)$' })) {
    try {
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        if ($img.Width -le $maxDim -and $img.Height -le $maxDim) {
            $img.Dispose()
            $skipped++
            continue
        }

        $ratio = [Math]::Min($maxDim / $img.Width, $maxDim / $img.Height)
        $newW = [Math]::Max(1, [int]($img.Width * $ratio))
        $newH = [Math]::Max(1, [int]($img.Height * $ratio))

        # 32 bit ARGB: la trasparenza dei PNG resta.
        $resized = New-Object System.Drawing.Bitmap($newW, $newH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($resized)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($img, 0, 0, $newW, $newH)
        $g.Dispose()
        $img.Dispose()

        $tmp = $file.FullName + ".tmp"
        $format = if ($file.Extension -ieq ".png") { [System.Drawing.Imaging.ImageFormat]::Png } else { [System.Drawing.Imaging.ImageFormat]::Jpeg }
        $resized.Save($tmp, $format)
        $resized.Dispose()

        Remove-Item $file.FullName -Force
        Rename-Item $tmp $file.Name -Force
        $processed++
    } catch {
        $errors++
        Write-Host "Errore su: $($file.FullName) - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------- TGA / EXR / TIF / PSD (ImageMagick)
$others = $allImages | Where-Object { $_.Extension -match '^\.(tga|exr|tiff?|psd)$' }
if ($others.Count -gt 0) {
    if (Get-Command magick -ErrorAction SilentlyContinue) {
        foreach ($file in $others) {
            try {
                if ($file.Extension -ieq ".psd") {
                    # [0] = immagine unita: i livelli del PSD vanno persi (il backup li conserva).
                    & magick "$($file.FullName)[0]" -resize "${maxDim}x${maxDim}>" $file.FullName
                } else {
                    & magick $file.FullName -resize "${maxDim}x${maxDim}>" $file.FullName
                }
                $processed++
            } catch {
                $errors++
                Write-Host "Errore su: $($file.FullName)" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "`n$($others.Count) file tga/exr/tif/psd SALTATI: ImageMagick non e' installato (imagemagick.org)." -ForegroundColor Yellow
    }
}

Write-Host "`nRidimensionate: $processed   Gia' piccole: $skipped   Errori: $errors"
Show-Weights "PESO TEXTURE DOPO"
Write-Host "`nOra riapri Unity e lascia reimportare. Poi commit + push da GitHub Desktop."
