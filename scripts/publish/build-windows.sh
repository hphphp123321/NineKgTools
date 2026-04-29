#!/usr/bin/env bash
# 打 NineKgTools Windows x64 便携版（self-contained, ReadyToRun, multi-file）。
#
# 在 Linux / macOS 上跨编译为 win-x64，CI（release.yml）也用这个。
# 输出：
#   publish/NineKgTools-win-x64/      解压目录（双击 NineKgTools.Web.exe 启动）
#   publish/NineKgTools-win-x64.zip   --zip 时打包
#
# 用法：
#   bash scripts/publish/build-windows.sh                 # 仅产解压目录
#   bash scripts/publish/build-windows.sh --zip           # 同时产 zip
#   bash scripts/publish/build-windows.sh --zip --clean   # 先清掉旧 publish/ 再构建
#   bash scripts/publish/build-windows.sh --no-r2r        # 关 ReadyToRun（构建快但启动慢）
set -euo pipefail

ZIP=0
CLEAN=0
NO_R2R=0
while [[ $# -gt 0 ]]; do
    case "$1" in
        --zip) ZIP=1; shift;;
        --clean) CLEAN=1; shift;;
        --no-r2r) NO_R2R=1; shift;;
        -h|--help)
            sed -n '1,16p' "$0" | sed 's/^# \?//'
            exit 0;;
        *) echo "未知参数：$1（用 --help 看用法）" >&2; exit 1;;
    esac
done

# 切到项目根（脚本在 scripts/publish/ 下）
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT"

OUTPUT_DIR="publish/NineKgTools-win-x64"
ZIP_NAME="NineKgTools-win-x64.zip"
ZIP_PATH="publish/$ZIP_NAME"

if [[ $CLEAN -eq 1 ]]; then
    echo ">> 清理 publish/"
    rm -rf publish
fi

mkdir -p publish

R2R_NOTE="$([[ $NO_R2R -eq 0 ]] && echo ", ReadyToRun" || true)"
echo ">> dotnet publish (win-x64, self-contained$R2R_NOTE, multi-file)"

PUBLISH_ARGS=(
    NineKgTools.Web/NineKgTools.Web.csproj
    -c Release
    -r win-x64
    --self-contained true
    -p:PublishSingleFile=false
    -o "$OUTPUT_DIR"
)
[[ $NO_R2R -eq 0 ]] && PUBLISH_ARGS+=(-p:PublishReadyToRun=true)
dotnet publish "${PUBLISH_ARGS[@]}"

echo ">> 复制 Config / 文档 / 创建 runtime 目录"
mkdir -p "$OUTPUT_DIR/Config" "$OUTPUT_DIR/Database" "$OUTPUT_DIR/Logs"
cp Config/config.example.yaml "$OUTPUT_DIR/Config/"
# 用户解压即可编辑 config.yaml；首次启动 Config.InitConfig 也会自动从 example bootstrap 兜底
cp Config/config.example.yaml "$OUTPUT_DIR/Config/config.yaml"
cp Config/tags.yaml "$OUTPUT_DIR/Config/"
cp LICENSE "$OUTPUT_DIR/"
cp README.md "$OUTPUT_DIR/"

if [[ $ZIP -eq 1 ]]; then
    echo ">> 打 zip"
    rm -f "$ZIP_PATH"
    # 在 publish/ 内压缩，让 zip 内顶层是 NineKgTools-win-x64/ 而不是 publish/NineKgTools-win-x64/
    (cd publish && zip -rq "$ZIP_NAME" NineKgTools-win-x64)
    echo ">> 完成：$ZIP_PATH ($(du -h "$ZIP_PATH" | cut -f1))"
else
    echo ">> 完成：$OUTPUT_DIR"
fi
