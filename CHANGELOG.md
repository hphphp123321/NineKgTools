# Changelog

本文档记录 NineKgTools 的所有重要变更。

格式遵循 [Keep a Changelog 1.1.0](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [Semantic Versioning 2.0](https://semver.org/lang/zh-CN/)。

每个条目按以下分类组织（顺序固定，缺省可省略）：

- **Added** 新功能
- **Changed** 现有功能的变更
- **Deprecated** 即将移除的功能
- **Removed** 本版本移除的功能
- **Fixed** bug 修复
- **Security** 安全相关变更
- **Notes** 补充说明（不影响功能）

---

## [Unreleased]

> 合并 PR 时把变更追加到这里；下次发版时把内容移到对应版本节，并标记发版日期。

### Added

### Changed

- **桌面端 Windows 改用 MSI 向导安装**（`vpk pack --msi`）：双击 `.msi` 走标准向导——欢迎页（应用介绍）+ **安装位置选择**（仅为我 / 为所有用户）+ 完成页，带品牌横幅/徽标。`Setup.exe` 一键极速版同时保留；自动更新不受影响（走 Update.exe，与安装器无关）。

### Fixed

---

## [0.2.0] - 2026/6/19

> 本版本的主线是**桌面端从 MAUI Blazor Hybrid 重写为 Avalonia 原生应用**并完成打包分发；Web 端为增量改进与修复。两端共享 ~95% Core 代码。

### Added

#### 桌面端（NineKgTools.Desktop · Avalonia 12 + FluentAvalonia 3）

- 桌面端**重写为 Avalonia 11/12 + FluentAvalonia + CommunityToolkit.Mvvm**，三平台原生（Win/Mac/Linux），目标视觉 Win11 Mica / Fluent
- 与 Web 功能对齐：媒体库 / 待处理 / 媒体源 / 任务 / 标签 / 创作者 / 社团 / 收藏夹 / 网站 / 设置 + 媒体详情（内嵌页 + 独立窗双模式）
- **系统集成**：系统托盘（状态色轮询）、文件拖拽识别、单实例 + NamedPipe IPC、资源管理器右键 verb、开机自启（静默到托盘）、窗口位置记忆、主窗 `Ctrl+1..9` 快捷键
- **交互式识别流程**：选项 → 进度+诊断 → 预览/入库 三步链（与 Web 对齐，AsyncLocal 诊断作用域）
- **数据目录与 Web/Docker 完全隔离**（各平台标准 LocalAppData / Application Support / XDG）
- **打包与分发（Velopack 统一方案）**：
  - Windows `Setup.exe`（安装版）+ 便携 zip；macOS / Linux best-effort 产物
  - **跨平台自动更新**：启动静默检查 + 主窗 InfoBar 提示 + 设置「应用」分组手动检查；增量更新
  - **Portable 模式**：exe 同目录放 `.portable` 标记 → 数据落同目录，整目录可拷走
  - **首次启动引导**：3 步向导（数据目录 / 监视文件夹 / 识别源 Bangumi ApiKey），可跳过 / 重新运行
  - CI `desktop-release.yml`：`desktop-v*` tag 触发，OS 矩阵 `vpk pack` + 上传同一 Release

### Changed

- **升级 .NET 9 → .NET 10 LTS**；FluentAvaloniaUI 2.2 → 3.0.0-preview2（匹配 Avalonia 12）
- **全局搜索改造**：实时预览 Flyout + 搜索结果详情页重做（Web + 桌面端）
- **发版 tag 改为对称双前缀**：Web/Docker 走 `web-v*`（原 `v*`），桌面端走 `desktop-v*`；改 Core 时两端同升、号对齐
- 桌面端标签选择器改为**两级浏览**（顶层分组 → 组内标签），避免全量铺墙卡顿；关联媒体搜索改为大小写不敏感；文件过滤高级规则改为客户端可编辑

### Fixed

- **任务树构建 O(N²) → O(N)**，修复任务页大批量卡顿
- 封面图片不显示 / 二次丢失修复（`.cache` 启动清空 + image cache 漂移链 + `InDatabase` 短路与入库路径冲突）
- `config.yaml` bootstrap 并发安全
- `ImageService` 直出内嵌 `Image.Content` BLOB
- 桌面端：选择器对话框宽度裁切、安装后启动弹控制台窗口（`OutputType` Exe → WinExe）

### Removed

- 移除旧的 MAUI Blazor Hybrid 桌面端实现（由 Avalonia 重写取代）

### Notes

- 桌面端 **Windows 优先**完整可用（含自动更新）；macOS / Linux 为 best-effort（产物可下载，mac arm64 自动更新本期未接通）
- 桌面端**不签名**：Windows 首次启动 SmartScreen、macOS Gatekeeper 需用户手动放行（见 [桌面端安装](docs/user-guide/14-desktop-install.md)）
- Velopack 安装包 packId 为 `NineKgToolsDesktop`，与数据目录 `NineKgTools.Desktop` 隔离，**卸载不删用户数据**

---

## [0.1.0] - 2026/4/30

> 按 SemVer 0.x 表示 API 仍在迭代，破坏性变更在 0.2 / 0.3 等小版本里允许。

### Added

#### 核心系统

- 基于 .NET 9 + Blazor Server + MudBlazor 8 的智能媒体管理 Web 端，端口默认 23333
- 五大媒体类型统一管理：音频 / 视频 / 游戏 / 图片 / 文本
- 完整实体模型：媒体 / 标签 / 创作者 / 社团 / 收藏夹 / 评分 / 封面

#### 识别与搜索

- 三大识别源接入：DLsite（HTML 爬取）、Bangumi（REST API）、Steam（公开 Storefront API）
- 按媒体类型独立配置识别源优先级，可在 `/website` 页面拖拽调整
- AI 驱动识别 + 向量语义搜索（Microsoft Semantic Kernel + OpenAI Embedding + SqliteVec）
- 标签向量化 + 多级匹配（精确 / 包含 / 规范化 / 模糊 / 向量）
- jieba.NET 中文分词支持
- **识别诊断快照**：每次识别自动记录关键词切分、尝试的网站、Top 5 候选、命中分数、跳过原因；任务详情 Tab 可视化呈现
- 全局搜索（媒体 / 标签 / 创作者 / 社团 多类型聚合）

#### 工作流

- **待识别 / 待入库** 双 Tab 工作流，PendingIdentification 表持久化序列化的 MediaBase
- **手动添加媒体**（识别源搜不到的冷门资源）支持多入口共享 Helper
- **监视文件夹**自动入库（FileSystemWatcher 与批量识别解耦，部分失败不阻断监控）
- 批量操作（识别 / 入库 / 删除 / 丢弃）+ 全页锁定防止并发污染
- 标签映射管理（识别源原始 tag → 自定义标签）
- 收藏夹 / 创作者 / 社团 三套实体管理

#### 任务系统

- Hangfire 双 BackgroundJobServer 架构（主队列 + identification 队列专用）
- 父子任务递归展示 + 5 档优先级（critical → background）
- 实时进度报告 + 运行中任务面板
- 5 个内置定时任务（CacheCleanup / MediaCleanup / TagVectorSync / MediaVectorSync / PendingIdentificationCleanup）

#### UI / UX

- 自研统一弹窗体系 `NineKgConfirmDialog`，替代项目里全部 19 处默认 `ShowMessageBox` 调用
- 4 种确认 intent（Info / Affirmative / Destructive / DestructiveBatch）
- 模块化 CSS（variables / utilities / components / pages 四层）
- 深色主题为默认，响应式适配桌面与移动

#### 部署

- Docker 容器化部署：多阶段 Dockerfile + docker-compose.yml + .env.example
- **Windows 便携版**（self-contained multi-file + ReadyToRun，~120 MB zip）：解压双击即用，不依赖系统 .NET 安装；启动无解压等待
- 启动账号默认 `admin / admin`，可通过 `NT_USER` / `NT_PASSWORD` 环境变量覆盖
- GHCR 镜像自动发布（tag 触发）

#### 项目治理

- MIT License + Contributor Covenant 2.1
- GitHub Actions 三件套：CI（PR build + test）、Docker（tag 触发推 GHCR）、Release（tag 触发产 GitHub Release + Windows zip）
- Issue / PR 模板（YAML 表单式）
- Playwright 自动化截图（4K UHD）
- 完整中文文档：配置参考、tags.yaml 字典、AI 系统、媒体识别流程、任务管理架构、前端设计指南

### Security

- `Config/config.yaml` 不再追踪进 git；敏感字段通过 `OPENAI_API_KEY` 环境变量覆盖
- 默认数据库行为改为**保留**：原 `EnsureDeletedAsync` 改为仅在 `NINEKG_RESET_DB=true` 时执行，避免容器重启丢数据

### Notes

- **桌面端（NineKgTools.Desktop, MAUI Blazor Hybrid）本版本编译通过但未发布二进制**，计划在后续小版本里以 Windows MSIX + portable zip 形式发布
- Docker 镜像不内置 Chromium（体积考量）；`config.yaml` 的 `dlsite.use_selenium_for_rating` 在容器内不可用，主流程用 HTML / REST API 直抓不受影响
- 内置 `tags.yaml` 标签字典源自老版本 DLsite 网站爬取整理（含 R18 标签），用作一般媒体管理建议替换；详见 [`docs/reference/tags-yaml.md`](docs/reference/tags-yaml.md)

---

[Unreleased]: https://github.com/hphphp123321/NineKgTools/compare/web-v0.2.0...HEAD
[0.2.0]: https://github.com/hphphp123321/NineKgTools/compare/v0.1.0...web-v0.2.0
[0.1.0]: https://github.com/hphphp123321/NineKgTools/releases/tag/v0.1.0
