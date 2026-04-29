# 部署指南

本文档面向**长期自托管、想稳定跑这套系统**的运维。如果你只想 5 分钟跑起来玩玩，看 [README 快速开始](../../README.md#-快速开始)。

## 部署方式对比

| 方式 | 适合谁 | 优点 | 缺点 |
|---|---|---|---|
| **Docker Compose** | 自托管 / NAS / 服务器 | 一行命令、自动重启、隔离干净 | 镜像不带 Chromium，`use_selenium_for_rating` 不可用 |
| **Windows 便携版** | Windows 单机用户 | 解压双击即用、不依赖 .NET、可用 Chromium | 仅 Windows，未签名 SmartScreen 弹窗一次 |
| **dotnet run** | 开发者 / 调优 | 热重载、易调试 | 需要装 .NET 9 SDK |

---

## 方式 A：Docker Compose（生产推荐）

### 标准部署

```bash
mkdir -p ninekg && cd ninekg
mkdir -p data logs config media
curl -O https://raw.githubusercontent.com/hphphp123321/NineKgTools/main/docker-compose.yml
curl -O https://raw.githubusercontent.com/hphphp123321/NineKgTools/main/.env.example
cp .env.example .env
# 编辑 .env 填 OPENAI_API_KEY
docker compose up -d
```

### 持久化目录

| 主机 | 容器 | 内容 | 备份建议 |
|---|---|---|---|
| `./data` | `/app/Database` | SQLite 主库 + 向量库 + Hangfire 库 | **必备**，定期 tar |
| `./logs` | `/app/Logs` | Serilog 文件日志 | 可选，按时间清理 |
| `./config` | `/app/Config` | `config.yaml` + `tags.yaml` | **必备**，含密钥 |
| `./media` | `/app/media` | `watch_folders` 监视的源 | 看你的媒体备份策略 |

### 反向代理 + HTTPS

容器内是 HTTP，HTTPS 由前置反代终止。**Nginx** 示例：

```nginx
server {
    listen 443 ssl http2;
    server_name media.example.com;

    ssl_certificate     /etc/letsencrypt/live/media.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/media.example.com/privkey.pem;

    # SignalR (Blazor Server) 长连接
    location / {
        proxy_pass         http://127.0.0.1:23333;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;

        # WebSocket（Blazor Server SignalR 必需）
        proxy_set_header   Upgrade           $http_upgrade;
        proxy_set_header   Connection        "upgrade";

        # SignalR 默认空闲断连较短，提到 6h 避免误断
        proxy_read_timeout 6h;
        proxy_send_timeout 6h;
    }

    # /healthz 不缓存
    location = /healthz {
        proxy_pass         http://127.0.0.1:23333/healthz;
        proxy_cache_bypass 1;
    }
}
```

**Caddy** 等价（更简单）：

```Caddyfile
media.example.com {
    reverse_proxy 127.0.0.1:23333 {
        flush_interval -1   # 禁缓冲，让 SignalR 实时推送
    }
}
```

### 环境变量参考

| 变量 | 默认 | 说明 |
|---|---|---|
| `OPENAI_API_KEY` | — | OpenAI / 兼容代理的 API Key（**必填**） |
| `OPENAI_BASE_DOMAIN` | `https://api.openai.com/` | 自定义代理时改这个 |
| `OPENAI_DEFAULT_MODEL` | `gpt-4o-mini` | 模型 ID |
| `NT_USER` | `admin` | 首次启动创建的管理员用户名 |
| `NT_PASSWORD` | `admin` | 首次启动创建的管理员密码 |
| `NINEKG_RESET_DB` | `false` | 设 `true` 启动一次会**清库重建**——慎用 |
| `NINEKG_DB_AUTO_MIGRATE` | `true` | 设 `false` 时启动只记 pending 迁移的 Warning 不执行（生产手动控时机用）。详见下方"数据库迁移" |
| `TZ` | `Asia/Shanghai` | 影响日志时间戳与 Hangfire cron 解析 |
| `DOTNET_RUNNING_IN_CONTAINER` | `true`（aspnet 镜像默认） | 触发 Serilog JSON 格式输出 |

### 数据库迁移

启动时由 `MediaDbContextMigrator.EnsureSchemaAsync` 协调，**4 路分支**：

1. `NINEKG_RESET_DB=true` → 删库后走分支 2
2. **库不存在** → `EnsureCreatedAsync` 首次建库 + 把全部已知 migrations 标为已应用
3. **库存在但缺 `__EFMigrationsHistory`**（旧 EnsureCreated 库）→ **盖 baseline**：写满 history、不重跑 SQL
4. **库存在且 history 完整** → 应用 pending migrations；无 pending 时 no-op

启动日志关键行（grep `Migration` 或中文关键词）：

```
[INF] 数据库已是最新，无待执行迁移                       # 平稳升级
[INF] 应用 N 个待执行迁移：V0_2_0_AddXxx, V0_3_0_DropYyy  # 拉到了带 schema 变更的版本
[WRN] 检测到旧库（EnsureCreated 创建...）, baseline ...   # 第一次升到引入 Migrations 的版本
[WRN] 检测到 N 个待执行迁移但 NINEKG_DB_AUTO_MIGRATE=false # 你设了手动控时机
```

### 升级到新版本

**普通升级（推荐）**——容器重启即自动迁移：

```bash
# 1. 备份（必做，迁移失败时回滚靠这个）
ts=$(date +%Y%m%d-%H%M)
tar -czf /var/backup/ninekg/preupgrade-$ts.tgz data config

# 2. 拉镜像 + 重启
docker compose pull
docker compose up -d

# 3. 检查日志确认迁移结果
docker compose logs --since=2m web | grep -E "Migration|baseline|迁移"
```

**手动控时机升级（保守路径）**——适合大版本或不放心自动迁移时：

```bash
# 1. 编辑 .env / docker-compose.yml 设 NINEKG_DB_AUTO_MIGRATE: "false"
docker compose up -d
docker compose logs web | grep "待执行迁移"   # 看日志确认有什么 pending

# 2. 停服 + 备份
docker compose down
tar -czf /var/backup/ninekg/preupgrade-$(date +%Y%m%d-%H%M).tgz data config

# 3. 改回 NINEKG_DB_AUTO_MIGRATE: "true"，启动并核对日志
docker compose up -d
docker compose logs --since=2m web | grep -E "Migration|迁移"
```

**回滚**——迁移后发现 schema 不兼容老镜像：

```bash
docker compose down
rm -rf data && tar -xzf /var/backup/ninekg/preupgrade-XXX.tgz
docker compose up -d  # 用回滚后的镜像 tag 重启
```

**主版本升级**（v1 → v2）：除上述流程外，看 [CHANGELOG.md](../../CHANGELOG.md) 的 Breaking Changes 小节，可能还要：

1. 改 `config.yaml` 适配新配置项
2. 极端情况下设 `NINEKG_RESET_DB=true` 整库重建（**所有数据丢失**，确保已备份且可接受导出/导入）

---

## 方式 B：Windows 便携版

### 部署

1. 下载 [Releases 最新版](https://github.com/hphphp123321/NineKgTools/releases/latest) 的 `NineKgTools-win-x64.zip`
2. 解压到任意目录（如 `D:\NineKgTools\`）
3. 用记事本编辑 `Config\config.yaml` 填 `OPENAI_API_KEY`
4. 双击 `NineKgTools.Web.exe`
5. 浏览器开 `http://localhost:23333`

### 开机自启动（任务计划程序）

```powershell
$action = New-ScheduledTaskAction -Execute 'D:\NineKgTools\NineKgTools.Web.exe' -WorkingDirectory 'D:\NineKgTools'
$trigger = New-ScheduledTaskTrigger -AtLogon
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERNAME" -RunLevel Limited
Register-ScheduledTask -TaskName "NineKgTools" -Action $action -Trigger $trigger -Principal $principal
```

### Windows Defender 误报

未签名 self-extract exe 偶尔触发误报。如果遇到：

1. 设置 → Windows 安全中心 → 病毒和威胁防护 → "排除项" → 加上 `D:\NineKgTools\`
2. 或提交 Microsoft 误报反馈：[microsoft.com/wdsi/filesubmission](https://www.microsoft.com/wdsi/filesubmission)

### SmartScreen 警告

第一次双击时会弹"已防止启动"。点 "更多信息" → "仍要运行" 即可（Windows 会记住选择）。

### 自动备份

```powershell
# 每天凌晨 3 点把 Database 目录打 zip
$ts = Get-Date -Format 'yyyyMMdd'
Compress-Archive -Path D:\NineKgTools\Database -DestinationPath "D:\Backup\ninekg-$ts.zip"
```

---

## 方式 C：dotnet run（开发者）

见 [DEVELOPMENT.md](../development/README.md)。本指南不重复。

---

## 备份与恢复

### 必备 vs 可选

```
data/Database/     ← 必备（媒体记录、标签、用户、任务历史）
config/config.yaml ← 必备（含密钥）
config/tags.yaml   ← 可选（启动时若 DB 已有标签则不读）
logs/              ← 可不备
.cache/            ← 不要备（启动时会清理）
media/             ← 看你怎么管理原始媒体
```

### Docker 备份脚本

```bash
#!/usr/bin/env bash
# crontab: 0 3 * * * /path/to/this.sh
ts=$(date +%Y%m%d-%H%M)
backup_dir=/var/backup/ninekg
mkdir -p $backup_dir
docker compose stop web
tar -czf $backup_dir/ninekg-$ts.tgz data config
docker compose start web
# 保留最近 30 天
find $backup_dir -name "ninekg-*.tgz" -mtime +30 -delete
```

### 恢复

```bash
docker compose down
rm -rf data config
tar -xzf /var/backup/ninekg/ninekg-YYYYMMDD-HHMM.tgz
docker compose up -d
```

---

## 故障排查

### 启动失败

| 症状 | 原因 | 解决 |
|---|---|---|
| 容器启动几秒后退出 | `config.yaml` 缺失或 watch_folders 路径不存在 | `docker logs ninekg-web` 看具体异常 |
| 日志卡在"初始化标签向量" | OpenAI 域名连不通 | 改 `ai.open_ai.base_domain` 或临时关掉向量 (`ai.vector.enable: false`) |
| 5 分钟仍未健康 | `start_period: 30s` 不够（首次构建/迁移） | 改 `docker-compose.yml` 把 start_period 调到 120s |
| 端口 23333 占用 | 主机别的服务在用 | compose 的 `ports: "8888:23333"` 把外部端口换掉 |
| Health check 一直 unhealthy | 镜像没装 curl（v1.0 镜像有装，自定义构建可能没） | 改 healthcheck 用 wget 或暂时移除 |

### 识别异常

| 症状 | 原因 | 解决 |
|---|---|---|
| 所有识别任务都失败 | `OPENAI_API_KEY` 没生效 / quota 用完 | Web UI Settings → AI 配置 → 测试连接；或控制台看 quota |
| Bangumi 识别 401 | `website.bangumi.api_key` 没填 / 过期 | 重新去 next.bgm.tv/demo/access-token 申请 |
| DLsite 识别拿不到评分 | Selenium 在容器内不可用 | 接受这个降级（`use_selenium_for_rating: false`），不影响其他字段 |
| 识别诊断显示"低于 min_similarity 全跳过" | 阈值过严 | `config.yaml` → `identification.min_similarity` 调到 0.6 或更低 |

### 性能

| 症状 | 原因 | 解决 |
|---|---|---|
| 批量识别百条以上很慢 | 默认并发 5 / 每条限速 | 提 `tasks.max_concurrent_identification_tasks` 到 10-15 |
| 首页打开慢 | PhotoWall 图片懒加载未命中 / `.cache` 太大 | 等 `CacheCleanup` 定时任务跑（每天 0 点）或手动触发 |
| 搜索语义匹配慢 | 向量库太大 | 关 `ai.vector.media.enable` 走纯关键词搜索 |
| dotnet 内存占用 1GB+ | Blazor Server circuit + Hangfire MemoryStorage 累积 | 重启容器；v1.1 计划切 SQLite Hangfire storage |

### 日志

容器 stdout 日志（默认 JSON 格式）：

```bash
docker logs ninekg-web --tail 200 -f          # 实时尾随
docker logs ninekg-web 2>&1 | jq '. | {ts:.["@t"], lvl:.["@l"], msg:.["@m"]}'   # 用 jq 过滤
```

文件日志：

```bash
ls logs/log-*.log
tail -200 logs/log-$(date +%Y-%m-%d).log
```

---

## 监控集成（可选）

`/healthz` 端点是单纯存活探针。如果想接 Prometheus / Grafana / Uptime Kuma：

- **Uptime Kuma**：HTTP keyword 监控，URL 设 `https://media.example.com/healthz`，关键字 `Healthy`
- **Prometheus blackbox**：`probe_http_status_code` 直接监控 `/healthz`
- **Grafana**：v1.0 没暴露 metrics 端点，手动用 docker stats / Loki + JSON 日志做仪表盘

更深度的可观测性（OpenTelemetry / 分布式追踪）属于 v1.1+ 工作。

---

## 安全建议

- **不要让 23333 直接暴露公网**——前置反代 + HTTPS + 限流是底线
- **改默认密码**：`NT_USER` / `NT_PASSWORD` 环境变量，或 Web UI Settings 改
- **定期备份 `data/`**：参考上面"备份与恢复"
- **监控 `OPENAI_API_KEY` 用量**：账户级限额，被滥用会很贵
- **不要把 `.env` / `config.yaml` 入 git**：含密钥；`.gitignore` 已配置
- **更新镜像**：定期 `docker compose pull` 拿安全补丁

---

## 参考

- [配置字段全表](../reference/config.md)
- [Dockerfile + docker-compose.yml](../../Dockerfile)（仓库根）
- [Health endpoint 实现](../../NineKgTools.Web/Program.cs)（搜 `MapHealthChecks`）
