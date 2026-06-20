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

# MSI 向导背景品牌色（深色主题 Hero 起始色 #1E2940）
$brandBg = [System.Drawing.Color]::FromArgb(30, 41, 64)

if (-not (Test-Path $src)) {
    Write-Error "源 logo 不存在：$src"
    exit 1
}

# 把 logo 等比缩放居中绘到指定尺寸的纯色背景 BMP 上（不拉伸）。
# alignRight=$true 时 logo 靠右放（给 WiX banner 左侧标题文字让位）。
function New-BrandBmp {
    param(
        [string]$Dst, [int]$Width, [int]$Height,
        [int]$LogoBox, [bool]$AlignRight = $false
    )
    $logo = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear($brandBg)
    # 等比缩放进 LogoBox 见方
    $scale = [Math]::Min($LogoBox / $logo.Width, $LogoBox / $logo.Height)
    $lw = [int]($logo.Width * $scale)
    $lh = [int]($logo.Height * $scale)
    if ($AlignRight) {
        $x = $Width - $lw - [int](($Height - $lh) / 2)   # 右侧，水平内边距 = 垂直居中余量
    } else {
        $x = [int](($Width - $lw) / 2)
    }
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

# MSI 向导横幅（顶部细条，logo 靠右避开标题文字）+ 欢迎/完成页背景（logo 居中）
New-BrandBmp -Dst $bannerDst -Width 493 -Height 58  -LogoBox 44  -AlignRight $true
New-BrandBmp -Dst $logoDst   -Width 493 -Height 312 -LogoBox 150

# 顺带刷新 Windows .ico（多尺寸）
$icoScript = Join-Path $PSScriptRoot "build-app-ico.ps1"
if (Test-Path $icoScript) {
    Write-Host ">> 调用 build-app-ico.ps1 刷新 app.ico"
    & $icoScript
}
