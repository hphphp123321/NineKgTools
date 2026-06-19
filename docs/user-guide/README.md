# NineKgTools 使用指南

欢迎！这里是从用户视角写的"怎么用 NineKgTools"指南。如果你只是想 5 分钟跑起来，回去看根 [README.md](../../README.md) 即可——本指南假设你已经成功登录到 `http://localhost:23333`。

## 三条学习路径

### 🚀 新手（30 分钟内能用起来）

1. [01 入门](01-getting-started.md) — 登录后看到啥、第一件事做啥
2. [02 第一次导入媒体](02-first-import.md) — 三种导入方式选一个开始
3. [03 媒体库浏览](03-media-library.md) — 看着自己的媒体被组织起来

### 📚 中级（用顺手）

4. [04 待处理工作流](04-pending.md) — 双 Tab：待识别 + 待入库
5. [05 媒体源管理](05-sources.md) — 监视文件夹与文件清单
6. [07 搜索](07-search.md) — 关键词 + 语义双轨
7. [08 任务系统](08-tasks.md) — 看后台在忙啥 / 出问题怎么排查
8. [13 端到端工作流](13-workflows.md) ⭐ — 把所有页面串起来的真实场景

### ⚙️ 高级（深度定制）

9. [06 标签系统](06-tags.md) — 标签字典 / 单标签详情 / 标签映射
10. [11 识别源配置](11-website.md) — DLsite / Bangumi / Steam 优先级
11. [12 系统设置](12-settings.md) — 7 个 tab 全字段
12. [09 创作者 & 社团](09-creators-circles.md)
13. [10 收藏夹](10-favorites.md)
14. [14 桌面端安装](14-desktop-install.md) — Win/Mac/Linux 原生客户端怎么装（与 Docker 区别 / 自动更新 / 便携版）

### ❓ 卡住了？

- [FAQ](faq.md) — 高频问题集合
- [GitHub Issues](https://github.com/hphphp123321/NineKgTools/issues) — 没找到答案就开 issue

---

## 跟其他文档的边界

本指南**不会**重复以下内容，遇到对应问题请去原文档：

| 我想知道… | 去这里 |
|---|---|
| 怎么 docker 部署 / 反代 / 备份 | [`docs/DEPLOYMENT.md`](../operations/deployment.md) |
| 怎么本地开发 / 调试 | [`docs/DEVELOPMENT.md`](../development/README.md) |
| `config.yaml` 的某个字段干啥用 | [`docs/config-reference.md`](../reference/config.md) |
| `tags.yaml` 怎么改 / 默认标签是哪来的 | [`docs/tags-yaml-reference.md`](../reference/tags-yaml.md) |
| 识别 / AI / 任务的内部架构 | [`docs/architecture/`](../architecture/) |
| 项目工程约定（命名、CSS 位置等） | [`CLAUDE.md`](../../CLAUDE.md) |

---

## 文档约定

每页章节统一是：

1. **这个页面是干啥的** — 一句话定位 + 截图
2. **主要操作** — 按"我想做 X"的用户场景给步骤
3. **进阶用法** — 折叠区里放快捷键 / 深链 / 批量技巧
4. **跟其他页面的关系** — 进出路径 + 协作
5. **常见问题** — 页面级 FAQ

带 ⭐ 的章节是**强烈推荐先读**的（即使不在你的学习路径上）。

---

## 一份"最快上路"清单

如果你**只能花 10 分钟**学这玩意，按下面三步：

1. 读 [01 入门](01-getting-started.md) 确认登录与 OpenAI key 都到位
2. 读 [13 端到端工作流](13-workflows.md) **第一节**（监视文件夹自动入库），照着配一遍
3. 浏览 [03 媒体库](03-media-library.md) 验证识别结果展示

之后边用边查，碰到具体页面再翻对应章节。
