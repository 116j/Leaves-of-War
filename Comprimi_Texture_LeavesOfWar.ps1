# ============================================================================
#  Leaves of War - ottimizzazione completa: pulizia, ordinamento, peso minimo
#
#  COME SI USA
#   1. Chiudi Unity.
#   2. Apri PowerShell nella cartella Assets del progetto
#      (in Esplora file: tasto destro dentro Assets > "Apri nel terminale").
#   3. Lancia:
#        powershell -ExecutionPolicy Bypass -File "PERCORSO\Comprimi_Texture_LeavesOfWar.ps1"
#      Con le build (Windows, WebGL o entrambe):
#        powershell -ExecutionPolicy Bypass -File "PERCORSO\Comprimi_Texture_LeavesOfWar.ps1" -Build All
#   4. Alla fine riapri Unity, controlla il gioco, poi commit + push da GitHub Desktop.
#
#  COSA FA
#   1. Mostra i pesi PRIMA (texture, audio, modelli, totale Assets, build).
#   2. PULIZIA FILE: cancella Temp, Logs vecchi, obj, MemoryCaptures, file .tmp,
#      i .meta rimasti senza file e le cartelle vuote dentro Assets.
#   3. TEXTURE: backup in ..\textures_before_resize_backup, poi riduce i pixel
#      (lato massimo $MaxDim, proporzioni mantenute; UI, menu e font esclusi).
#   4. UNITY (senza aprire la finestra, se Unity viene trovato):
#      Tools > Leaves of War > Optimize Everything
#        - pulizia: demo, cestino vecchio, texture MakeHuman doppie;
#        - ordinamento: oggetti di test fuori dalle scene, scene ordinate,
#          asset sparsi nelle loro cartelle, inutilizzati in Assets\_Trash (ripristinabili);
#        - peso minimo delle build: audio, texture, modelli e impostazioni del player
#          (Windows LZ4HC, WebGL Brotli con fallback, stripping del codice e degli shader).
#      Il resoconto e' in "Optimize Report.txt" nella cartella del progetto.
#   5. BUILD (con -Build): Builds\Desktop e/o Builds\WebGL, con le impostazioni piu' leggere.
#   6. Mostra i pesi DOPO.
#
#  RIPRISTINO TEXTURE (se qualcosa non va):
#   robocopy "..\textures_before_resize_backup" "." /E /IS /IT
#  RIPRISTINO ASSET SPOSTATI NEL CESTINO: Tools > Leaves of War > Project Tidy > Restore.
# ============================================================================

param(
    [ValidateSet("None", "Windows", "WebGL", "All")]
    [string]$Build = "None",
    [int]$MaxDim = 512,
    [switch]$SkipUnity,
    [switch]$SkipTextures,
    [string]$UnityPath = ""
)

# Cartelle (relative ad Assets) da NON ridimensionare: UI, logo, cursore, font, menu.
$excludeFolders = @(
    "Resources\UI",
    "Resources\MainMenu",
    "Resources\Fonts",
    "TextMesh Pro",
    "StreamingAssets",
    "_Trash"
)

$root = (Get-Location).Path
if ((Split-Path $root -Leaf) -ne "Assets") {
    Write-Host "Lancia lo script DENTRO la cartella Assets del progetto. Cartella attuale: $root" -ForegroundColor Red
    exit 1
}
$project = Split-Path $root -Parent

if (Get-Process -Name "Unity" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*Leaves*" -or $_.MainWindowTitle -like "*Leaves-of-War*" }) {
    Write-Host "Unity e' aperto su questo progetto: chiudilo e rilancia lo script." -ForegroundColor Red
    exit 1
}

