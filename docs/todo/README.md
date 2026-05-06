# 桌面端（Avalonia）实施 Roadmap

本目录是桌面端从 Phase 0（已完成）逐步推进到 Phase 4（打包分发）的实施清单。每个 Phase 一个 md，按顺序推进，里面是可勾选的任务条目 + 文件路径 + 验收标准。

## Phase 索引

| Phase | 主题 | 状态 | 文件 |
|---|---|---|---|
| 0 | 基础设施（AppBootstrap + Avalonia 骨架） | ✅ 已完成 | 见 [`CLAUDE.md`](../../CLAUDE.md) "桌面端" 章节 |
| 1 | 核心信息架构（主窗 + 媒体库 + 详情 + 后台任务 + 待处理源） | 📋 待开始 | [`desktop-phase-1.md`](desktop-phase-1.md) |
| 2 | 识别 / 配置 / 标签 / 创作者 | 📋 待开始 | [`desktop-phase-2.md`](desktop-phase-2.md) |
| 3 | 桌面端独占（托盘 / 拖拽 / Shell 集成 / 多窗口） | 📋 待开始 | [`desktop-phase-3.md`](desktop-phase-3.md) |
| 4 | 打包与分发（Win MSIX / Mac DMG / Linux 单文件） | 📋 待开始 | [`desktop-phase-4.md`](desktop-phase-4.md) |

## 跨 Phase 清单

| 文件 | 用途 |
|---|---|
| [`desktop-web-parity-checklist.md`](desktop-web-parity-checklist.md) | 桌面端 ↔ Web 端 功能对等盘点：列出 Web 全部页面 / 组件 / 功能 + 桌面端当前实现，差距条目留空待逐项填充。**做对等性补齐之前必读**。 |

## 总体原则

- **不要并发推进多个 Phase**——做完一个 Phase 验收通过再开下一个，避免半成品堆积。
- **每完成一项任务，对照该 Phase 的"验收标准"勾掉**；勾不掉的任务必须暴露在末尾的"未解决问题"里。
- **代码层面的工程约定** 仍以 [`CLAUDE.md`](../../CLAUDE.md) 为准，本目录只描述待办与验收，不重复约定细节。
- **跨 Phase 的依赖** 在每个 Phase 文件开头的"前置条件"里声明，不满足前置条件不要跳跃推进。
- **平台优先级**：Windows 优先（完整体验），macOS/Linux best-effort（保证能编译能启动，原生集成后置）。

## 决策清单

下面这些决策已经在 Phase 0 拍板，整个 Roadmap 都按这个执行：

- ✅ **UI 框架**：Avalonia 11 + FluentAvalonia + CommunityToolkit.Mvvm
- ✅ **数据隔离**：桌面端使用平台 LocalAppData 独立 db 文件，与 Web/Docker 完全隔离
- ✅ **共享代码**：`NineKgTools.Core` 全部复用；UI 层（Razor / MudBlazor）**不能复用**，必须用 AXAML 重写
- ✅ **平台优先级**：Win 优先，Mac/Linux best-effort
- ✅ **Hangfire 策略**：保留 Hangfire（不换 Quartz），Phase 1 切到 SQLite 持久化以支持任务跨重启续跑

## 总工作量预估

| Phase | 估时 | 主要消耗 |
|---|---|---|
| Phase 1 | 4–6 周 | 25 个 Razor 页面里的核心 5 页 AXAML 重写 + NavigationView 框架 |
| Phase 2 | 3–4 周 | 配置/标签/创作者 等剩余页面 + 识别诊断面板 |
| Phase 3 | 2–3 周 | 系统托盘 / 文件拖拽 / Shell 集成（仅 Win 完整） |
| Phase 4 | 1–2 周 | 打包 + 自动更新 + 三平台 smoke test |
| **合计** | **10–15 周** | |

> 单人节奏；Phase 1 期间冻结 Web 端非关键功能开发以避免双线维护拖累。
