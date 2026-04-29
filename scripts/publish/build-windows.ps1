<#
.SYNOPSIS
打 NineKgTools Windows x64 便携版（self-contained, ReadyToRun, multi-file）。

.DESCRIPTION
Windows 本地开发者用这个脚本打便携包；CI（Linux runner）走 build-windows.sh。
两边产物完全等价。

输出：
  publish\NineKgTools-win-x64\      解压目录（双击 NineKgTools.Web.exe 启动）
  publish\NineKgTools-win-x64.zip   -Zip 时打包

.PARAMETER Zip
打完 publish 后压成 zip。

.PARAMETER Clean
开始前清掉整个 publish\ 目录。

.PARAMETER NoR2R
关掉 ReadyToRun 编译——构建快但启动会变慢。一般不要加。

.EXAMPLE
.\scripts\publish\build-windows.ps1
.\scripts\publish\build-windows.ps1 -Zip
.\scripts\publish\build-windows.ps1 -Clean -Zip
#>
[CmdletBinding()]
param(
    [switch]$Zip,
    [switch]$Clean,
    [switch]$NoR2R
)

$ErrorActionPreference = 'Stop'

# 切到项目根（脚本在 scripts/publish/ 下）
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
Set-Location $Root

$OutputDir = Join-Path 'publish' 'NineKgTools-win-x64'
$ZipPath = Join-Path 'publish' 'NineKgTools-win-x64.zip'

if ($Clean) {
    Write-Host '>> 清理 publish\'
    if (Test-Path 'publish') { Remove-Item -Recurse -Force 'publish' }
}

New-Item -ItemType Directory -Force -Path 'publish' | Out-Null

$r2rNote = if ($NoR2R) { '' } else { ', ReadyToRun' }
Write-Host ">> dotnet publish (win-x64, self-contained$r2rNote, multi-file)"

$publishArgs = @(
    'publish'
    'NineKgTools.Web\NineKgTools.Web.csproj'
    '-c', 'Release'
    '-r', 'win-x64'
    '--self-contained', 'true'
    '-p:PublishSingleFile=false'
    '-o', $OutputDir
)
if (-not $NoR2R) { $publishArgs += '-p:PublishReadyToRun=true' }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败 (exit $LASTEXITCODE)" }

Write-Host '>> 复制 Config / 文档 / 创建 runtime 目录'
$configOut = Join-Path $OutputDir 'Config'
New-Item -ItemType Directory -Force -Path $configOut, (Join-Path $OutputDir 'Database'), (Join-Path $OutputDir 'Logs') | Out-Null
Copy-Item 'Config\config.example.yaml' $configOut
# 用户解压即可编辑 config.yaml；首次启动 Config.InitConfig 也会自动从 example bootstrap 兜底
Copy-Item 'Config\config.example.yaml' (Join-Path $configOut 'config.yaml')
Copy-Item 'Config\tags.yaml' $configOut
Copy-Item 'LICENSE' $OutputDir
Copy-Item 'README.md' $OutputDir

if ($Zip) {
    Write-Host '>> 打 zip'
    if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
    # 在 publish\ 里压缩，让 zip 内顶层是 NineKgTools-win-x64\ 而不是 publish\NineKgTools-win-x64\
    Push-Location 'publish'
    try {
        Compress-Archive -Path 'NineKgTools-win-x64' -DestinationPath 'NineKgTools-win-x64.zip' -CompressionLevel Optimal
    }
    finally {
        Pop-Location
    }
    $size = '{0:N1} MB' -f ((Get-Item $ZipPath).Length / 1MB)
    Write-Host ">> 完成：$ZipPath ($size)"
}
else {
    Write-Host ">> 完成：$OutputDir"
}
