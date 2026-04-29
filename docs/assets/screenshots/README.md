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

```bash
cd scripts/screenshots
npm install
npx playwright install chromium
npx playwright test
```

详细脚本说明见 [`scripts/screenshots/README.md`](../../../scripts/screenshots/README.md)。

## 规格

- 视口：1440×900，`deviceScaleFactor: 2`（Retina 清晰）
- 输出：PNG，`fullPage: true`
- 主题：浅色（暗色模式变种暂未启用）

> 当前截图占位 — 将在 v0.1.0 发布前由 CI 生成真实截图。
