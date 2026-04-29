# NineKgTools Playwright 截图任务

自动化生成根 `README.md` 里的 6 张项目截图。

## 它干什么

1. 启动（或复用）`NineKgTools.Web`（端口 `23333`）
2. 通过 `POST /api/auth/login` 登录（默认 `admin` / `admin`）
3. 依次访问 6 个路由，等 Blazor 就绪后截图
4. 把 PNG 写到 `docs/assets/screenshots/`
5. 关闭 `dotnet` 子进程（如果是我们启动的）

整个过程**无头、无人工、可重复**。UI 改动后只要跑一遍，README 的图就刷新。

## 首次使用

```bash
# 1. 进入本子项目
cd scripts/screenshots

# 2. 装依赖（只需一次）
npm install
npm run install-browsers   # 仅下载 Chromium，不装 Firefox/WebKit

# 3. 跑截图
npm run capture
```

首次运行从"启 dotnet + restore + 迁移 + 建 SignalR + 截 6 张图"大约需要 2-4 分钟；
后续因为编译缓存命中，通常 1 分钟以内搞定。

## 常用命令

| 命令 | 用途 |
|---|---|
| `npm run capture` | 无头跑，生产模式 |
| `npm run capture:headed` | 带浏览器界面，调试 UI 布局 |
| `npm run capture:debug` | 打开 Playwright Inspector 单步调试 |
| `npm run clean` | 清理 `auth.json` / 测试产物 |
| `npx playwright show-report` | 看上次的 HTML 报告（含每步截图、trace） |

## 环境变量

| 变量 | 默认 | 说明 |
|---|---|---|
| `NINEKG_BASE_URL` | `http://127.0.0.1:23333` | 目标服务 baseURL，改了 dotnet 端口也要改这个 |
| `NT_USER` | `admin` | 登录用户名（对应 `NineKgTools.Web` 的 `NT_USER`） |
| `NT_PASSWORD` | `admin` | 登录密码 |
| `OPENAI_API_KEY` | — | 如果你的 `config.yaml` 里 `api_key` 留空，dotnet 启动需要这个 |

**复用已启动的服务**：如果你本地已经在跑 `dotnet run --project NineKgTools.Web`，
`globalSetup` 会直接 HTTP 探测到并复用，不会再启一份。结束时也不会 kill 你的开发进程。

## 目录结构

```
scripts/screenshots/
├── package.json              # Node 依赖
├── tsconfig.json
├── playwright.config.ts      # 视口 1440×900 @2x、超时、storageState
├── global-setup.ts           # 启动 + 登录
├── global-teardown.ts        # 关闭我们启的 dotnet
├── utils/
│   ├── app-lifecycle.ts      # dotnet 子进程 + 端口探测
│   └── wait-for-blazor.ts    # 等 Blazor Server circuit 就绪 + 禁动画
├── specs/
│   └── capture.spec.ts       # 6 张截图
├── auth.json                 # 运行时生成（.gitignore）
└── .runtime-state.json       # 运行时生成（.gitignore）
```

## 截图列表

| 文件 | 路由 | 说明 |
|---|---|---|
| `home.png` | `/` | 首页，媒体库概览 |
| `overview.png` | `/media/overview` | 媒体库，多分类切换 |
| `search.png` | `/search?q=示例` | 全局搜索结果 |
| `pending.png` | `/source/pending` | 待识别 / 待入库双 Tab |
| `tasks.png` | `/tasks` | 任务系统，父子任务树 |
| `settings.png` | `/settings` | 设置页 |

输出目录：`docs/assets/screenshots/*.png`。

## 关于种子数据

截图脚本**不负责准备数据**。它直接使用你现有的 `Database/database.db`。
所以：

- **完全空库**：截图里是"空状态"UI（也可以用，许多 README 截图就这样）
- **本地开发库**：截到你真实的媒体条目——**提交截图前先确认里面没有敏感个人数据**
- **专用演示库**：推荐做法，见下

### 推荐：演示专用库

在 `docs/assets/seed/demo.db` 放一份**脱敏的演示数据库**，包含 20-30 条假媒体（封面用公开 CC0 图、标题用"演示作品 001"这类占位）。

运行截图前做一次临时替换：

```bash
# 备份原库
mv Database/database.db Database/database.db.bak
# 用演示库
cp docs/assets/seed/demo.db Database/database.db
# 跑截图
(cd scripts/screenshots && npm run capture)
# 恢复
mv Database/database.db.bak Database/database.db
```

或者把上述流程写成 `scripts/screenshots/utils/seed.ts` 让 `globalSetup` 自动处理。
v1.0 首发暂未纳入自动化——因为这要先构造 `demo.db`，留给后续迭代。

## 调试

### 截图里空白 / 只有部分 UI

Blazor Server 的 circuit 建立 + 首次渲染可能比 `waitForBlazor` 的默认超时更慢。
在 `capture.spec.ts` 里把对应 shot 的 `extraWaitMs` 调大（比如 2000）。

### 登录失败

```
Error: 登录失败：401 Unauthorized...
```

- 确认 `admin/admin` 凭据存在（首次启动会自动创建）
- 或设置 `NT_USER` / `NT_PASSWORD`
- 手动登录过一次然后 Web 页面里改过密码？恢复初始密码或导出新 cookie

### dotnet 启动超时

```
Error: 等待 NineKgTools.Web 就绪超时（180000ms）
```

- 本地先试试 `dotnet run --project NineKgTools.Web` 能正常启动吗
- 检查 `Config/config.yaml`（`OPENAI_API_KEY` 必须能拿到——空的话是否设了环境变量？）
- 查看 `npm run capture` 输出的 `[dotnet]` 与 `[dotnet:err]` 前缀行

### 截图不稳定 / 像素轻微抖动

`wait-for-blazor.ts` 里的 `freezeAnimations` 禁了过渡和 ripple，但还有少数元素（如异步加载的图片）会抖动。
在 spec 里用 `await page.locator('img').evaluate(imgs => Promise.all([...imgs].map(i => i.complete ? null : new Promise(r => i.onload = r))))` 强等图片加载完。

## CI 集成

v1.0 的 GitHub Actions 里会提供一个 `screenshots.yml`（由 07 号 todo 产出），手动触发 `workflow_dispatch`：

- 安装 .NET 9 SDK + Node 20 + Chromium
- 启 dotnet → 跑 capture → 把 `docs/assets/screenshots/` 的 diff 通过 `peter-evans/create-pull-request` 开成一个 PR

默认 **不在每次 PR / push 都跑**，因为成本高且容易把无关改动混进截图 diff。

## 进一步阅读

- [Playwright 官方：Screenshots](https://playwright.dev/docs/screenshots)
- [Playwright 官方：Global setup & teardown](https://playwright.dev/docs/test-global-setup-teardown)
- 上层 todo：[`docs/todo/05-playwright-screenshots.md`](../../docs/todo/05-playwright-screenshots.md)
