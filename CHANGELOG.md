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

### Fixed

---

## [1.0.0] - 待定

> 首次公开发布。发布前把"待定"改为实际日期（YYYY-MM-DD）。

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
- **Windows 便携版**（self-contained single-file，~75 MB zip）：解压双击即用，不依赖系统 .NET 安装
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

- **桌面端（NineKgTools.Desktop, MAUI Blazor Hybrid）本版本编译通过但未发布二进制**，计划在 v1.1 以 Windows MSIX + portable zip 形式发布
- Docker 镜像不内置 Chromium（体积考量）；`config.yaml` 的 `dlsite.use_selenium_for_rating` 在容器内不可用，主流程用 HTML / REST API 直抓不受影响
- 内置 `tags.yaml` 标签字典源自老版本 DLsite 网站爬取整理（含 R18 标签），用作一般媒体管理建议替换；详见 [`docs/reference/tags-yaml.md`](docs/reference/tags-yaml.md)

---

[Unreleased]: https://github.com/hphphp123321/NineKgTools/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/hphphp123321/NineKgTools/releases/tag/v1.0.0
