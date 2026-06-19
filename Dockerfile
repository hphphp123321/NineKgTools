# syntax=docker/dockerfile:1.7
#
# NineKgTools.Web 多阶段 Dockerfile
#
# - build:   .NET 10 SDK，restore + publish
# - runtime: ASP.NET Core 10 Runtime，只带最小运行时依赖
#   （10.0 浮动 tag = 最新 GA 补丁，默认 Debian trixie；.NET 10 暂无稳定 *-slim Debian tag）
#
# 注意：本镜像 **不包含 Chromium**——DLsite 评分抓取（use_selenium_for_rating）
# 在容器内不可用，但默认配置已禁用该特性，主流程不受影响。
#
# 构建：  docker build -t ninekg-tools:dev .
# 运行：  docker run -p 23333:23333 -e OPENAI_API_KEY=sk-... ninekg-tools:dev
# 推荐：  docker compose up -d  （见仓库根 docker-compose.yml）

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 利用 Docker 层缓存：先复制 csproj + Directory.Build.props 做 restore，再复制源码做 build
# Directory.Build.props 必须 COPY——里面有全局 ImplicitUsings / Nullable 开关，
# 缺了它源码大量 BCL 类型（List<> / Task<> / CancellationToken 等）都会编译失败
COPY ["Directory.Build.props", "./"]
COPY ["NineKgTools.Core/NineKgTools.Core.csproj", "NineKgTools.Core/"]
COPY ["NineKgTools.Web/NineKgTools.Web.csproj",   "NineKgTools.Web/"]

RUN dotnet restore "NineKgTools.Web/NineKgTools.Web.csproj" \
    --runtime linux-x64 \
    /p:PublishReadyToRun=true

# 复制源码
COPY NineKgTools.Core/ NineKgTools.Core/
COPY NineKgTools.Web/  NineKgTools.Web/

# Publish（self-contained=false，依赖 runtime 镜像里的 .NET）
RUN dotnet publish "NineKgTools.Web/NineKgTools.Web.csproj" \
    --configuration Release \
    --runtime linux-x64 \
    --no-restore \
    --no-self-contained \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 装 curl（供 healthcheck / 运维诊断用，~5MB 增量）
# tzdata 让 Asia/Shanghai 等时区可解析
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl tzdata ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# 非 root 运行
ARG APP_UID=1001
ARG APP_GID=1001
RUN groupadd --system --gid ${APP_GID} appuser \
 && useradd  --system --uid ${APP_UID} --gid ${APP_GID} --create-home --shell /bin/false appuser \
 && mkdir -p /app/Database /app/Logs /app/Config /app/.cache /app/media \
 && chown -R appuser:appuser /app

# 复制 publish 产物
COPY --from=build --chown=appuser:appuser /app/publish ./

# 复制示例配置：首次运行时若 /app/Config/config.yaml 不存在，会被启动脚本复制为模板
COPY --chown=appuser:appuser Config/config.example.yaml /app/Config/config.example.yaml
COPY --chown=appuser:appuser Config/tags.yaml          /app/Config/tags.yaml

USER appuser

# 容器内默认时区（可被环境变量 TZ 覆盖）
ENV TZ=Asia/Shanghai \
    ASPNETCORE_URLS=http://+:23333 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_NOLOGO=true \
    NINEKG_RESET_DB=false

EXPOSE 23333

# 简易启动脚本：若 Config/config.yaml 缺失则从 example 复制一份
# （这样首次启动不需要用户手动 cp，挂 volume 后会持久化）
RUN printf '%s\n' \
    '#!/bin/sh' \
    'set -e' \
    'if [ ! -f /app/Config/config.yaml ]; then' \
    '  echo "[entrypoint] Config/config.yaml 不存在，复制 config.example.yaml 作为初始配置"' \
    '  cp /app/Config/config.example.yaml /app/Config/config.yaml' \
    'fi' \
    'exec dotnet NineKgTools.Web.dll "$@"' \
    > /app/entrypoint.sh \
 && chmod +x /app/entrypoint.sh

# Healthcheck（10 号 todo 的 /healthz 落地后 curl 会真实生效；当前先简单 hit /login）
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:23333/login -o /dev/null || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
