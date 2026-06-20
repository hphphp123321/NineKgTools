# build-icons.ps1 — 从 logo-9-icon-transparent.png 派生打包用图标资源。
#
# 产出（已 commit，普通构建无需重跑；仅 logo 改了之后重跑）：
#   Assets/app.ico        Windows（由 build-app-ico.ps1 生成，本脚本会顺带调用）
#   Assets/app-icon.png   256x256，Linux AppImage（vpk pack -i）用
#   Assets/msi-banner.bmp 493x58，Windows MSI 向导顶部横幅（vpk pack --msiBanner）
#   Assets/msi-logo.bmp   493x312，Windows MSI 向导欢迎/完成页背景（vpk pack --msiLogo）
#
# macOS 的 .icns 不在这里生成（需要 Mac 的 iconutil/sips），由 CI 在 macos runner 上
# 从 Assets/app-icon.png 转换——见 .github/workflows/desktop-release.yml。
#
# 用法（仓库根目录）：pwsh -File scripts/build-icons.ps1

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "NineKgTools.Desktop\Assets\Logos\logo-9-icon-transparent.png"
$pngDst = Join-Path $repoRoot "NineKgTools.Desktop\Assets\app-icon.png"
$bannerDst = Join-Path $repoRoot "NineKgTools.Desktop\Assets\msi-banner.bmp"
$logoDst = Join-Path $repoRoot "NineKgTools.Desktop\Assets\msi-logo.bmp"

# MSI 向导品牌色（深色主题 Hero 渐变 #1E2940 → #262040 → #3D2540）
$brandBg = [System.Drawing.Color]::FromArgb(30, 41, 64)
$gradTop = [System.Drawing.Color]::FromArgb(30, 41, 64)
$gradMid = [System.Drawing.Color]::FromArgb(38, 32, 64)
$gradBot = [System.Drawing.Color]::FromArgb(61, 37, 64)

if (-not (Test-Path $src)) {
    Write-Error "源 logo 不存在：$src"
    exit 1
}

# 欢迎/完成页背景（493x312）：纯品牌竖向渐变，**不放 logo**。
# 原因：Velopack 的 WiX 模板会按不同宽高比拉伸这张图，居中 logo 会被挤成竖条失真；
# 均匀渐变被拉伸看不出失真，深底配模板的浅色文字清晰。logo 仅放在顶部横幅。
function New-MsiLogoBmp {
    param([string]$Dst, [int]$Width, [int]$Height)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # 三段竖向渐变：上半 top→mid，下半 mid→bot
    $r1 = New-Object System.Drawing.Rectangle 0, 0, $Width, ([int]($Height / 2) + 1)
    $b1 = New-Object System.Drawing.Drawing2D.LinearGradientBrush $r1, $gradTop, $gradMid, ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($b1, $r1)
    $r2 = New-Object System.Drawing.Rectangle 0, ([int]($Height / 2)), $Width, ([int]($Height / 2) + 1)
    $b2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush $r2, $gradMid, $gradBot, ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($b2, $r2)
    $b1.Dispose(); $b2.Dispose(); $g.Dispose()
    $bmp.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
    Write-Host "已生成: $Dst ($Width x $Height)"
}

# 顶部横幅（493x58）：纯品牌底 + 居中 logo（横幅宽高比接近模板区域，失真小）。
function New-MsiBannerBmp {
    param([string]$Dst, [int]$Width, [int]$Height, [int]$LogoBox)
    $logo = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear($brandBg)
    $scale = [Math]::Min($LogoBox / $logo.Width, $LogoBox / $logo.Height)
    $lw = [int]($logo.Width * $scale)
    $lh = [int]($logo.Height * $scale)
    $x = [int](($Width - $lw) / 2)
    $y = [int](($Height - $lh) / 2)
    $g.DrawImage($logo, $x, $y, $lw, $lh)
    $g.Dispose()
    $bmp.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
    $logo.Dispose()
    Write-Host "已生成: $Dst ($Width x $Height)"
}

# 256x256 PNG（Linux / Velopack 跨平台 icon 通用尺寸）
$img = [System.Drawing.Image]::FromFile($src)
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)
$g.DrawImage($img, 0, 0, 256, 256)
$g.Dispose()
$bmp.Save($pngDst, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$img.Dispose()
Write-Host "已生成: $pngDst (256x256)"

# MSI 向导横幅（顶部细条，logo 居中）+ 欢迎/完成页背景（纯渐变无 logo，避免被模板拉伸失真）
New-MsiBannerBmp -Dst $bannerDst -Width 493 -Height 58 -LogoBox 40
New-MsiLogoBmp   -Dst $logoDst   -Width 493 -Height 312

# 顺带刷新 Windows .ico（多尺寸）
$icoScript = Join-Path $PSScriptRoot "build-app-ico.ps1"
if (Test-Path $icoScript) {
    Write-Host ">> 调用 build-app-ico.ps1 刷新 app.ico"
    & $icoScript
}
