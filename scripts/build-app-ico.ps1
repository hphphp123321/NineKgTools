# build-app-ico.ps1 — 从 logo-9-icon-transparent.png 生成 multi-size app.ico
# 用途：嵌入到 NineKgTools.Desktop.exe (csproj 的 ApplicationIcon)，让 Win11
# 任务栏 / 资源管理器 / Alt+Tab 看到的图标都是项目品牌 logo。
#
# 用法：从仓库根目录运行
#   pwsh -File scripts/build-app-ico.ps1
#
# 何时跑：仅在 Assets/Logos/logo-9-icon-transparent.png 改了之后；生成的
# Assets/app.ico 已 commit，普通构建不需要重新生成。

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repoRoot "NineKgTools.Desktop\Assets\Logos\logo-9-icon-transparent.png"
$dst = Join-Path $repoRoot "NineKgTools.Desktop\Assets\app.ico"

if (-not (Test-Path $src)) {
    Write-Error "源 logo 不存在：$src"
    exit 1
}

# Windows .ico 标准支持的多尺寸——Win11 任务栏会按 DPI 自动选最合适的
$sizes = @(16, 24, 32, 48, 64, 128, 256)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms

# ICO header: Reserved(2)=0 + Type(2)=1(icon) + Count(2)=N
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

# 每张图先编码成 PNG payload，再写 16 字节的 ICONDIRENTRY
$dirSize = 6 + 16 * $sizes.Count
$payloadBytes = @()

foreach ($sz in $sizes) {
    $img = [System.Drawing.Image]::FromFile($src)
    $bmp = New-Object System.Drawing.Bitmap($img, $sz, $sz)
    $pngStream = New-Object System.IO.MemoryStream
    $bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $payloadBytes += ,$pngStream.ToArray()
    $bmp.Dispose()
    $img.Dispose()
    $pngStream.Dispose()
}

# ICONDIRENTRY: W(1) H(1) Colors(1) Reserved(1) Planes(2) BitCount(2) Size(4) Offset(4)
# W/H 在 256 时填 0（按规范）
$cumOffset = $dirSize
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $bytes = $payloadBytes[$i]
    $w = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $h = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0)        # ColorCount = 0 (32-bit)
    $bw.Write([byte]0)        # Reserved
    $bw.Write([UInt16]1)      # Planes
    $bw.Write([UInt16]32)     # BitCount = 32 (RGBA)
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$cumOffset)
    $cumOffset += $bytes.Length
}

# 紧接着所有 PNG payload
foreach ($bytes in $payloadBytes) {
    $bw.Write($bytes)
}

[System.IO.File]::WriteAllBytes($dst, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()

Write-Host "已生成: $dst"
Write-Host "尺寸覆盖: $($sizes -join ', ')"
Write-Host "文件大小: $((Get-Item $dst).Length) bytes"
