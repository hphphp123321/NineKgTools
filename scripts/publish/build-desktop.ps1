# build-desktop.ps1 — NineKgTools 桌面端 self-contained 发布（build-desktop.sh 的 PowerShell 版）。
#
# 两种形态：
#   普通模式（默认）：publish/desktop/<rid>/        —— 给 Velopack `vpk pack` 当输入
#   portable（-Portable，仅 win-x64/linux-x64）：单文件 + .portable + 文档，数据落 exe 同目录
#
# 用法：
#   pwsh -File scripts/publish/build-desktop.ps1 -Rid win-x64
#   pwsh -File scripts/publish/build-desktop.ps1 -Rid win-x64 -Portable -Zip
#   pwsh -File scripts/publish/build-desktop.ps1 -Rid win-x64 -Clean
param(
    [string]$Rid = "win-x64",
    [switch]$Portable,
    [switch]$Zip,
    [switch]$Clean
)
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

$proj = "NineKgTools.Desktop/NineKgTools.Desktop.csproj"
$mainExe = if ($Rid -like "win-*") { "NineKgTools.Desktop.exe" } else { "NineKgTools.Desktop" }

if ($Clean) {
    Write-Host ">> 清理 publish/"
    if (Test-Path publish) { Remove-Item publish -Recurse -Force }
}
New-Item -ItemType Directory -Force publish | Out-Null

if ($Portable) {
    if ($Rid -ne "win-x64" -and $Rid -ne "linux-x64") {
        throw "portable 模式仅支持 win-x64 / linux-x64（mac 用 .app）。"
    }
    $out = "publish/NineKgTools-Desktop-$Rid-portable"
    Write-Host ">> dotnet publish ($Rid, self-contained, 单文件 portable)"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish $proj -c Release -r $Rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }
    Write-Host ">> 写 .portable 标记 + 文档"
    New-Item -ItemType File -Force "$out/.portable" | Out-Null
    Copy-Item LICENSE $out -ErrorAction SilentlyContinue
    Copy-Item README.md $out -ErrorAction SilentlyContinue
    if ($Zip) {
        $zipName = "NineKgTools-Desktop-$Rid-portable.zip"
        Write-Host ">> 打 zip"
        if (Test-Path "publish/$zipName") { Remove-Item "publish/$zipName" -Force }
        Compress-Archive -Path $out -DestinationPath "publish/$zipName"
        Write-Host ">> 完成：publish/$zipName"
    } else {
        Write-Host ">> 完成：$out（主程序 $mainExe）"
    }
} else {
    $out = "publish/desktop/$Rid"
    Write-Host ">> dotnet publish ($Rid, self-contained, 多文件 → Velopack pack 输入)"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish $proj -c Release -r $Rid --self-contained true -p:PublishSingleFile=false -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }
    Write-Host ">> 完成：$out（主程序 $mainExe，交给 vpk pack -p $out -e $mainExe）"
}
