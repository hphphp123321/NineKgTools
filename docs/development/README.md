# 开发者指南

本文档面向**想为 NineKgTools 修 bug、加功能或做实验的开发者**。如果你只是想用它管理媒体库，请看 [README](../../README.md) 的"快速开始"。

## 速览

```bash
git clone https://github.com/hphphp123321/NineKgTools.git
cd NineKgTools
cp Config/config.example.yaml Config/config.yaml      # 编辑填 OPENAI_API_KEY
dotnet run --project NineKgTools.Web                   # 浏览器开 http://localhost:23333
```

默认账号 `admin / admin`（首次启动时由 `UserInitializationService` 创建到 SQLite）。

## 环境要求

| 必需 | 版本 | 用途 |
|---|---|---|
| **.NET 9 SDK** | 9.0.100 起 | 由 `global.json` 钉，setup-dotnet 会自动读 |
| **Git** | 任意现代版本 | 克隆与提交 |
| **Chrome / Chromium** | 任意 | 仅 `dlsite.use_selenium_for_rating: true` 时需要 |

| 可选 | 版本 | 用途 |
|---|---|---|
| **Node.js** | 20+ | 跑 `scripts/screenshots/` Playwright 截图任务 |
| **Docker Desktop** | 4.x | 测试 Dockerfile 与 docker-compose |
| **JetBrains Rider / VS / VS Code** | 任意 | IDE 都能用，`.editorconfig` 已统一风格 |

## 项目结构

```
NineKgTools/
├── NineKgTools.Core/         # 共享业务（DI 服务、模型、DbContext、识别源…）
│   └── Core/Services/        #   AI / Auth / Cache / Files / Media / Tasks / Websites
├── NineKgTools.Web/          # Blazor Server Web 端（启动项目）
│   ├── Pages/                #   20+ 路由（@page）
│   ├── Components/           #   可复用组件
│   └── wwwroot/              #   静态资源 + 模块化 CSS
├── NineKgTools.Desktop/      # MAUI Blazor Hybrid 桌面端（v1.0 暂不发布）
├── NineKgTools.Tests/        # xUnit + InMemory EFCore + FluentAssertions
├── Config/                   # config.example.yaml / tags.yaml
├── scripts/
│   ├── publish/              #   build-windows.ps1 / .sh —— 便携版打包
│   └── screenshots/          #   Playwright README 截图自动化
└── docs/                     # 你正在看的文件
```

更详细约定见 [`CLAUDE.md`](../../CLAUDE.md)（项目工程约定）和 [前端设计指南](frontend-design.md)。

## 首次启动详解

### 1. 配置

```bash
cp Config/config.example.yaml Config/config.yaml
```

> **可省略**：如果你跳过这步，启动时 `Config.InitConfig` 会自动从 `config.example.yaml` 复制一份 `config.yaml`。但建议手动 cp + 编辑，因为 example 里 `api_key` 是空的，跑起来 AI 识别会立刻报缺 key。CI / fresh clone 场景靠这个自动 fallback 跑过 `dotnet test`。

最少需要填的字段：

```yaml
ai:
  open_ai:
    api_key: sk-...           # 或留空走 OPENAI_API_KEY 环境变量
website:
  bangumi:
    api_key:                  # 留空就不能用 Bangumi 识别（DLsite/Steam 不需要 key）
```

或用环境变量替代（推荐）：

```bash
# Windows PowerShell（持久化到用户级）
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-...", "User")

# bash / zsh
export OPENAI_API_KEY=sk-...
```

### 2. 启动

```bash
dotnet run --project NineKgTools.Web
```

首次会：

1. 创建 `Database/database.db`、`Database/vectors.db`、`Database/hangfire.db`
2. 数据库不存在 → `EnsureCreatedAsync` 建表；已存在 → `MigrateAsync`（无迁移时 no-op）
3. 从 `Config/tags.yaml` 灌入 375 个标签到 DB
4. 创建默认用户 `admin / admin`
5. 启动 Kestrel 监听 `23333`
6. 启动两个 Hangfire BackgroundJobServer（主 + identification 专用）

启动日志看到 `应用已启动...` 即就绪。

### 3. 登录

浏览器开 `http://localhost:23333` → admin / admin → 进首页。

> **改默认账号**：在 Web UI 的 Settings 页改密码，或设环境变量 `NT_USER` / `NT_PASSWORD` 后清库重建（`NINEKG_RESET_DB=true`）。