function Test-Excluded([string]$fullPath) {
    $relative = $fullPath.Substring($root.Length).TrimStart('\')
    foreach ($ex in $excludeFolders) {
        if ($relative.StartsWith($ex, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-Size([string]$path, [string[]]$include) {
    if (-not (Test-Path $path)) { return 0 }
    $items = if ($include) { Get-ChildItem -Path $path -Include $include -Recurse -File -ErrorAction SilentlyContinue }
             else { Get-ChildItem -Path $path -Recurse -File -ErrorAction SilentlyContinue }
    $sum = ($items | Measure-Object -Property Length -Sum).Sum
    if ($sum) { return $sum } else { return 0 }
}

function Show-Weights([string]$title) {
    Write-Host "`n--- $title ---" -ForegroundColor Cyan
    "Texture   {0,10:N2} MB" -f ((Get-Size $root @("*.png","*.jpg","*.jpeg","*.tga","*.exr","*.tif","*.tiff","*.psd")) / 1MB)
    "Audio     {0,10:N2} MB" -f ((Get-Size $root @("*.wav","*.mp3","*.ogg","*.aif","*.aiff","*.flac")) / 1MB)
    "Modelli   {0,10:N2} MB" -f ((Get-Size $root @("*.fbx","*.obj","*.glb","*.gltf","*.blend")) / 1MB)
    "Assets    {0,10:N2} MB" -f ((Get-Size $root $null) / 1MB)
    foreach ($b in @("Builds\Desktop", "Builds\WebGL")) {
        $p = Join-Path $project $b
        if (Test-Path $p) { "{0,-9} {1,10:N2} MB" -f ($b -replace 'Builds\\',''), ((Get-Size $p $null) / 1MB) }
    }
}

Show-Weights "PESI PRIMA"

# ---------------------------------------------------------------- 2. PULIZIA FILE
Write-Host "`n--- PULIZIA FILE ---" -ForegroundColor Cyan
foreach ($folder in @("Temp", "obj", "MemoryCaptures")) {
    $p = Join-Path $project $folder
    if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Cancellata la cartella $folder" }
}
$logs = Join-Path $project "Logs"
if (Test-Path $logs) {
    Get-ChildItem $logs -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } | Remove-Item -Force -ErrorAction SilentlyContinue
}

$tmp = Get-ChildItem -Path $root -Include "*.tmp","*.orig","~*" -Recurse -File -ErrorAction SilentlyContinue
$tmp | Remove-Item -Force -ErrorAction SilentlyContinue
Write-Host "File temporanei cancellati: $($tmp.Count)"

$orphans = 0
foreach ($meta in Get-ChildItem -Path $root -Filter "*.meta" -Recurse -File -Force -ErrorAction SilentlyContinue) {
    $asset = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
    if (-not (Test-Path -LiteralPath $asset)) {
        Remove-Item -LiteralPath $meta.FullName -Force
        $orphans++
    }
}
Write-Host ".meta senza file cancellati: $orphans"

$emptyCount = 0
do {
    $removed = 0
    $dirs = Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue | Sort-Object { $_.FullName.Length } -Descending
    foreach ($dir in $dirs) {
        if (-not (Get-ChildItem -LiteralPath $dir.FullName -Force -ErrorAction SilentlyContinue)) {
            Remove-Item -LiteralPath $dir.FullName -Force -ErrorAction SilentlyContinue
            $meta = $dir.FullName + ".meta"
            if (Test-Path -LiteralPath $meta) { Remove-Item -LiteralPath $meta -Force }
            $removed++
        }
    }
    $emptyCount += $removed
} while ($removed -gt 0)
Write-Host "Cartelle vuote cancellate: $emptyCount"

# ---------------------------------------------------------------- 3. TEXTURE
if (-not $SkipTextures) {
    Write-Host "`n--- TEXTURE (lato massimo $MaxDim) ---" -ForegroundColor Cyan
    $backupFolder = Join-Path $root "..\textures_before_resize_backup"
    New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null

    $allImages = Get-ChildItem -Path . -Include "*.png","*.jpg","*.jpeg","*.tga","*.exr","*.tif","*.tiff","*.psd" -Recurse -File |
        Where-Object { -not (Test-Excluded $_.FullName) }

    Write-Host "Backup di $($allImages.Count) immagini in $backupFolder ..."
    foreach ($file in $allImages) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\')
        $dest = Join-Path $backupFolder $relative
        if (Test-Path -LiteralPath $dest) { continue }
        New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
        Copy-Item -LiteralPath $file.FullName $dest -Force
    }
    Write-Host "Backup completato (le immagini gia' salvate in un backup precedente non vengono sovrascritte)."

    Add-Type -AssemblyName System.Drawing
    $processed = 0
    $skipped = 0
    $errors = 0

    foreach ($file in ($allImages | Where-Object { $_.Extension -match '^\.(png|jpe?g)$' })) {
        try {
            $img = [System.Drawing.Image]::FromFile($file.FullName)
            if ($img.Width -le $MaxDim -and $img.Height -le $MaxDim) {
                $img.Dispose()
                $skipped++
                continue
            }

            $ratio = [Math]::Min($MaxDim / $img.Width, $MaxDim / $img.Height)
            $newW = [Math]::Max(1, [int]($img.Width * $ratio))
            $newH = [Math]::Max(1, [int]($img.Height * $ratio))

            $resized = New-Object System.Drawing.Bitmap($newW, $newH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [System.Drawing.Graphics]::FromImage($resized)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.DrawImage($img, 0, 0, $newW, $newH)
            $g.Dispose()
            $img.Dispose()

            $tmpFile = $file.FullName + ".tmp"
            $format = if ($file.Extension -ieq ".png") { [System.Drawing.Imaging.ImageFormat]::Png } else { [System.Drawing.Imaging.ImageFormat]::Jpeg }
            $resized.Save($tmpFile, $format)
            $resized.Dispose()

            Remove-Item -LiteralPath $file.FullName -Force
            Rename-Item -LiteralPath $tmpFile $file.Name -Force
            $processed++
        } catch {
            $errors++
            Write-Host "Errore su: $($file.FullName) - $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    $others = $allImages | Where-Object { $_.Extension -match '^\.(tga|exr|tiff?|psd)$' }
    if ($others.Count -gt 0) {
        if (Get-Command magick -ErrorAction SilentlyContinue) {
            foreach ($file in $others) {
                try {
                    if ($file.Extension -ieq ".psd") {
                        & magick "$($file.FullName)[0]" -resize "${MaxDim}x${MaxDim}>" $file.FullName
                    } else {
                        & magick $file.FullName -resize "${MaxDim}x${MaxDim}>" $file.FullName
                    }
                    $processed++
                } catch {
                    $errors++
                    Write-Host "Errore su: $($file.FullName)" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "$($others.Count) file tga/exr/tif/psd SALTATI: ImageMagick non e' installato (imagemagick.org)." -ForegroundColor Yellow
        }
    }
    Write-Host "Ridimensionate: $processed   Gia' piccole: $skipped   Errori: $errors"
}

# ---------------------------------------------------------------- 4/5. UNITY
function Find-Unity {
    if ($UnityPath -and (Test-Path $UnityPath)) { return $UnityPath }
    $versionFile = Join-Path $project "ProjectSettings\ProjectVersion.txt"
    $version = $null
    if (Test-Path $versionFile) {
        $line = Get-Content $versionFile | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
        if ($line) { $version = ($line -split ":")[1].Trim() }
    }
    $candidates = @()
    foreach ($base in @("$env:ProgramFiles\Unity\Hub\Editor", "${env:ProgramFiles(x86)}\Unity\Hub\Editor", "$env:LOCALAPPDATA\Unity\Hub\Editor", "D:\Unity\Hub\Editor", "D:\Program Files\Unity\Hub\Editor")) {
        if ($version) { $candidates += Join-Path $base "$version\Editor\Unity.exe" }
    }
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

function Invoke-Unity([string]$method, [string]$logName) {
    $log = Join-Path $project "Logs\$logName"
    New-Item -ItemType Directory -Force -Path (Join-Path $project "Logs") | Out-Null
    Write-Host "Unity: $method (puo' richiedere diversi minuti, il dettaglio e' in Logs\$logName) ..."
    $p = Start-Process -FilePath $unity -ArgumentList @("-batchmode", "-quit", "-projectPath", "`"$project`"", "-executeMethod", $method, "-logFile", "`"$log`"") -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) {
        Write-Host "Unity ha terminato con errore ($($p.ExitCode)): guarda Logs\$logName" -ForegroundColor Yellow
        return $false
    }
    return $true
}

if (-not $SkipUnity) {
    $unity = Find-Unity
    if (-not $unity) {
        Write-Host "`nUnity non trovato: apri il progetto e usa Tools > Leaves of War > Optimize Everything (e Build > ... per le build)." -ForegroundColor Yellow
        Write-Host "Oppure rilancia con -UnityPath `"C:\...\Editor\Unity.exe`"." -ForegroundColor Yellow
    } else {
        Write-Host "`n--- UNITY: pulizia, ordinamento, peso minimo ---" -ForegroundColor Cyan
        if (Invoke-Unity "ProjectOptimizer.RunAll" "optimize.log") {
            $report = Join-Path $project "Optimize Report.txt"
            if (Test-Path $report) { Get-Content $report | ForEach-Object { Write-Host $_ } }
        }
        if ($Build -eq "Windows" -or $Build -eq "All") {
            Write-Host "`n--- BUILD WINDOWS ---" -ForegroundColor Cyan
            Invoke-Unity "ProjectOptimizer.BuildWindows" "build-windows.log" | Out-Null
        }
        if ($Build -eq "WebGL" -or $Build -eq "All") {
            Write-Host "`n--- BUILD WEBGL ---" -ForegroundColor Cyan
            Invoke-Unity "ProjectOptimizer.BuildWebGL" "build-webgl.log" | Out-Null
        }
    }
}

Show-Weights "PESI DOPO"
Write-Host "`nOra riapri Unity, prova il gioco, poi commit + push da GitHub Desktop."
