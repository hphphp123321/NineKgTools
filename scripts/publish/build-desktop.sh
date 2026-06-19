#!/usr/bin/env bash
# 打 NineKgTools 桌面端（Avalonia）self-contained 发布产物。
#
# 两种形态：
#   1) 普通模式（默认）：产出 self-contained 目录，给 Velopack `vpk pack` 当输入。
#      publish/desktop/<rid>/
#   2) portable 模式（--portable，仅 win-x64 / linux-x64）：单文件 exe + .portable 标记
#      + Config/ + README，数据落 exe 同目录。--zip 时打成 zip。
#      publish/NineKgTools-Desktop-<rid>-portable/  (+ .zip)
#
# Velopack pack 必须在目标 OS 上跑；本脚本只负责 dotnet publish，pack/upload 由 CI 接力。
#
# 用法：
#   bash scripts/publish/build-desktop.sh --rid win-x64                 # Velopack 输入目录
#   bash scripts/publish/build-desktop.sh --rid win-x64 --portable --zip
#   bash scripts/publish/build-desktop.sh --rid linux-x64 --portable --zip
#   bash scripts/publish/build-desktop.sh --rid osx-arm64               # mac（仅普通模式）
#   bash scripts/publish/build-desktop.sh --rid win-x64 --clean
set -euo pipefail

RID="win-x64"
PORTABLE=0
ZIP=0
CLEAN=0
while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid) RID="$2"; shift 2;;
        --portable) PORTABLE=1; shift;;
        --zip) ZIP=1; shift;;
        --clean) CLEAN=1; shift;;
        -h|--help)
            sed -n '1,24p' "$0" | sed 's/^# \?//'
            exit 0;;
        *) echo "未知参数：$1（用 --help 看用法）" >&2; exit 1;;
    esac
done

# 切到项目根（脚本在 scripts/publish/ 下）
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$ROOT"

PROJ="NineKgTools.Desktop/NineKgTools.Desktop.csproj"
# 主可执行文件名按 RID 推扩展名
if [[ "$RID" == win-* ]]; then
    MAIN_EXE="NineKgTools.Desktop.exe"
else
    MAIN_EXE="NineKgTools.Desktop"
fi

if [[ $CLEAN -eq 1 ]]; then
    echo ">> 清理 publish/"
    rm -rf publish
fi
mkdir -p publish

if [[ $PORTABLE -eq 1 ]]; then
    if [[ "$RID" != "win-x64" && "$RID" != "linux-x64" ]]; then
        echo "portable 模式仅支持 win-x64 / linux-x64（mac 用 .app）。" >&2
        exit 1
    fi
    OUTPUT_DIR="publish/NineKgTools-Desktop-${RID}-portable"
    echo ">> dotnet publish ($RID, self-contained, 单文件 portable)"
    rm -rf "$OUTPUT_DIR"
    dotnet publish "$PROJ" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$OUTPUT_DIR"
    echo ">> 写 .portable 标记 + 文档"
    : > "$OUTPUT_DIR/.portable"   # 空标记文件：数据落 exe 同目录/data
    cp LICENSE "$OUTPUT_DIR/" 2>/dev/null || true
    cp README.md "$OUTPUT_DIR/" 2>/dev/null || true
    if [[ $ZIP -eq 1 ]]; then
        ZIP_NAME="NineKgTools-Desktop-${RID}-portable.zip"
        echo ">> 打 zip"
        rm -f "publish/$ZIP_NAME"
        (cd publish && zip -rq "$ZIP_NAME" "NineKgTools-Desktop-${RID}-portable")
        echo ">> 完成：publish/$ZIP_NAME ($(du -h "publish/$ZIP_NAME" | cut -f1))"
    else
        echo ">> 完成：$OUTPUT_DIR（主程序 $MAIN_EXE）"
    fi
else
    OUTPUT_DIR="publish/desktop/${RID}"
    echo ">> dotnet publish ($RID, self-contained, 多文件 → Velopack pack 输入)"
    rm -rf "$OUTPUT_DIR"
    dotnet publish "$PROJ" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -o "$OUTPUT_DIR"
    echo ">> 完成：$OUTPUT_DIR（主程序 $MAIN_EXE，交给 vpk pack -p $OUTPUT_DIR -e $MAIN_EXE）"
fi