## 数据库迁移

启动 schema 协调由 `Core/DbContexts/MediaDbContextMigrator.cs` 的 **`EnsureSchemaAsync`** 统一负责（`Program.cs` 调一行）。**4 路分支**：

| 触发条件 | 行为 | 适用场景 |
|---|---|---|
| `NINEKG_RESET_DB=true` | 删库 → 走"库不存在"分支 | 仅开发期重置 |
| 库不存在 | `EnsureCreatedAsync` + history 写满全部 migrations | 新部署 / fresh clone |
| 库存在但缺 `__EFMigrationsHistory` | **盖 baseline**（建 history 表 + 写满 migrations，**不重跑 SQL**） | 现网旧库的平滑迁徙 |
| 库存在且 history 完整 | `MigrateAsync` 应用 pending；无 pending → no-op | 正常运行 / 升级 |

**`NINEKG_DB_AUTO_MIGRATE=false`**：仅在分支 4 生效——检测到 pending 时只 Warning 不执行，给生产手动控时机用。

### 加 migration 流程（开发期）

```bash
# 一次性安装 EF Core 工具
dotnet tool install --global dotnet-ef

# 加迁移（command 在仓库根目录运行；输出到 NineKgTools.Core/Core/DbContexts/Migrations/）
dotnet ef migrations add V0_2_0_AddXxx \
  --project NineKgTools.Core \
  --startup-project NineKgTools.Core \
  --output-dir Core/DbContexts/Migrations
```

- `MediaDbContextDesignFactory` 提供 design-time DbContext，dotnet ef 不会触发 Web 启动副作用
- `--startup-project` 指 `NineKgTools.Core` 而不是 `.Web`——避开 Program.cs 的 `config.InitConfig()` / Hangfire / Kestrel 副作用
- **命名约定**：`V<Major>_<Minor>_<Patch>_<动词大写>` 形如 `V0_2_0_AddPendingIdentificationRetention`
- `Migrations/` 必须 commit（`.gitignore` 已放开）

### 加 migration 后的本地验证

每次加完 migration 至少跑下面 3 种数据库状态确认逻辑都正确：

```bash
# 状态 1：fresh 库（删了再启）
rm -f Database/database.db && dotnet run --project NineKgTools.Web
#   预期日志："数据库不存在，执行 EnsureCreatedAsync 首次建库" + "已写入 N 个 migration 标记到 __EFMigrationsHistory"

# 状态 2：旧 EnsureCreated 库（手动删 history 表模拟）
sqlite3 Database/database.db "DROP TABLE IF EXISTS __EFMigrationsHistory;" && dotnet run --project NineKgTools.Web
#   预期日志："检测到旧库（EnsureCreated 创建，无 __EFMigrationsHistory），baseline ..."

# 状态 3：已 history 库（正常启动）
dotnet run --project NineKgTools.Web
#   预期日志："数据库已是最新，无待执行迁移"（或"应用 N 个待执行迁移"如果你刚加了新 migration）
```

### 首次落地 InitialCreate 的特殊校验

仓库当前**还没有任何 migration**。首次 `dotnet ef migrations add InitialCreate` 之后，现网旧库会走分支 3 baseline——**前提是 InitialCreate 生成的 schema 与现有 EnsureCreated 创建出的 schema 等价**。校验方法（任选其一）：

**方法 A：对比 schema dump**

```bash
# 1) stash Migrations/，让 EnsureCreated 路径跑一次，dump schema
mv NineKgTools.Core/Core/DbContexts/Migrations /tmp/migrations-stash
NINEKG_RESET_DB=true dotnet run --project NineKgTools.Web
# 等启动完毕（日志"应用已启动"），Ctrl-C
sqlite3 Database/database.db ".schema" | grep -v __EFMigrationsHistory > /tmp/schema-ensurecreated.sql

# 2) 恢复 Migrations/ 让 Migrate 路径跑一次，dump schema
mv /tmp/migrations-stash NineKgTools.Core/Core/DbContexts/Migrations
NINEKG_RESET_DB=true dotnet run --project NineKgTools.Web
# 等启动完毕，Ctrl-C
sqlite3 Database/database.db ".schema" | grep -v __EFMigrationsHistory > /tmp/schema-migrated.sql

diff /tmp/schema-ensurecreated.sql /tmp/schema-migrated.sql
```

**方法 B：用 `dotnet ef migrations script` 直接看生成的 SQL**

