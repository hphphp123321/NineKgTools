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

# ⚠ Velopack 的 --msiBanner / --msiLogo 参数与直觉/官方文档标注的尺寸**相反**
# （实测：导出 MSI 的 Binary 表，按 WixUI 标准模板的 Control 表布局核对）：
#   --msiBanner <file>  →  WixUI_Bmp_Dialog  →  欢迎/完成页**全屏背景**，需 **493x312**
#   --msiLogo   <file>  →  WixUI_Bmp_Banner  →  其余页**顶部细条**，需 **493x58**
# 而工作流里 --msiBanner 指向 msi-banner.bmp、--msiLogo 指向 msi-logo.bmp，所以：
#   msi-banner.bmp 必须是 493x312（欢迎页背景）  ←  本脚本 New-MsiDialogBmp
#   msi-logo.bmp   必须是 493x58 （顶部细条）      ←  本脚本 New-MsiStripBmp
# 模板文字固定黑色（TextStyle Color 为空），故两张图都用**浅色底**保证黑字可读。
$paper = [System.Drawing.Color]::FromArgb(246, 245, 242)   # #F6F5F2 暖白（非纯白）
$ink   = [System.Drawing.Color]::FromArgb(34, 27, 51)      # #221B33 深墨靛（低彩度，非纯黑）
$hair  = [System.Drawing.Color]::FromArgb(225, 222, 230)   # 纸面与面板间的细分隔线

if (-not (Test-Path $src)) {
    Write-Error "源 logo 不存在：$src"
    exit 1
}

# 欢迎/完成页全屏背景（493x312 → 喂给 --msiBanner）：
# WixUI 正文黑字排在右侧（X≈135 DLU≈180px 起），左侧 ~34% 是不被文字覆盖的 art 区。
# 493x312 等比铺满 370x234 DLU 控件，1:1 无失真。
# 故：右侧暖纸白供黑字阅读；左侧深墨靛面板放居中 logo（面板宽止于正文左边界之前）。
function New-MsiDialogBmp {
    param([string]$Dst, [int]$Width, [int]$Height)
    $panelW = [int]($Width * 0.34)   # ≈168，止于正文左边界(≈180)之前
    $logo = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear($paper)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $ink), 0, 0, $panelW, $Height)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $hair), $panelW, 0, 1, $Height)
    $box = 92
    $scale = [Math]::Min($box / $logo.Width, $box / $logo.Height)
    $lw = [int]($logo.Width * $scale); $lh = [int]($logo.Height * $scale)
    $lx = [int](($panelW - $lw) / 2); $ly = [int](($Height - $lh) / 2)
    $g.DrawImage($logo, $lx, $ly, $lw, $lh)
    $g.Dispose()
    $bmp.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose(); $logo.Dispose()
    Write-Host "已生成: $Dst ($Width x $Height)"
}

# 其余页顶部细条（493x58 → 喂给 --msiLogo）：标准 WixUI 横幅标题黑字排左侧，
# 故暖纸白底 + 右侧放小 logo（不与标题文字重叠），黑字可读。
function New-MsiStripBmp {
    param([string]$Dst, [int]$Width, [int]$Height)
    $logo = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear($paper)
    $box = 38
    $scale = [Math]::Min($box / $logo.Width, $box / $logo.Height)
    $lw = [int]($logo.Width * $scale); $lh = [int]($logo.Height * $scale)
    $lx = $Width - $lw - 18; $ly = [int](($Height - $lh) / 2)
    $g.DrawImage($logo, $lx, $ly, $lw, $lh)
    $g.Dispose()
    $bmp.Save($Dst, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose(); $logo.Dispose()
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

# 注意尺寸：msi-banner.bmp(→--msiBanner) 是 493x312 欢迎页背景；
#           msi-logo.bmp(→--msiLogo) 是 493x58 顶部细条（Velopack 参数与尺寸反直觉，见上方说明）
New-MsiDialogBmp -Dst $bannerDst -Width 493 -Height 312
New-MsiStripBmp  -Dst $logoDst   -Width 493 -Height 58

# 顺带刷新 Windows .ico（多尺寸）
$icoScript = Join-Path $PSScriptRoot "build-app-ico.ps1"
if (Test-Path $icoScript) {
    Write-Host ">> 调用 build-app-ico.ps1 刷新 app.ico"
    & $icoScript
}
