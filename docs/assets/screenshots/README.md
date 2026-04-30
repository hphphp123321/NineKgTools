# README 截图目录

本目录存放根 `README.md` 引用的项目截图，由 [`scripts/screenshots/`](../../../scripts/screenshots/) 下的 **Playwright 脚本自动生成**，请不要手工编辑或替换。

## 文件约定

| 文件 | 路由 | 用途 |
|---|---|---|
| `home.png` | `/` | 首页 — 媒体库概览 |
| `overview.png` | `/media/overview` | 媒体库 — 多分类切换 |
| `search.png` | `/search` | 全局搜索 |
| `pending.png` | `/source/pending` | 待识别 / 待入库双 Tab |
| `tasks.png` | `/tasks` | 任务系统 — 父子任务树 |
| `settings.png` | `/settings` | 设置页 — 可视化配置 |

## 刷新截图

**推荐（CI 自动跑 + push 回 main）**：去 [Actions → Screenshots](https://github.com/hphphp123321/NineKgTools/actions/workflows/screenshots.yml) 点 "Run workflow"，或：

```bash
gh workflow run screenshots.yml
```

跑完 CI 自动 commit 刷新的 PNG 回 main（commit message 带 `[skip ci]` 防递归）。

**本地手动**：

```bash
cd scripts/screenshots
npm install
npx playwright install chromium
npm run capture
```

详细脚本说明见 [`scripts/screenshots/README.md`](../../../scripts/screenshots/README.md)。CI workflow 配置见 [`.github/workflows/screenshots.yml`](../../../.github/workflows/screenshots.yml)。

## 规格

- 视口：1440×900，`deviceScaleFactor: 2`（Retina 清晰）
- 输出：PNG，`fullPage: false`（仅截视口，避免长页面体积爆炸）
- 主题：跟随系统（CI 上是 Linux Chromium 默认 light）
- 字体：本地用 Windows 微软雅黑；CI 上用 `fonts-noto-cjk`，CJK 字形会跟本地不同（可接受）