```bash
dotnet ef migrations script --project NineKgTools.Core --startup-project NineKgTools.Core --output /tmp/initial.sql
# 人工 review，与 docs/architecture/ 里的现行 schema 对照
```

如果 diff 出非平凡差异（最常见：默认值、索引名、外键 `ON DELETE` 子句、列顺序、`AUTOINCREMENT` 语义），需手工调整 InitialCreate 的 `Up` 方法对齐——否则现网旧库 baseline 后会有**隐形 schema 漂移**，将来某次 EF 优化器看 model snapshot 与实际表不一致时会跑出诡异错误。

## Hangfire Dashboard

启动后访问 `http://localhost:23333/hangfire`（需登录），可以看到：

- **Recurring Jobs**：5 个内置定时任务（CacheCleanup / MediaCleanup / TagVectorSync / MediaVectorSync / PendingIdentificationCleanup）
- **Servers**：主服务器 + identification 专用服务器（一共两个 BackgroundJobServer）
- **Jobs**：实时跑的识别 / 监视任务

## 调试技巧

### 热重载

```bash
dotnet watch --project NineKgTools.Web
```

改 `.razor` / `.css` 立即生效；改 `.cs` 会重新编译重启 SignalR circuit。

### 详细日志

`Config/config.yaml`：

```yaml
log:
  log_level: Verbose          # 默认 Information；Verbose 会刷屏但看得到所有 [DBUG]
  log_types: [Console, File]
```

日志写到 `Logs/log-YYYY-MM-DD.log`。

### 识别诊断

任意识别任务跑完后，进 **Tasks → 历史 → 详情对话框**，看"识别诊断"Tab。每次识别的关键词、尝试的网站、Top 5 候选、命中分数都在里面。

如果想看运行中的任务：**Tasks 页 → 运行中 → 详情**，识别诊断信息实时刷新。

### 测试

```bash
dotnet test                              # 跑全部 156 个测试
dotnet test --filter "FullyQualifiedName~MediaName"   # 只跑名字匹配的
dotnet test --filter "Category=Integration"           # 跑集成测试（如果有 trait）
```

注意：`VectorDatabaseTests` 里有 2 个集成测试默认 fail（DI 链问题），属于 v1.1 的清理工作。

## 常见坑

### 坑 1：`watch_folders` 路径不存在 → 启动失败

```yaml
source:
  watch_folders:
    - F:\test                  # 这个路径必须存在，否则启动报错
```

如果想取消监视，直接把数组留空：

```yaml
source:
  watch_folders: []
```

### 坑 2：OpenAI 域名连不通 → 卡在向量初始化

如果在内网或被墙的环境，需要配置代理：

```yaml
ai:
  open_ai:
    base_domain: https://your-proxy.com/    # 兼容 OpenAI 协议的任何代理
```

启动时 `[INFO] InitializeTagVectorsAsync` 卡 30 秒以上 → 多半是 OpenAI 域名连不上。

### 坑 3：识别并发数改了不生效

`config.yaml` → `tasks.max_concurrent_identification_tasks`：**只在启动时读一次**，运行中改了 yaml 不会生效。改完必须重启 dotnet。

### 坑 4：Selenium ChromeDriver 版本错位

`Selenium.WebDriver.ChromeDriver` 锁定了某个 Chrome 版本（看 `.csproj`）。本机 Chrome 升级到新大版本后可能 driver 不兼容。

修复：

```bash
dotnet add NineKgTools.Core package Selenium.WebDriver.ChromeDriver --version 最新版本
```

或者直接关掉 Selenium 走纯 HTML 抓：

```yaml
website:
  dlsite:
    use_selenium_for_rating: false
```

### 坑 5：清库重建无效

设了 `NINEKG_RESET_DB=true` 但库没清——多半是开了多个 dotnet 实例，前一个把 SQLite 锁住了。`taskkill /F /IM dotnet.exe` 后再试。

## 截图自动化

为 README 刷新截图：

```bash
cd scripts/screenshots
npm install
npx playwright install chromium
npm run capture
```

详见 [`scripts/screenshots/README.md`](../../scripts/screenshots/README.md)。

## 提交贡献

参见 [`CONTRIBUTING.md`](../../CONTRIBUTING.md)：分支命名、commit 规范、PR 模板都在那里。

工程约定（命名、文件分离、CSS 位置等）见 [`CLAUDE.md`](../../CLAUDE.md)（虽然标题是 AI 助手上下文，但里面的规则人也该遵守）。
