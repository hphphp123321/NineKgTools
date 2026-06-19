# build-icons.ps1 — 从 logo-9-icon-transparent.png 派生打包用图标资源。
#
# 产出（已 commit，普通构建无需重跑；仅 logo 改了之后重跑）：
#   Assets/app.ico        Windows（由 build-app-ico.ps1 生成，本脚本会顺带调用）
#   Assets/app-icon.png   256x256，Linux AppImage（vpk pack -i）用
#
# macOS 的 .icns 不在这里生成（需要 Mac 的 iconutil/sips），由 CI 在 macos runner 上
# 从 Assets/app-icon.png 转换——见 .github/workflows/desktop-release.yml。
#
# 用法（仓库根目录）：pwsh -File scripts/build-icons.ps1

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "NineKgTools.Desktop\Assets\Logos\logo-9-icon-transparent.png"
$pngDst = Join-Path $repoRoot "NineKgTools.Desktop\Assets\app-icon.png"

if (-not (Test-Path $src)) {
    Write-Error "源 logo 不存在：$src"
    exit 1
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

# 顺带刷新 Windows .ico（多尺寸）
$icoScript = Join-Path $PSScriptRoot "build-app-ico.ps1"
if (Test-Path $icoScript) {
    Write-Host ">> 调用 build-app-ico.ps1 刷新 app.ico"
    & $icoScript
}
