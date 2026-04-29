# NineKgTools 项目文档

欢迎来到 NineKgTools 项目文档中心。这里包含了项目的完整技术文档和架构说明。

## 📚 文档导航

### 用户文档（终端用户优先看这里）
- **📖 [使用指南](user-guide/)** ⭐ - 按页面和工作流的详细操作教程（14 篇，覆盖 12 个核心页面 + 4 大工作流 + FAQ）
- **[开发者指南](development/README.md)** - 本地跑起来 / 数据库 / 调试 / 常见坑
- **[部署指南](operations/deployment.md)** - Docker / 反代 / 备份 / 故障排查

### 参考文档
- **[配置参考文档](reference/config.md)** - 完整的 `config.yaml` 字段说明和示例
- **[`tags.yaml` 标签字典参考](reference/tags-yaml.md)** - 内置标签字典格式、加载时机与自定义指南
- **[前端设计指南](development/frontend-design.md)** - 完整的前端设计系统和开发规范
- **[搜索系统指南](development/search-system.md)** - 搜索系统架构和实现详解

### 架构文档
- **[AI系统架构](architecture/ai-system.md)** - AI功能和向量数据库系统的完整架构说明
- **[媒体识别流程](architecture/media-identification-flow.md)** - 媒体识别系统的详细流程说明
- **[任务管理系统架构](architecture/task-management-system.md)** - 任务调度、进度报告和父子任务机制详解

## 🎯 快速了解项目

### 项目概述
NineKgTools 是一个基于 .NET 9.0 的现代化**智能媒体管理系统**，采用 Blazor 技术栈构建，支持 Web 和桌面双端部署。

### 核心特性
- 🌐 **双端支持**：Web端（Blazor Server）+ 桌面端（MAUI Blazor Hybrid）
- 🤖 **AI驱动**：基于OpenAI的智能识别和语义搜索
- 📁 **多媒体管理**：支持音频、视频、游戏、图片、文本五大类型
- 🏷️ **智能标签**：多级标签匹配策略，支持单个标签详情页面管理
- 🌍 **网站集成**：自动从DLsite、Bangumi、Steam 等网站抓取数据
- 🔍 **语义搜索**：基于向量数据库的相似度搜索

### 技术栈
- **后端**：.NET 9.0, Entity Framework Core, SQLite, Hangfire
- **前端**：Blazor Server/MAUI, MudBlazor, 模块化CSS
- **AI技术**：Microsoft.SemanticKernel, OpenAI API, SqliteVec
- **数据抓取**：Selenium WebDriver, HtmlAgilityPack

## 🏗️ 项目架构

### 整体架构
```
┌─────────────────┐    ┌─────────────────┐
│  NineKgTools    │    │  NineKgTools    │
│    .Web         │    │   .Desktop      │
│ (Blazor Server) │    │ (MAUI Hybrid)   │
└─────────────────┘    └─────────────────┘
         │                       │
         └───────────┬───────────┘
                     │
         ┌─────────────────┐
         │  NineKgTools    │
         │    .Core        │
         │ (业务逻辑层)     │
         └─────────────────┘
                     │
         ┌─────────────────┐
         │   SQLite DB     │
         │  + Vector DB    │
         └─────────────────┘
```

### 项目结构
```
NineKgTools/
├── NineKgTools.Core/        # 核心业务逻辑库
│   ├── Core/
│   │   ├── Services/        # 业务服务
│   │   ├── Models/          # 数据模型
│   │   ├── DbContexts/      # 数据库上下文
│   │   └── Interfaces/      # 服务接口
│   └── Utils/               # 工具类
├── NineKgTools.Web/         # Web前端应用
│   ├── Pages/               # 页面组件
│   ├── Components/          # 可复用组件
│   ├── Layout/              # 布局组件
│   └── wwwroot/css/         # 模块化CSS
├── NineKgTools.Desktop/     # 桌面应用
├── NineKgTools.Tests/       # 测试项目
├── Config/                  # 配置文件
└── docs/                    # 项目文档
```

## 🚀 快速开始

### 环境要求
- .NET 9.0 SDK
- Visual Studio 2022 或 JetBrains Rider

### 运行项目
```bash
# 克隆项目
git clone [repository-url]
cd NineKgTools

# 构建项目
dotnet build NineKgTools.sln

# 运行Web应用（端口：23333）
dotnet run --project NineKgTools.Web

# 运行桌面应用
dotnet run --project NineKgTools.Desktop

# 运行测试
dotnet test
```

### 配置说明
主要配置文件位于 `Config/` 目录：
- `config.yaml` - 主配置文件（应用设置、日志、AI配置、识别配置等）
- `tags.yaml` - 标签配置文件

详细配置说明请参阅 **[配置参考文档](reference/config.md)**。

#### 识别配置 (`identification`)
系统支持配置全局默认的媒体识别选项，包括：
- **skip_cache**: 是否跳过缓存强制重新识别（默认：false）
- **max_retries**: 识别失败时的最大重试次数（默认：3）
- **timeout_seconds**: 单个网站的识别超时时间（默认：30秒）
- **min_similarity**: 最小相似度阈值，低于此值的搜索结果将被忽略
- **strategy**: 识别策略（Auto、Manual、Hybrid等，默认：Auto）
- **enable_parallel_query**: 是否并行查询多个网站（默认：false）
- **max_parallelism**: 并行查询时的最大并发数（默认：3）
- **auto_add_to_database**: 后台识别任务完成后是否自动入库（默认：true）。关闭时结果进入"待处理/待入库"列表等待人工确认；不影响手动识别，手动识别始终需要人工确认入库
- **pending_retention_days**: 待入库识别结果保留天数（默认：30；0 表示永不清理）。由定时任务 `PendingIdentificationCleanupTask`（与 `CacheCleanupTask`、`MediaCleanupTask` 等同属 ScheduledTask 系统）按 Cron 触发执行，默认每天凌晨 3 点；超期记录会被删除并把对应媒体源回到"待识别"状态。可在 Tasks 页面手动触发或禁用

可在设置页面（/settings）的"识别配置"标签页中进行可视化配置。

### 🌟 功能亮点

#### 待处理媒体源页面 (`/source/pending`，旧别名 `/source/unknown`)
本页面取代了原"未识别"页面，采用双 Tab 结构统一管理"尚未识别"和"已识别但未入库"两种状态。

- **数据模型拆分**：原 `MediaSource.Processed` 字段已拆成 `Identified`（是否已执行过识别流程）和 `InDatabase`（识别结果是否已作为 MediaBase 入库）两个布尔字段
- **双 Tab 结构**：
  - **待识别 Tab**（`Identified = false`）：原"未识别"页面的完整功能——表格列表、分类卡片、筛选、单项/批量操作
  - **待入库 Tab**（`Identified = true && InDatabase = false`）：展示已识别但未入库的媒体及识别出的标题，提供预览、直接入库、重新识别、丢弃等操作
- **Pending 持久化**：新增 `PendingIdentification` 表和 `PendingIdentificationService`，把已识别的 MediaBase 通过 `MediaBaseJsonSerializer`（多态 System.Text.Json）序列化到 JSON 列持久保存
- **Pending 产出路径**：只有后台识别任务（`SingleSourceIdentificationTask`）在 `AutoAddToDatabase = false` 时会产出 Pending 记录；手动识别入口始终弹 `MediaInfoDialog` 由用户确认
- **分类统计卡片**：数据驱动的分类卡片（与MediaOverviewPage一致），支持骨架屏加载态、彩色计数、键盘操作；点击可快速筛选
- **响应式操作列**：桌面端展开图标按钮，移动端折叠为MudMenu三点菜单
- **筛选功能**：支持按类型下拉筛选、按路径关键字搜索，筛选栏响应式布局；两个 Tab 共享同一组筛选条件
- **单项操作**（待识别 Tab）：每条记录支持手动识别、**手动添加媒体**、加入识别队列、打开文件位置、删除
- **单项操作**（待入库 Tab）：预览（弹 MediaInfoDialog 确认入库）、直接入库（不预览）、重新识别（替换当前 Pending 结果）、丢弃（删除 Pending 并回到待识别）
- **手动添加媒体流程**（多入口共享）：识别源搜不到的冷门资源/个人录制可走此流程。核心对话框 `ManualAddMediaDialog` 收集 Title + TopCategory（必填），另配"填更多信息（可选）"手风琴折叠选填字段（具体分类 / 简介 / 评分），构造对应子类 MediaBase（`StaticCategories.OtherVideo/OtherAudio/OtherPicture/OtherText/OtherGame` 作默认具体分类），走 `FilesService.AddMediaToDatabase` 统一入库路径，返回 `ManualAddMediaResult(MediaId, FullyFilled)`；调用方根据 `FullyFilled` 决定是否附加 `?edit=true` —— 用户展开手风琴填了任一选填项则直跳详情页不进入编辑态，否则仍跳转 `/media/{id}?edit=true` 让用户补齐剩余字段。`MediaPage` 通过 `[SupplyParameterFromQuery(Name = "edit")]` 识别该参数。对话框本身是"略升一档"的体验：Hero 带分类色 135° 渐变 + 发光图标，Hero 色随 TopCategory 选择平滑过渡，路径可点击复制；Hero 下元数据 chip 显示"文件夹 · N 个条目" / "文件 · 后缀"。三个入口：
  - **待识别 Tab 行按钮**（`UnknownPage`）：直接传入现有 `MediaSource` 实例，不经 Helper 直接调用对话框
  - **媒体源浏览器**（`SourcesPage`）：文件夹行和文件行的"尝试识别"按钮旁都有"手动添加"按钮，传入路径字符串；交由共享入口 `ManualAddMediaHelper.OpenByPathAsync` 处理
  - **媒体库页面**（`MediaOverviewPage`）：`PageHeader` 的 `Actions` 里"新建媒体"按钮 → 先弹 `MediaKindPickerDialog`（可视化双卡片选择器，文件夹=warning 色 / 单文件=info 色）→ 弹 `FileExplorer` 选路径 → 交给 Helper
- **手动添加的共享封装 `ManualAddMediaHelper`**（`NineKgTools.Web/Components/Medias/ManualAddMediaHelper.cs`）：`OpenByPathAsync(path, ...)` 统一处理"路径 → `new MediaSource(path)` → `SourceService.FindMediaSourceAsync` 查重 → 已入库则跳转现有 Media（用 `MediaService.GetMediaIdByFullPathAsync` 按路径快速查 Id）/ 未入库则复用 DB 跟踪实例或用新候选 → 弹 `ManualAddMediaDialog` → 根据 `ManualAddMediaResult.FullyFilled` 决定带不带 `?edit=true` 后缀"
- **批量操作**：支持多选后批量删除、批量加入识别队列，带处理中状态防止重复提交
- **批量运行时全页锁定**：任一批量任务（批量识别/删除/入库/丢弃）执行期间，刷新按钮、Tab 切换、筛选栏（搜索框、类型下拉、重置）、分类统计卡片以及所有行内操作均被禁用，分类卡片追加 `card-disabled` 工具类给出视觉反馈；键盘分支通过 `SetCategoryFilter`/`OnActiveTabChanged` 内部 guard 双重拦截
- **远程访问检测**：自动检测是否为远程访问，远程时隐藏"打开文件位置"按钮
- **`IdentificationOptionsDialog` 瘦身**：删除了"自动添加到数据库"开关（该开关在手动识别流程中从未生效），对应面板标题改为"网站优先级"
- **入库入口集中**：四个手动识别页面（UnknownPage、SourcesPage、SourceDetailPage、TaskExecutionHistoryPage）已把 `MediaService.AddOrUpdateMediaAsync` 改为调用 `FilesService.AddMediaToDatabase`，确保 `Identified` 与 `InDatabase` 两个标志的维护有单一 source of truth，并自动清理可能存在的 Pending 记录
- **重新识别语义**（`AddOrUpdateMediaAsync`）：当 `media.Source.InDatabase == true` 且该 Source 已有关联 Media 时，**先删除旧 Media 再插入新结果**（"用新识别结果替换旧记录"的预期）。删除范围仅限旧 Media 及其图片/向量，MediaSource 本身保留并复用于新 Media。历史上该守卫是"跳过重复添加"，导致 SourcesPage 重新识别后看不到更新
- **MediaInfoDialog 入库反馈**：`MediaInfoDialog` 新增 `OnConfirmAsync` 回调参数，点"添加到数据库"时按钮自身切换为加载态（spinner + "添加中..."）并禁用取消按钮；失败则在对话框顶部显示 `MudAlert` 错误信息并允许重试，成功则关闭对话框并由调用方显示成功 Snackbar。同时支持 `ConfirmText`（自定义按钮文案）和 `HideConfirmButton`（纯预览模式）参数
- **深链查询参数**：页面支持 `?tab=unidentified` / `?tab=pending` 查询参数直接落到指定 Tab，缺省或无效值时维持默认（待识别）。主页"待处理"卡片即通过该参数直达对应 Tab

#### 任务管理页面 (`/tasks`)
- **上下布局结构**：页面采用全宽上下布局，更简洁美观
- **统计与导航区域**：全宽4列统计卡片，显示总执行数、成功率、后台任务数、定时任务数
- **快速导航**：点击后台任务和定时任务卡片可直接跳转到对应管理页面
- **运行中任务监控**：全宽显示所有运行中的任务，包含进度条、状态标签和时间信息
- **父子任务树展示**：支持展开/收起的递归树形结构，清晰展示任务层级关系
- **任务操作**：支持取消单个任务、批量取消任务树（父任务+所有子任务）
- **执行历史入口**：底部提供执行历史跳转按钮
- **导航栏集成**：任务管理入口带有活动任务数量Badge，点击可弹出快速预览
- **快速查看弹窗**：不离开当前页面即可查看活动任务概览
- **自动刷新**：2秒自动刷新任务状态（页面）、3秒刷新导航栏Badge

#### 识别诊断信息（任务详情/历史详情对话框新增 Tab）

每条**识别类任务**（`SingleSourceIdentificationTask` 与 `BatchSourceIdentificationTask` 子任务）执行期间，框架自动收集一份「识别诊断快照」并通过两个入口暴露给前端：

- **`TaskHistoryDetailsDialog`**（执行历史 → 详情）：日志 Tab 之后多了「识别诊断」Tab，反序列化自 `TaskExecutionInfo.IdentificationDiagnosticsJson`
- **`TaskDetailsDialog`**（运行中任务 → 详情）：执行日志后追加「识别诊断」面板，引用 `TaskProgress.IdentificationDiagnostics`，运行中也能实时刷出已完成的网站尝试

诊断内容包含：
- **关键词解析快照**：`ProductCode` / `CircleName` / `PrimaryKeyword` / `SecondaryKeywords` / `CleanedTitle` / `DetectedLanguage`
- **每次网站尝试**：网站名 + 状态（Success / NoMatch / Skipped / Exception / CacheHit）+ 来源（搜索 / ID 直查 / 缓存）+ 用时 + 跳过/异常原因
- **Top 5 候选**：每个候选的网站特定 ID、标题、相关性得分、命中查询关键词；最终被采用那条会高亮 + ✓
- **总扫描数 / 因低于 `identification.min_similarity` 被过滤数**

**实现要点**：
- 收集机制：`IdentificationDiagnosticsContext`（`AsyncLocal<IdentificationDiagnostics?>`，`Core/Services/Tasks/Diagnostics/`），`SingleSourceIdentificationTask` 入口 `BeginScope`，`WebsiteService` / `IWebsite` 实现链路在关键节点 `RecordKeywords` / `BeginAttempt` / `RecordCandidates` / `MarkChosen` / `EndAttempt`，无需修改任何接口签名
- 持久化：**仅内存**，跟现有 `TaskExecutionInfo` 一致（重启即丢、上限 1000 条），不进数据库；前端通过 `TaskExecutionInfo.GetIdentificationDiagnostics()` 取得
- 视图组件：`Components/Tasks/IdentificationDiagnosticsView.razor[.cs]` + `wwwroot/css/components/diagnostics.css`（类名 `diag-*` 前缀）

**数据流向（识别成功路径）**：
```
SingleSourceIdentificationTask.ExecuteAsync
  └─ BeginScope(diagnostics) + AttachDiagnostics 到 TaskProgress
       └─ WebsiteService.GetMediaInfoAsync
            ├─ RecordSkippedWebsite（分类不支持）
            ├─ BeginAttempt(name, ById/Search) → SafeInvokeWebsiteAsync
            │     └─ DLsite/Bangumi/Steam Search 类
            │          ├─ RecordKeywords（首站为准）
            │          ├─ RecordCandidates（quote PriorityQueue 转 Top 5）
            │          └─ MarkChosen（选中候选时高亮）
            └─ EndAttempt（自动写 Status/Duration/Reason，命中时设 FinalChoice）
  └─ result.ResultData["identificationDiagnostics"] = diagnostics
       └─ UnifiedTaskService.UpdateExecutionHistory
            └─ TaskExecutionInfo.IdentificationDiagnosticsJson = JsonSerializer.Serialize(diagnostics)
```

#### 后台任务页面 (`/tasks/background`)
- **统计概览**：显示运行中任务数、总处理数、失败数、最长运行时间四项统计指标
- **任务详情卡片**：每个后台任务显示完整信息，包括任务名称、监控路径、运行时长、处理统计
- **状态指示**：运行中的任务显示脉冲动画和绿色状态标签
- **操作按钮**：支持查看任务详情、停止任务，停止前显示确认对话框
- **路径快捷操作**：点击监控路径可在文件管理器中打开对应目录
- **空状态展示**：无后台任务时显示友好的空状态提示
- **自动刷新**：每5秒自动刷新任务状态

#### 单个标签管理页面 (`/tag/{tagId}`)
- **直接编辑**：无需弹窗，直接在页面编辑标签名称和描述
- **媒体展示**：完整展示标签关联的所有媒体，支持网格/列表视图切换
- **现代化设计**：采用卡片布局，参考FavoritesPage的设计风格
- **实时统计**：显示媒体数量、创建时间等统计信息
- **响应式布局**：适配各种设备尺寸，提供良好的用户体验

#### 标签映射管理页面 (`/tags/mappings`)
- **映射管理**：管理源标签名称到目标标签的映射关系，用于媒体识别时的标签自动匹配
- **统计面板**：显示总映射数、启用/禁用数量、命中次数、未使用映射数等统计信息
- **数据表格**：使用MudDataGrid展示映射列表，支持多选、分页、多列排序
- **搜索筛选**：支持按源名称、目标标签名称搜索，按启用状态筛选
- **单项操作**：编辑映射（修改源名称、目标标签、优先级等）、删除映射、切换启用状态
- **批量操作**：支持批量启用、批量禁用、批量删除选中的映射
- **清理功能**：一键清理从未使用或90天未使用的映射
- **入口位置**：从标签管理页面（/tags）顶部的"标签映射"按钮进入

#### 媒体库页面 (`/media/overview`)
- **统一入口**：将原有的分散媒体分类页面（视频、音频、图片、文本、游戏）合并为统一的媒体库页面
- **分类切换**：页面顶部显示可点击的分类统计卡片，点击可快速切换分类
- **切换过渡动画**：分类切换时内容区域先淡出（150ms），新内容挂载后自动淡入，体验更顺滑
- **URL参数支持**：支持 `/media/overview/{category}` 格式直接访问指定分类（如 `/media/overview/game`）
- **动态标题**：页面标题、图标、颜色根据当前选中分类动态变化
- **核心功能**：使用MediaShownView组件，支持筛选、排序、分页等完整功能
- **响应式设计**：分类选择卡片在不同屏幕尺寸下自动适配布局
- **手动新建媒体**：`PageHeader` 右侧"新建媒体"按钮（`PostAdd` 图标，Success 色）→ 先问"文件夹/单文件" → `FileExplorer` 选路径 → `ManualAddMediaHelper.OpenByPathAsync` 统一处理（重复检测 + 对话框 + 跳转编辑模式）

#### 媒体源详情页 (`/source/{id}`)
- **目录结构展示**：使用MudTreeView组件完整展示媒体源的目录树结构
- **文件高亮显示**：根据媒体类型自动高亮相关文件（如游戏高亮exe，视频高亮mp4等）
- **类别修改**：支持下拉修改媒体源类型（视频/音频/图片/文本/游戏）
- **入口文件选择**：根据类别筛选可用文件，支持保存入口文件路径并直接启动/打开
- **重新识别**：复用识别流程，支持配置识别选项、显示进度、确认入库
- **关联媒体显示**：已识别的媒体源显示关联媒体的封面、标题、摘要等信息
- **导航集成**：从MediaPage和UnknownPage均可跳转到媒体源详情页
- **远程访问检测**：自动检测是否为远程访问，远程时隐藏本地文件操作按钮

#### 页面头部组件 (`PageHeader`)
- **统一页面标题**：所有页面使用 `PageHeader` 组件渲染标题卡片，确保一致的视觉风格
- **参数**：`Icon`（图标）、`IconColor`（图标颜色）、`Title`（标题）、`Subtitle`（副标题文本）
- **插槽**：`SubtitleContent`（动态副标题）、`Actions`（右侧操作按钮区域）
- **响应式**：480px以下自动堆叠为两行布局，按钮全宽

#### 通用媒体筛选组件 (`MediaFilterDialog`)
- **适用范围广**：通过TopCategory参数支持所有媒体类型（音频、视频、游戏、图片、文本）
- **智能适配**：根据TopCategory自动调整对话框标题、图标、颜色和分类标签
- **完整筛选功能**：支持分类、标签、收藏夹、评分、日期范围等多维度筛选
- **可复用设计**：可在任何媒体类型的页面中使用，减少代码重复

#### 共享弹窗体系（`NineKgConfirmDialog` / `MediaKindPickerDialog` / 重做的 `ManualAddMediaDialog`）

取代项目里所有系统默认 `DialogService.ShowMessageBox` 的模板样，统一走一套"分类色 Hero 带 + 左侧 accent 色条 + 进入动画"的视觉骨架。共享 CSS 在 `wwwroot/css/components/dialogs.css`（在 `App.razor` 里排在 `common.css` 之后注册），所有类名带 `nk-dialog-` 前缀。

- **`NineKgConfirmDialog`**（`Components/Common/NineKgConfirmDialog.razor[.cs]`）—— 替代 19 处 `ShowMessageBox` 调用。支持四种 `ConfirmIntent` 变体：
  - `Info`（一般信息确认，primary 色）—— 如取消任务、停止后台任务、向量同步确认
  - `Affirmative`（积极操作，success 色）—— 如批量入库、批量加入识别队列
  - `Destructive`（单对象销毁，error 色 + 目标名预览卡 + "此操作不可撤销"警告行）—— 如删除媒体/创作者/社团/标签/映射/收藏夹/媒体源
  - `DestructiveBatch`（批量销毁，error 色 + Hero 右侧大号 count 数字）—— 如批量删除媒体源、批量删除映射、批量丢弃识别结果
  - 静态便利方法：`await NineKgConfirmDialog.ShowAsync(DialogService, title, message, intent: ..., confirmText: ..., targetName: ..., affectedCount: ...)` 返回 `bool`
  - 错误文案全部脱敏：日志用 Serilog 结构化字段记录 `ex`，用户侧只显示"操作失败，请稍后重试。"，消除原先的 `{ex.Message}` 泄漏
- **`MediaKindPickerDialog`**（`Components/Medias/MediaKindPickerDialog.razor[.cs]`）—— 替代 `MediaOverviewPage.HandleNewMediaAsync` 里原来的三选一 `ShowMessageBox`，做成可视化双卡片选择器：左卡 warning 色边框（文件夹），右卡 info 色边框（单文件）。卡片本身是 `<button>`，走 `role="button"` + 键盘可达；hover 有上浮 + 色向 glow。返回 `MediaKind?` 枚举（`Folder / File / null-取消`）
- **`ManualAddMediaDialog` 重做**（`Components/Medias/ManualAddMediaDialog.razor[.cs]`）—— 用上述骨架重构：
  - Hero 区高度 120px，随所选 TopCategory 从 default 色过渡到对应分类色（Video=primary / Audio=secondary / Picture=warning / Text=tertiary / Game=info）；Hero 里大图标、文件名、完整路径（点击复制）、元数据 pill（文件夹条目数 / 文件后缀）
  - 核心字段：Title + TopCategory 大号 chip 行，选中 chip 有 glow + translateY 位移
  - 选填手风琴"填更多信息（可选）"：具体分类（按当前 TopCategory 过滤的子分类）、简介（多行）、评分（MudRating）。手风琴头右侧 chip 显示"已填 N"
  - 智能跳转：用户只填核心两字段 → 返回 `ManualAddMediaResult(id, FullyFilled=false)` → 调用方跳 `/media/{id}?edit=true`；展开手风琴填了任一选填 → `FullyFilled=true` → 直跳 `/media/{id}` 不进入编辑态

新增同类"确认"弹窗时请统一调用 `NineKgConfirmDialog.ShowAsync(...)`，禁止再用 `DialogService.ShowMessageBox`。

## 📖 详细文档说明

### [开发者指南](development/README.md)
本地开发的入门文档，包含：
- **环境要求**与首次启动
- **数据库 / Hangfire / 调试技巧**
- **常见坑**（watch_folders 路径、OpenAI 代理、Selenium 等）

**适合人群**：贡献者、修 bug 或加功能的开发者

### [部署指南](operations/deployment.md)
运维与自托管的深度版（README 是简版），包含：
- **Docker** 反向代理 / HTTPS / 备份 / 升级
- **Windows 便携版**部署细节
- **故障排查**树（启动失败 / 识别异常 / 性能）

**适合人群**：自托管运维、想长期跑这套系统的人

### [媒体识别流程文档](architecture/media-identification-flow.md)
这是核心业务流程文档，包含：
- **详细的识别流程说明**
- **各组件职责和交互**
- **错误处理机制**
- **性能优化策略**

**适合人群**：核心开发人员、系统维护者

### [任务管理系统架构文档](architecture/task-management-system.md)
这是任务调度系统文档，包含：
- **统一任务服务架构**
- **5级优先级队列机制**
- **父子任务并发处理**
- **实时进度报告系统**
- **Hangfire集成详解**
- **任务开发指南**

**适合人群**：后端开发人员、系统架构师

## 🎨 UI/UX 设计

### 设计系统
项目采用基于 Material Design 的设计系统：
- **MudBlazor** 组件库确保一致性
- **模块化CSS** 架构支持主题定制
- **响应式设计** 适配多种设备

### CSS架构
```
wwwroot/css/
├── variables.css          # 设计令牌（颜色、间距、字体等）
├── utilities.css          # 原子化CSS工具类
├── global.css            # 全局基础样式
├── components/           # 组件级样式
└── pages/               # 页面级样式
```

## 🔧 开发指南

### 代码规范
- **语言要求**：注释和文档使用中文
- **文件组织**：.razor文件负责结构，.razor.cs负责逻辑
- **组件复用**：优先使用Components文件夹中的组件
- **样式管理**：CSS统一放在wwwroot/css目录

### 扩展指南
- **新媒体类型**：继承MediaBase，实现对应Service
- **新网站集成**：实现IWebsite接口，注册到DI容器（现有支持：DLsite、Bangumi、Steam）
- **新任务类型**：实现ITask接口，注册到任务工厂
- **新页面开发**：参考TagPage实现，使用MudBlazor组件和模块化CSS

### 识别网站支持
| 网站 | 分类 | 认证 | ID 格式 | 备注 |
|---|---|---|---|---|
| DLsite | Audio / Video / Game / Picture | 无 | `RJ01081508` | HTML 爬取 |
| Bangumi | Video / Game / Text / Picture | Bearer Token | `22905` | REST API |
| Steam | Game / Unknown | 无 | `730`（AppID） | 公开 Storefront API，无需密钥；`country_code` 推荐 `us`（勿用 `cn`，部分游戏对 CN 区未开放） |

## 🤝 贡献指南

### 开发流程
1. Fork 项目
2. 创建功能分支
3. 提交代码并编写测试
4. 更新相关文档
5. 提交 Pull Request

### 文档贡献
- 发现文档错误或不清晰的地方，欢迎提交Issue
- 新增功能需要同步更新文档
- 文档使用中文编写，保持风格一致

## 📞 联系方式

如果您在使用过程中遇到问题或有建议，欢迎：
- 提交 GitHub Issue
- 参与项目讨论
- 贡献代码和文档

---

## 📝 更新日志

### 2026-04-22 - 监控文件夹启动失效修复
- **问题**：
  1. 媒体源管理页能看到 `watch_folders` 列表，但后台任务页看不到对应监控任务
  2. 在 watch_folders 里建/删文件没有任何反应
  3. 从 UI 删除监视文件夹不会写回 `config.yaml`，重启又冒出来
- **根因**：启动监控与批量识别**强耦合**：
  - `FilesService.StartProcessConfiguredFolders` 只对每个 watch_folder 调 `IdentifyBatchMedia`，依赖批量识别成功后再起监控
  - `BatchSourceIdentificationTask.OnAllChildTasksCompletedAsync` 仅在 `failedCount == 0` 时提交 `FolderMonitorTask`——任一文件识别失败即跳过
  - `UnifiedTaskService.ExecuteParentTaskAsync` 子任务数为 0 时直接 return，**空文件夹**连 `OnAllChildTasksCompletedAsync` 都不会调用 → 监控永远起不来
  - `SourcesPage._deleteSourceFolder` 只改内存 `_sourceConfig`，没调 `Config.SaveConfig()`
- **修复**：**解耦**启动监控与批量识别
  - `StartProcessConfiguredFolders` 重写：对每个 watch_folder 先独立 `SubmitTaskAsync(new FolderMonitorTask(...))` 保证监控立即挂上，再调 `IdentifyBatchMedia(..., startMonitoringAfterCompletion: false)` 扫描历史文件
  - `BatchSourceIdentificationTask.OnAllChildTasksCompletedAsync` 去掉 `_batchResult.Success` 条件，部分失败不再阻止监控
  - `SourcesPage._deleteSourceFolder` 补 `Config.Source.WatchFolders = ...; await Config.SaveConfig();`
- **行为变化**：启动阶段即便 watch_folder 为空、识别全挂也能起监控；用户从 UI 删监视文件夹会真正持久化到配置

### 2026-04-18 - Hangfire 识别队列并发控制修复
- **问题**：`config.yaml` 的 `max_concurrent_identification_tasks: 5` 未生效，批量识别时仍然卡顿
- **根因**：`Program.cs` 注册了两个 `BackgroundJobServer`（主服务器 + 识别专用），但**主服务器的 Queues 数组也包含 `identification`**，导致 `ProcessorCount*2` 个主 worker 也在拉识别任务。实际识别并发 = `5 + ProcessorCount*2`（8 核 → 21 并发），5 的限制被完全绕过
- **修复**：主服务器 `Queues` 移除 `identification`，让识别专用服务器**独占**该队列。`MaxConcurrentIdentificationTasks` 从此真正锁定识别并发
- **Hangfire 并发控制的正确姿势**：想把某队列的并发锁到 N，需要"专用服务器 + 独占队列"—`WorkerCount=N` 且其他服务器的 `Queues` 不含该队列；单服务器的 `WorkerCount` 不能按队列细分
- **调整方式**：改 `config.yaml` 的 `tasks.max_concurrent_identification_tasks` 后**重启应用**生效（Hangfire `BackgroundJobServer` 只在启动时读取 `WorkerCount`）
- **回归面**：`critical`/`high`/`default`/`low`/`background` 队列仍由主服务器正常处理（`FolderMonitorTask`、`CacheCleanupTask`、`PendingIdentificationCleanupTask` 等不受影响）

### 2026-04-11 - 文件监控稳定性优化
- **新增 `FileStabilityChecker`**：
  - 文件/文件夹创建后，通过初始延迟 + 大小稳定性轮询 + 文件锁探测，确保完全写入后再处理
  - 自动去重，防止 `FileSystemWatcher` 重复事件导致多次处理
  - 支持 `CancellationToken`，停止监控时立即取消进行中的等待
- **MonitorService 改进**：
  - `OnFileCreated` 在处理前调用稳定性检测，解决粘贴文件夹时文件未完全复制就触发识别的问题
  - `MonitorTaskContext` 添加 `CancellationTokenSource`，停止监控时取消所有进行中的稳定性检测
  - `ProcessedCount`/`FailedCount` 改用 `Interlocked.Increment` 保证线程安全
- **FilesService 改进**：
  - `IsValidMediaSource` 增加目录支持：检查隐藏/系统属性和空目录
  - 修复扩展名检查对目录的误判（目录跳过扩展名检查）
  - 移除 `GetMediaByPath` 中的临时 `Task.Delay(1000)`

### 2026-03-26 - MediaPage 最终打磨 (Polish)
- **折叠面板布局统一**：
  - 图片面板从 `justify-space-between` 改为 `MudSpacer` + `@onclick:stopPropagation` 模式，与其他三个面板一致
  - 所有折叠面板展开/折叠图标统一添加 `Class="ml-2"` 间距
- **按钮内容一致性**：
  - 保存按钮移除 `<MudText>` 包裹，改为直接文本，与取消按钮模式一致
  - "保存修改" 简化为 "保存"
- **命名优化**：
  - "详情描述" → "详细描述"（消除"详情"一词在页面中的过度重复）
- **代码清理**：
  - `<MudSpacer></MudSpacer>` → `<MudSpacer/>` 自闭合标签
  - 移除 `MudCardContent` 与 `MudCard` 之间的空行
  - HTML注释与标题文案同步更新

### 2026-03-26 - MediaPage 文案清晰度优化 (Clarify)
- **按钮文案**：
  - "编辑模式" → "编辑"（动作描述更简洁）
  - "添加" → "添加关联"（相关媒体按钮更具体）
  - 添加链接对话框确认按钮 "添加" → "添加链接"
- **删除确认对话框**：
  - `确定要删除"xxx"吗？此操作不可撤销!` → `确定要删除「xxx」吗？删除后无法恢复。`（语气更平和，使用书名号）
  - 确认按钮 `删除!` → `确认删除`（去除感叹号）
- **Snackbar消息统一**：
  - 翻译相关：统一为 "简介翻译完成/失败" 和 "描述翻译完成/失败" 格式，区分操作对象
  - `翻译出错: {msg}` → `翻译失败: {msg}`（统一用"失败"而非混用"出错"）
  - `无法翻译空简介` → `简介为空，无法翻译`；`无法翻译空内容` → `描述为空，无法翻译`
  - `保存失败，请检查媒体信息` → `保存失败，服务端未返回更新数据`（具体原因）
  - 翻译失败补充原因：`翻译失败` → `简介/描述翻译失败，未获取到翻译结果`
- **空状态文案**：
  - 图片空状态：非编辑模式下不再显示"点击上方按钮添加图片"；编辑模式改为"点击上方「添加图片」按钮上传"
  - 简介空状态：`暂无简介` → `暂无简介，进入编辑模式添加`
  - 相关媒体空状态提示与按钮文本同步更新
- **标题副标题**：
  - 吸顶标题副标题从 `视频详情` 等冗余文案改为显示具体分类名

### 2026-03-26 - MediaPage 设计系统规范化 (Normalize)
- **消除所有内联样式**：
  - 页面仅剩1处动态`background-image`（必须内联），其余全部提取为CSS类
  - 新增CSS类：`media-empty-state`, `media-images-empty`, `media-preview-image`, `media-sidebar-text`(增强)
  - `style="min-width: 0"` (5处) → 新增工具类 `.min-w-0` 到 utilities.css
  - `style="height: 100%"` → MudBlazor内置 `mud-height-full`
  - `style="width: 100%"` → MudBlazor内置 `mud-width-full`
  - `style="word-break: break-all"` → `.media-sidebar-text` CSS类
  - `Style="height: 70vh"` → `.media-empty-state` CSS类
  - `Style="height: 200px"` → `.media-images-empty` CSS类
  - `Style="max-width/max-height: 100%"` → `.media-preview-image` CSS类
- **硬编码值替换为设计令牌**：
  - `box-shadow: 0 4px 6px...` (3处) → `var(--shadow-sm)` / `var(--shadow-lg)`
  - `border-left: 2px solid` (5处) → `var(--border-width-medium) solid`
  - `height: 2px` → `var(--border-width-medium)`
  - `height: 1px` → `var(--border-width-thin)`
  - `right: 10px; bottom: 5px` → `var(--spacing-md)` / `var(--spacing-xs)`
  - `padding-right: 2px` → `var(--spacing-xs)`
  - `height: 24px` (渐变遮罩) → `var(--spacing-2xl)`
  - `margin: 2px` → `var(--spacing-xs)`
- **消除重复定义**：
  - 移除 media.css 中重复的 `.left-sidebar` 定义（已由 cards.css 的 `.card-sidebar` 管理）
  - hover选择器从 `.left-sidebar:hover` 改为 `.media-sidebar .card-sidebar:hover`
- **新增全局工具类**：
  - `utilities.css` 新增 `.min-w-0` (`min-width: 0`) 工具类

### 2026-03-26 - MediaPage 响应式适配 (Adapt)
- **吸顶标题适配**：
  - 新增 `media-header-row` / `media-header-actions` CSS类，平板端按钮换行堆叠
  - 手机端隐藏头像(`.media-header-avatar`)、标题改为两行截断、编辑按钮全宽
- **骨架屏重构**：
  - 骨架屏结构与实际页面布局一致（标题栏+侧边栏+右侧内容），使用响应式高度
  - 添加标签骨架pill、分区骨架，提升加载体验
- **相关媒体移动端**：
  - 新增 `media-related-scroll` 类，768px以下从换行变为水平滚动，防止卡片挤压变形
- **触屏适配**：
  - 768px以下编辑模式链接删除按钮始终可见（不再依赖hover显示）
  - 折叠面板标题添加 `media-collapse-header` 类，手机端min-height 48px保证触控区域
- **图片预览适配**：
  - 768px以下图片预览容器从70vh降为50vh，横屏手机友好
- **空状态适配**：
  - 新增 `media-empty-state` 类，480px以下从固定70vh改为min-height 50vh + padding

### 2026-03-26 - MediaPage 健壮性加固 (Harden)
- **键盘无障碍**：
  - 四个折叠面板添加 `@onkeydown` 处理，支持 Enter/Space 键展开/折叠
  - 所有对话框统一添加 `CloseOnEscapeKey = true`（日期选择、添加链接）
- **双击防护**：
  - 删除操作添加 `_deleteProcessing` 状态锁，防止重复触发
  - 翻译操作添加 `_translating` 前置检查，防止并发翻译
- **文本溢出保护**：
  - 吸顶标题添加 `media-header-title` CSS类，超长标题自动截断省略
  - 标题div添加 `min-width: 0` 防止flex布局溢出
  - 侧边栏 `flex: 1` 容器统一改为 `flex-grow-1` + `min-width: 0`
  - `.media-title` 添加 `word-wrap` 和 `overflow-wrap` 保护
- **滚动提示**：
  - 链接容器添加 `links-container-wrapper` 包裹层，链接超过3个时底部显示渐变遮罩提示还有更多内容
- **减少动画偏好**：
  - 添加 `@media (prefers-reduced-motion: reduce)` 规则，为所有页面特定动画提供无动画回退
- **空状态优化**：
  - "暂无相关链接"在非编辑模式下不再显示"点击+添加"（因为+按钮仅编辑模式可见）
  - 媒体不存在错误页增加媒体ID显示和"浏览媒体库"按钮，提供更多导航选择

### 2026-03-26 - MediaPage 质量审计修复
- **Bug修复**：
  - 修复 `CategoryChanged` 方法逻辑bug：分类赋值后再比较导致条件永远为false，媒体类型转换逻辑无法执行
  - 修复评分显示格式：int类型的`_rating`使用`"0.0"`格式导致永远显示`.0`后缀
- **无障碍改进**：
  - 四个折叠面板标题添加 `role="button"` `tabindex="0"` `aria-expanded` ARIA属性
  - 图片预览对话框添加alt文本和ESC键关闭支持
  - 海报图片添加alt文本
- **响应式优化**：
  - 海报容器、图片轮播从固定高度改为CSS类，支持960px/768px/480px三级响应式断点
  - 提取10+处内联style到media.css作为命名类（media-poster-container, media-carousel等）
- **设计系统一致性**：
  - 吸顶标题 z-index 从硬编码`100`改为使用全局变量`var(--z-sticky)`
  - 详情页卡片hover效果移除`translateY(-2px)`位移，仅保留shadow变化，减少交互时视觉抖动
  - 统一对话框按钮顺序：取消在左，主操作在右
- **代码质量**：
  - 5个事件回调方法去除无用的`async`关键字，改为返回`Task.CompletedTask`
  - 标签颜色哈希从`GetHashCode()`改为确定性字符求和，确保跨会话颜色一致
  - 翻译截断提示改为显示具体字数（如"原文共X字，仅翻译前4000字"）
  - 移除侧边栏底部多余的MudDivider

### 2026-03-12 - MediaPage 编辑模式改造
- **编辑模式设计**：
  - 新增 `_isEditMode` 状态变量，默认 `false`（只读浏览模式）
  - 顶部按钮区：浏览时显示"编辑模式"按钮，编辑时显示"取消"+"保存修改"按钮
  - 保存成功或取消时，自动退出编辑模式并重载数据
  - `FavoriteSelector`（收藏）不受编辑模式影响，始终可用
- **只读保护的元素**：
  - 删除按钮（MudFab）：仅编辑模式可见
  - 更换封面按钮：仅编辑模式可见
  - 评分星星（MudRating）：仅编辑模式可点击
  - 日期编辑图标：仅编辑模式可见
  - 分类：浏览时显示 MudChip，编辑时显示 CategorySelector
  - 链接添加/删除按钮：仅编辑模式可见
  - 简介：浏览时显示 MudText，编辑时显示 MudTextField
  - 翻译按钮（简介/详情）：仅编辑模式可见
  - 描述编辑/预览按钮：仅编辑模式可见
  - 相关媒体添加/删除按钮：仅编辑模式可见
- **组件参数扩展**：
  - `EditableAliasList` 新增 `IsEditable` 参数（默认 true）
  - `TagSelector` 新增 `IsEditable` 参数（默认 true）
  - `InfoEditor` 新增 `IsEditable` 参数（默认 true）
  - `DescriptionEditor` 新增 `ExitEditMode()` 公开方法
  - `EditableCreatorList` 已有 `Editable` 参数，MediaPage 中统一传入 `_isEditMode`

### 2026-03-10 - Circle 社团管理功能
- **CreatorService扩展**（Circle相关）：
  - 新增 `GetAllCirclesAsync()` 获取所有社团
  - 新增 `SearchCirclesByNameAsync()` 按名称/别名搜索社团
  - 新增 `CreateCircleAsync()` 快速创建社团
  - 新增 `GetCircleCountAsync()` 获取社团总数
  - 新增 `DeleteCircleAsync()` 删除社团
  - 新增 `GetCircleMediasAsync()` 获取社团关联媒体
  - 新增 `UpdateCircleMediasAsync()` 更新社团关联媒体列表
- **CirclesPage页面**（`/circles`）：
  - 页面标题卡片显示总数和添加按钮
  - 搜索框（300ms防抖）+ 刷新按钮
  - 社团网格（xs=12 sm=6 md=4 lg=3）
  - hover显示删除按钮，点击进入详情页
  - 添加社团对话框（仅名称）
- **CirclePage详情页增强**：
  - MediaShownView 添加 `@ref` 引用
  - 关联作品区域右上角显示编辑图标
  - 点击编辑打开 MediaSelectorDialog，选择后更新并刷新列表
- **NavMenu导航**：在"创作者"后添加"社团"入口

### 2026-02-03 - Creator管理功能
- **CreatorService扩展**：
  - 新增 `GetAllCreatorsAsync()` 方法获取所有Creator
  - 新增 `SearchCreatorsByNameAsync()` 方法按名称和别名搜索
  - 新增 `CreateCreatorAsync()` 方法快速创建Creator
  - 新增 `GetCreatorCountAsync()` 方法获取Creator总数
  - 新增 `GetCreatorsByTypeAsync()` 方法按类型筛选
  - 新增 `DeleteCreatorAsync()` 方法删除Creator
- **CreatorSelectorDialog组件**：
  - 支持搜索框（300ms防抖）和类型筛选
  - 列表展示Creator（头像+名称+类型标签）
  - 支持多选/单选模式
  - 已选择预览和快速移除
  - 内置新增Creator面板，可直接创建并添加
- **EditableCreatorList组件**：
  - 继承CreatorList的展示样式
  - 添加"+"按钮打开CreatorSelectorDialog
  - 每个Creator显示删除按钮（hover时显示）
  - 支持EventCallback通知变更
  - 可配置FilterByType限制可选类型
- **CreatorsPage页面**（`/creators`）：
  - 页面标题卡片显示总数和添加按钮
  - 搜索框支持300ms防抖
  - 按类型下拉筛选
  - Creator卡片网格展示（头像+名称+类型标签）
  - 点击卡片跳转到CreatorPage详情页
  - 添加对话框支持名称输入和类型多选
  - hover时显示删除按钮
- **MediaPage增强**：
  - 所有CreatorList替换为EditableCreatorList
  - 支持直接在媒体详情页添加/删除创作者
  - 按媒体类型预设FilterByType筛选
- **导航菜单**：
  - 在"标签管理"后添加"创作者"导航链接

### 2026-01-17 - 任务管理页面布局重构
- **布局优化**：
  - 将 TasksPage 从左右布局改为上下布局，整体更简洁美观
  - 移除标题卡片中的"执行历史"按钮，简化页面头部
- **统计与导航区域重构**：
  - 全宽4列统计卡片：总执行数、成功率、后台任务、定时任务
  - 后台任务和定时任务卡片改为可点击，直接跳转到对应管理页面
  - 底部添加"查看执行历史"按钮，统一入口
- **运行中任务区域**：
  - 改为全宽布局，更好利用屏幕空间
  - 保持原有的任务树展示功能
- **新增依赖**：
  - TasksPage.razor.cs 注入 ScheduledTaskFactory 以获取定时任务数量

### 2026-01-17 - 后台任务独立页面
- **新增后台任务页面**（`/tasks/background`）：
  - 将原 TasksPage 中的"后台任务"部分移动到独立页面
  - 统计概览：显示运行中任务数、总处理数、失败数、最长运行时间
  - 任务卡片：展示任务名称、监控路径、运行时长、处理统计和进度条
  - 状态动画：运行中任务显示脉冲动画效果
  - 路径操作：点击监控路径可在文件管理器中打开
  - 停止确认：停止任务前显示确认对话框
  - 空状态：无后台任务时显示友好提示
- **新增文件**：
  - `BackgroundTasksPage.razor` - 后台任务页面视图
  - `BackgroundTasksPage.razor.cs` - 页面后端逻辑
- **修改文件**：
  - `NavMenu.razor` - 添加"后台任务"导航菜单项
  - `TasksPage.razor` - 后台任务部分改为链接卡片，显示任务数量并跳转到独立页面
  - `TasksPage.razor.cs` - 保留后台任务数量统计，移除详细操作方法

### 2026-01-16 - 定时任务配置页面
- **新增定时任务配置页面**（`/tasks/scheduled`）：
  - 将原 Settings 页面中的定时任务配置移至独立页面
  - 放置在任务导航组下，与任务概览、执行历史同级
  - 页面功能：
    - 任务列表展示：显示所有注册的定时任务，包括任务名称、描述、启用状态
    - 启用/禁用开关：实时切换任务启用状态
    - Cron 表达式编辑：支持编辑任务执行周期，带格式帮助对话框
    - 下次执行时间：根据 Cron 表达式自动计算显示
    - 上次执行时间：显示任务最后执行时间
    - 手动执行按钮：支持立即手动触发任务
    - 保存配置：统一保存所有任务配置
- **新增文件**：
  - `ScheduledTasksPage.razor` - 定时任务配置页面视图
  - `ScheduledTasksPage.razor.cs` - 页面后端逻辑
- **修改文件**：
  - `NavMenu.razor` - 添加"定时任务"导航菜单项
  - `Settings.razor` - 移除定时任务配置部分，添加链接到新页面
  - `Settings.razor.cs` - 移除相关验证逻辑

### 2026-01-15 - 定时任务系统重构
- **架构简化**：
  - 使用 `[ScheduledTask]` 特性作为任务元数据的单一事实来源
  - `ScheduledTaskFactory` 通过反射自动发现所有标记特性的任务类
  - 移除硬编码的任务注册和手动映射逻辑
- **新增文件**：
  - `ScheduledTaskAttribute.cs` - 定时任务元数据特性和 `ScheduledTaskMetadata` 记录类
- **修改文件**：
  - `ScheduledTaskFactory.cs` - 重构为反射自动发现模式
  - `UnifiedTaskService.cs` - 简化 `ExecuteScheduledTaskAsync`，移除 `ParseTaskType()` 方法
  - `ServiceCollectionExtensions.cs` - 移除已废弃的 `InitializeScheduledTasks()` 调用
  - `Settings.razor.cs` - 更新任务调用名称
  - `config.yaml` - 将 `VectorSync` 拆分为 `TagVectorSync` 和 `MediaVectorSync` 两个独立任务
- **任务特性标记**：
  - `TagVectorSyncTask` - `[ScheduledTask("TagVectorSync", "标签向量同步", TaskType.TagVectorSync)]`
  - `MediaVectorSyncTask` - `[ScheduledTask("MediaVectorSync", "媒体向量同步", TaskType.MediaVectorSync)]`
  - `CacheCleanupTask` - `[ScheduledTask("CacheCleanup", "缓存清理", TaskType.CacheCleanup)]`
  - `MediaCleanupTask` - `[ScheduledTask("MediaCleanup", "媒体清理", TaskType.MediaCleanup)]`
- **配置变更**：
  - config.yaml 中的 `type` 字段现在直接对应任务特性的 `Key`
  - 例如：`type: TagVectorSync` 对应 `[ScheduledTask("TagVectorSync", ...)]`

### 2026-01-14 - AI向量配置重构
- **配置结构简化**：
  - 将分散在4个配置类中的向量相关配置统一到 `ai.vector` 节点下
  - 合并原有冗余配置项：
    - `media.enable_vector_indexing` + `search.vector_search.enable_for_media` → `ai.vector.media.enable`
    - `tag_matching.enable_vector_matching` + `search.vector_search.enable_for_tags` → `ai.vector.tag.enable`
    - `ai.enable_vector_storage` → `ai.vector.enable`
    - `ai.vector_db` → `ai.vector.db`
  - 移除 `search.vector_search` 配置节点
- **新的配置结构**：
  - `ai.vector.enable` - 向量功能总开关
  - `ai.vector.media.enable` - 媒体向量开关
  - `ai.vector.media.min_similarity` - 媒体搜索最小相似度
  - `ai.vector.tag.enable` - 标签向量开关
  - `ai.vector.tag.similarity_threshold` - 标签匹配相似度阈值
  - `ai.vector.tag.search_top_k` - 标签搜索返回数
  - `ai.vector.search.weight` - 向量搜索权重
  - `ai.vector.db.*` - 向量数据库配置
- **配置变更自动同步**：
  - 在设置页面保存AI配置时，自动检测向量功能从禁用变为启用的情况
  - 弹窗询问用户是否立即同步数据
  - 用户确认后自动触发 TagVectorSync 和 MediaVectorSync 定时任务
- **新增文件**：
  - `VectorConfig.cs` - 统一的向量配置类，包含 MediaVectorConfig、TagVectorConfig、VectorSearchSettings
- **修改文件**：
  - `AIConfig.cs` - 添加 Vector 属性
  - `MediaConfig.cs` - 移除 EnableVectorIndexing
  - `TagMatchingConfig.cs` - 移除向量相关配置项
  - `SearchConfig.cs` - 移除 VectorSearch 配置
  - `Settings.razor` - 重构向量配置界面，全部移入AI配置标签页
  - `Settings.razor.cs` - 添加配置变更检测和同步触发逻辑
  - 更新所有读取旧配置路径的服务类

### 2026-01-14 - 数据库配置迁移
- **配置统一管理**：
  - 将数据库连接字符串配置从 `appsettings.json` 迁移到 `Config/config.yaml`
  - 新增 `DatabaseConfig` 配置类，管理数据库配置
  - 简化配置格式，直接配置数据库文件路径：`path`（默认数据库）和 `hangfire_path`（Hangfire数据库）
- **新增文件**：
  - `DatabaseConfig.cs` - 数据库配置模型类，提供 `GetConnectionString()` 方法自动构建连接字符串
- **修改文件**：
  - `Config.cs` - 添加 Database 属性
  - `config.yaml` - 添加 database 配置节点
  - `Program.cs` - 改用 config.Database 获取数据库配置
  - `appsettings.json` - 移除 ConnectionStrings 配置

### 2026-01-08 - 认证系统改进：数据库存储
- **安全性提升**：
  - 用户凭据从配置文件改为存储在SQLite数据库中
  - 密码使用PBKDF2-SHA256安全哈希（ASP.NET Core Identity），替代Base64编码
  - 自动处理盐值生成和存储，支持算法升级检测
- **初始账号逻辑**：
  - 优先读取环境变量 `NT_USER` 和 `NT_PASSWORD`
  - 如果环境变量不存在，使用默认账号 `admin/admin`
  - 应用启动时自动检测并创建初始用户
- **账号管理功能**：
  - 设置页面新增"账号管理"标签页
  - 修改用户名：需要验证原密码
  - 修改密码：需要验证原密码 + 确认新密码
  - 修改后自动登出，需重新登录
- **新增文件**：
  - `User.cs` - 用户实体模型
  - `PasswordService.cs` - 密码哈希服务（PBKDF2）
  - `UserInitializationService.cs` - 应用启动时初始化默认用户
- **修改文件**：
  - `AuthService.cs` - 重写为异步数据库验证
  - `MediaDbContext.cs` - 添加Users表
  - `AuthController.cs` - 添加修改账号/密码API
  - `AppConfig.cs` - 移除LoginUser和LoginPassword配置
  - `Settings.razor` - 添加账号管理UI
- **API变更**：
  - `POST /api/auth/change-username` - 修改用户名
  - `POST /api/auth/change-password` - 修改密码
  - `GET /api/auth/current-user` - 获取当前用户信息

### 2026-01-06 - 登录认证系统
- **新增登录认证机制**：
  - 实现完整的Cookie认证系统，保护所有页面
  - 新增登录页面（`/login`），使用MudBlazor组件构建
  - 登录专用布局（LoginLayout），无侧边栏的简洁界面
  - 支持"记住我"功能，延长Cookie有效期至30天
  - 用户名和密码（Base64编码）存储在配置文件中
- **新增文件**：
  - `AuthService.cs` - 认证服务，验证用户凭据
  - `CookieAuthenticationStateProvider.cs` - 自定义认证状态提供者
  - `AuthController.cs` - 登录/登出API控制器
  - `LoginLayout.razor` - 登录专用布局
  - `Login.razor` + `Login.razor.cs` - 登录页面
  - `RedirectToLogin.razor` - 未授权重定向组件
  - `login.css` - 登录页面样式
- **登出功能**：
  - 在MainLayout导航栏添加登出按钮
  - 点击登出清除认证Cookie并跳转到登录页
- **路由保护**：
  - 修改Routes.razor使用AuthorizeRouteView
  - 未登录用户自动重定向到登录页

### 2026-01-05 - 标签映射管理页面
- **新增标签映射管理页面**（`/tags/mappings`）：
  - 使用MudDataGrid表格展示所有标签映射，支持多选、分页、多列排序
  - 统计信息面板显示总数、启用、禁用、命中次数、未使用数量
  - 支持按源名称/目标标签搜索，按启用状态筛选
  - 单项操作：编辑、删除、切换启用状态
  - 批量操作：批量启用、批量禁用、批量删除
  - 清理功能：一键清理未使用的映射
- **标签映射编辑对话框**（TagMappingEditorDialog）：
  - 支持添加和编辑映射
  - 源名称输入、目标标签选择（复用TagSelectorDialog）、优先级设置、启用状态开关、描述说明
  - 自动验证源名称重复
- **TagSelectorDialog增强**：
  - 支持两种使用方式：EventCallback（嵌入使用）和IMudDialogInstance（DialogService使用）
- **TagsPage入口按钮**：
  - 在标签管理页面顶部添加"标签映射"按钮，快速进入映射管理页面

### 2026-01-03 - 媒体源详情页与文件浏览器服务
- **媒体源详情页**（`/source/{id}`）：
  - 新增 SourceDetailPage 页面，展示单个媒体源的完整信息
  - 使用 MudTreeView 组件展示目录树结构，支持延迟加载
  - 根据媒体类型高亮显示相关文件（游戏→exe，视频→mp4等）
  - 支持下拉修改媒体源类型（视频/音频/图片/文本/游戏）
  - 入口文件选择功能，支持保存路径并直接启动/打开
  - 已识别媒体源显示关联媒体的封面、标题、摘要等
  - 从 MediaPage 和 UnknownPage 均可跳转到详情页
- **MediaSource 模型扩展**：
  - 新增 `Id` 自增主键字段（FullPath 改为唯一索引）
  - 新增 `EntryFilePath` 字段，持久化存储入口文件路径
  - 新增 `GetSourceLink()` 方法生成详情页链接
- **目录树组件**（DirectoryTreeView）：
  - 支持参数：RootPath、HighlightCategory、OnFileSelected、SelectedFilePath、MaxDepth
  - 文件大小显示、选中状态高亮、类别相关文件标记
- **文件浏览器服务重构**：
  - 新增 IFileExplorerService 接口，支持跨平台文件操作
  - 实现 Windows、Mac、Linux、Remote 四种平台服务
  - 自动检测本地/远程访问，远程时返回 NotSupported
  - 使用工厂模式（FileExplorerServiceFactory）动态创建服务
  - 删除旧的 IFileSystemService 和 WebFileSystemService

### 2025-12-24 - 任务日志功能增强
- **任务执行日志系统**：
  - 新增 `TaskLogEntry` 日志条目模型，支持 Debug/Info/Warning/Error/Success 五个级别
  - 新增 `TaskLogBuffer` 环形缓冲区，自动限制日志条数（默认200条）
  - 扩展 `IProgressReporter` 接口，新增 `LogAsync`、`LogDebugAsync`、`LogInfoAsync`、`LogWarningAsync`、`LogErrorAsync`、`LogSuccessAsync` 方法
- **日志持久化**：
  - `TaskProgress` 类集成日志缓冲区，运行时收集日志
  - `TaskExecutionInfo` 和 `TaskExecutionRecord` 新增 `LogEntriesJson` 字段，支持日志持久化到数据库
  - 任务完成后自动序列化日志并保存到执行历史
- **前端日志查看组件**：
  - 新增 `TaskLogViewer` 组件，终端风格的日志展示界面
  - 支持按日志级别筛选、自动滚动、日志计数显示
  - 不同级别使用不同颜色高亮显示
- **对话框集成**：
  - `TaskDetailsDialog`：运行中任务可查看实时日志
  - `TaskHistoryDetailsDialog`：新增"执行日志"标签页查看历史日志
- **识别任务日志增强**：
  - `SingleSourceIdentificationTask`、`BatchSourceIdentificationTask` 自动记录执行日志
  - `FilesService.GetMediaByPath` 增加细粒度日志记录
- **网站服务层日志传递**：
  - `IWebsite` 接口 `GetMediaInfoAsync` 方法新增 `IProgressReporter` 可选参数
  - `WebsiteService` 在遍历网站时记录详细日志（尝试网站、使用缓存、搜索结果等）
  - `DLsiteService` 实现细粒度日志（代码提取、搜索、页面抓取等）
  - `BangumiService` 实现细粒度日志（搜索、匹配、条目获取等）
- **配置项**：


### 2025-12-23 - 媒体库页面重构
- **统一媒体库入口**：
  - 将原有分散的媒体分类页面（GamePage、VideoPage、AudioPage、PicturePage、TextPage）合并为统一的MediaOverviewPage
  - 新页面路由：`/media/overview` 和 `/media/overview/{category}`
  - 删除旧的分类页面文件夹（Games、Videos、Audios、Pictures、Texts）
- **分类选择功能**：
  - 页面顶部显示可点击的分类统计卡片，参考UnknownPage的设计
  - 支持全部、视频、音频、图片、文本、游戏六种分类切换
  - 选中状态使用 `card-bordered-{color}` 样式高亮显示
- **导航菜单简化**：
  - "库"导航组简化为单一"媒体库"入口
  - 移除原有的分类子链接
- **首页链接更新**：
  - 更新 `GetCategoryLink` 方法，指向新的路由格式
  - 更新"查看更多"按钮链接

### 2025-12-22 - 未识别媒体源管理页面
- **新增UnknownPage页面**（`/source/unknown`）：
  - 使用MudDataGrid表格展示所有未处理的媒体源（Processed=false）
  - 统计信息面板显示总数及各类型数量，支持点击快速筛选
  - 支持按类型下拉筛选和路径关键字搜索
  - 单项操作：手动识别、加入识别队列、打开文件位置、删除
  - 批量操作：支持多选后批量删除、批量加入识别队列
  - 自动检测远程访问，远程时隐藏"打开文件位置"功能
  - 复用SourcePage的识别逻辑，完整支持识别选项配置和进度显示
- **路由修复**：将页面路由从`/unknown`修改为`/source/unknown`，与导航菜单配置一致

### 2025-12-18 - 执行历史页面优化
- **页面标题风格统一**：
  - 执行历史页面标题卡片风格与其他页面（TasksPage、SourcePage）统一
  - 使用 `card-base card-header mb-4` 类，调整布局和按钮样式
  - 将"清理历史"按钮替换为"返回任务管理"和"刷新"按钮
- **失败任务重新手动识别功能**：
  - 在 `TaskExecutionInfo` 中新增 `SourcePath` 字段，用于存储识别任务的源路径
  - 修改 `UnifiedTaskService.UpdateExecutionHistory` 从 `TaskResult.ResultData` 中提取 `filePath`
  - 失败的单文件识别任务（`SingleSourceIdentification`）操作栏显示"重新手动识别"按钮
  - 点击按钮弹出识别选项配置对话框，配置后执行实时识别并显示结果
  - 识别成功后可选择是否添加到数据库

### 2025-12-18 - 任务类型枚举重构
- **TaskType 从字符串改为枚举**：
  - 重构 `ITask.TaskType` 从 `string` 改为 `TaskType` 枚举类型
  - 重新设计 TaskType 枚举分类：识别任务、后台任务、定时任务、其他任务
  - 在 `TaskResult`、`TaskExecutionInfo`、`TaskExecutionRecord` 中添加 `TaskType` 属性
  - 创建 `TaskTypeExtensions` 扩展方法提供 `GetDisplayName()` 和 `GetCategory()`
- **前端筛选优化**：
  - 任务执行历史页面的任务类型筛选从文本输入改为枚举下拉框选择
  - 在历史记录表格中新增任务类型列，使用 MudChip 显示中文名称
- **新增任务类型**：
  - `SingleSourceIdentification`：单文件识别
  - `BatchSourceIdentification`：批量文件识别
  - `FolderMonitor`：文件夹监控
  - `CacheCleanup`、`MediaCleanup`、`TagVectorSync`、`MediaVectorSync`：定时任务
  - `Custom`：自定义任务
- **ScheduledTaskFactory 改进**：
  - 使用 TaskType 枚举作为任务注册键，提供更好的类型安全

### 2025-12-16 - 任务重试代码鲁棒性增强
- **ExecuteTaskAsync方法重构**：
  - 添加`finally`块保证元数据清理的可靠性
  - 引入`shouldCleanup`标志控制元数据清理时机
  - 提取`ScheduleRetryAsync`辅助方法减少代码重复
  - 删除不再使用的`HandleTaskFailureAsync`方法
  - 无论代码执行路径如何，元数据清理都会在适当时机执行
  - 确保重试任务的元数据不会被意外清理

### 2025-12-15 - 任务重试逻辑修复
- **任务重试系统重构**：
  - 修复任务失败后重试时"未找到任务元数据"的错误
  - 禁用Hangfire默认的10次重试，改用项目自定义重试逻辑
  - 重试次数由配置文件`config.yaml`中的`retry_count`控制（默认3次）
  - 实现指数退避重试策略（5s → 10s → 20s → 40s）
  - 任务元数据仅在任务最终完成或达到最大重试次数后才清理
- **新增Retrying任务状态**：
  - `TaskExecutionStatus`枚举新增`Retrying`状态
  - `TaskProgress`模型新增`CurrentRetry`、`MaxRetries`、`RetryInfo`字段
  - `TaskMetadataStore`新增重试计数管理功能
- **前端重试状态显示**：
  - `TaskTreeNode`组件支持显示重试状态（黄色Replay图标）
  - 状态文本显示重试进度（如"重试中 (2/3)"）
  - 重试中的任务可以被取消
  - 进度条在重试状态下也会显示

### 2025-12-01 - 识别进度对话框优化
- **IdentificationLoadingDialog组件**：
  - 新增专用的媒体识别进度对话框，替换原有的通用LoadingDialog
  - 显示实时进度条（MudProgressLinear）和百分比
  - 显示当前识别消息和处理项
  - 保留取消按钮功能，支持用户随时取消识别
  - 通过DialogProgressReporter实现进度报告器，订阅进度事件更新UI
  - 线程安全的进度更新机制（使用InvokeAsync确保UI线程更新）

### 2025-12-01 - 文件浏览器排序功能
- **SourcePage和FileExplorer排序工具栏**：
  - 新增排序工具栏，支持按名称、修改日期、类型排序
  - 点击同一排序字段切换升序/降序
  - 显示排序方向图标（上箭头/下箭头）
  - 显示当前目录的文件夹和文件数量统计
  - 文件夹和文件分别独立排序

### 2025-11-23 - 识别取消功能
- **识别取消支持**：
  - 前端LoadingDialog组件新增取消按钮，支持用户取消识别操作
  - 全面集成CancellationToken，从前端到后端完整支持取消机制
  - WebsiteService和FilesService添加CancellationToken参数支持
  - 任务层（SingleSourceIdentificationTask、ManualIdentificationTask）连接CancellationToken传递
  - 识别卡住时不再阻塞界面，用户可随时取消并恢复操作
  - 优雅处理OperationCanceledException，提供友好的取消提示
  - 在关键操作点（网络请求前、网站遍历时）检查取消状态，确保及时响应

### 2025-11-23 - 识别配置系统
- **识别配置系统**：
  - 新增全局识别默认配置（IdentificationConfig），支持10个核心配置项
  - 在Settings页面新增"识别配置"标签页，提供可视化配置界面
  - 支持配置：重试次数、超时时间、模糊匹配、识别策略、并行查询等
  - 替换FilesService中的硬编码配置，统一使用全局默认配置
  - 更新config.yaml配置文件模板，添加identification配置节点
  - 优化媒体识别流程，提供更灵活的配置能力

### 2025-10-25
- **GamePage界面优化**：
  - 将游戏库统计和最近入库游戏部分改为响应式布局，大屏幕时并排显示（各占50%），小屏幕时自动换行
  - 游戏库统计部分样式统一为Home页面的媒体库统计样式，使用`total-stats-card`和`dashboard-item`类
  - 最近入库游戏部分样式统一为Home页面的内容卡片样式，使用`content-card`类
  - 提升了页面的视觉一致性和响应式体验

- **筛选工具栏布局优化**：
  - 将筛选与排序区域与游戏展示区域合并到一个MudItem中，提升布局紧凑性
  - 保持原有的简洁设计风格，确保界面的一致性和可用性
  - 筛选工具栏紧贴在游戏展示区域上方，减少页面空白区域

### 2026-04-01 - Settings页面全面优化

- **Settings 接口健壮性加固**：
  - 所有异步操作（保存、测试、刷新、修改密码/用户名）添加双击防护（`if (processing) return`）
  - `_saveAllConfig()` 保存前同步多选状态（日志类型、忽略文件/模式、允许扩展名），修复之前"保存全部"会丢失多选修改的问题
  - `_testAI()` 将配置保存移入 try-catch，修复保存失败时按钮永久禁用的问题
  - `_testProxy()` 添加代理地址格式校验（`Uri.TryCreate`），并对输入 trim 空白
  - `_changeUsername()` 添加用户名长度校验（2-50字符），在 finally 中同时清空用户名和密码字段
  - 对话框新增 `Counter="50" MaxLength="50"` 限制，Disabled 条件从空字符串检查改为最小长度检查
  - `_addIgnoredFile/Pattern/Extension` 添加输入 trim、同时更新选中列表和默认下拉选项，修复新增项不出现在多选下拉中的问题
  - 网络相关错误提示添加"请检查网络连接"引导

- **settings.css 从646行精简至195行**：
  - 移除与 `cards.css` 重复的 `.card-content`、`.card-actions`、`.card-title`、`.card-subtitle`、`.card-base` 样式
  - 移除全局 MudBlazor 组件选择器覆盖（`.mud-button`、`.mud-switch`、`.mud-select`、`.mud-tooltip` 等），避免污染其他页面
  - 移除与 `Tip.razor`/`forms.css` 重复的 `.tip-icon` 样式
  - 移除未使用的 `.status-indicator`/`.status-connected`/`.status-disconnected`/`.status-testing` 样式
  - 移除有害的 `will-change` 性能反模式和未使用的 `@keyframes tabFadeIn`
  - 移除 `@media (prefers-color-scheme: dark)` 深色模式块（MudBlazor 通过 CSS 变量自动处理主题切换）
  - 合并多处重复的激活标签页样式定义
  - 清理十几处 `/* 移除xxx */` 残留注释
- **修复内联 `<script>` 问题**：将 `getUserAgent()` 函数从 `Settings.razor` 移至独立的 `wwwroot/js/settings.js`，在 `App.razor` 中引用
- **Settings.razor 设计系统规范化**：
  - 移除外层 `card-base` 类（hover lift效果不适合标签页容器），改用 `Elevation="0"` 配合 `modern-tabs-container` 自定义阴影
  - 内层每个标签卡片移除 `card-base`（内容区域不需要悬停动画），仅保留 `card-bordered-*` 左边框样式
  - 将 `card-content` 类替换为 MudBlazor 原生 `pa-6` 工具类，避免与 `cards.css` 的 `.card-content` 产生双重 padding
  - 将 `card-actions` 类替换为 MudBlazor 原生 `d-flex justify-end gap-3 pa-4` 工具类
  - 将 `card-title` 类替换为 `font-weight-semibold`，避免 `cards.css` 中 `.card-title` 的 `margin-bottom` 在 CardHeader 上下文中产生多余间距
  - 所有内层卡片 `Elevation` 从 2 改为 0，避免与 `card-bordered-*` 的渐变背景产生视觉冲突
- **Settings 性能优化**：
  - `KeepPanelsAlive` 改为 `false`，减少 DOM 中约 7/8 的 MudBlazor 组件实例（状态由 C# 字段持有，切换标签页不丢失数据）
  - 移除 `_saveAllConfig` 和 `_resetAllConfig` 中的 `Task.Delay(500)` 人为延迟
  - `_testAI()` 不再调用 `_saveAllConfig()`，仅保存 AI 配置后测试
  - `_testProxy()` 从模拟 `Task.Delay(2000)` 改为真实 HTTP 代理连接测试
  - `_testSearch()` 从模拟异步改为同步检查，移除无意义的 `_testSearchProcessing` 状态
  - `_resetAllConfig()` 从 `async Task` 改为同步 `void`（纯内存操作无需异步），并补充重置多选状态同步
  - CSS: 降低 `backdrop-filter` 模糊半径（10px→4px），移除 `settings-fadeInUp` 弹跳动画，添加 `prefers-reduced-motion` 支持
- **Settings 最终打磨**：
  - `MudCardActions` 水平 padding 从 `pa-4` (16px) 改为 `px-6 py-4`，与内容区 `pa-6` 的水平 padding 对齐
  - 代理密码字段添加 `Label="代理密码"`，修复之前缺少标签与相邻用户名字段不一致的问题
  - `MudInputPassword` 组件新增 `Margin` 参数，代理密码和API密钥字段添加 `Margin="Margin.Normal"` 与相邻字段间距一致
  - 重置按钮简化为纯静态（同步操作不需要 loading 状态），移除无用的 `_resetProcessing` 字段
  - 移除未使用的 `System.Text.RegularExpressions` 引用
  - CSS 滑动指示器 `transition: all` 改为 `transition: left, width`，减少不必要的属性过渡
  - 清理代码中多余空行
- **Settings 标签页切换过渡动画**：
  - 参考 MediaOverviewPage 分类切换的 fade-out → swap → fade-in 模式
  - 将 `@bind-ActivePanelIndex` 改为 `ActivePanelIndex` + `ActivePanelIndexChanged` 回调，由 `_onTabChanged` 方法控制切换时序
  - CSS: 在 `.modern-tabs-container .mud-tabs-panels` 上添加 `transition: opacity 0.15s ease, transform 0.15s ease`
  - 切换时添加 `.is-tab-transitioning` 类触发淡出（opacity: 0 + translateY(6px)），等待 160ms 后切换内容并移除类触发淡入
  - 移除旧的 `@keyframes settings-fadeIn` 和 `TabPanelClass="modern-tab-panel"`
  - `prefers-reduced-motion` 媒体查询中包含新过渡，确保无障碍

### 2026-03-17 - 修复文件删除后 Media/MediaSource 未删除的 Bug

- **根本原因**：`FilesService.RemoveMediaByPath` 通过 `MediaSourceFactory.Create(path)` 创建的对象 `Id=0`，EF Core 将对象引用比较翻译为 `MediaBaseId == 0`，导致永远找不到记录，删除流程无法执行
- **修复 `MediaService.cs`**：
  - `MediaExistAsync(MediaSource)` 将 `m.Source == mediaSource` 改为 `m.Source.FullPath == mediaSource.FullPath`
  - `RemoveMediaAsync(MediaSource)` 同样改为 FullPath 字符串比较，并增加 `Include(m => m.Source)`
- **修复 `SourceService.cs`**：新增 `RemoveMediaSourceAsync` 方法，用于清理孤立的 MediaSource（存在源记录但无关联 Media 的情况）
- **修复 `FilesService.cs`**：`RemoveMediaByPath` 改为先通过 `_sourceService.FindMediaSourceAsync` 从数据库查找真实 MediaSource，再根据是否有关联 MediaBase 决定删除策略（有 Media → 删 Media 触发级联；无 Media → 直接删孤立源）

### 2026-03-17 - HomePage 统计卡片区域重构

- **问题**：原"总作品数卡片"（md=6）与5个分类统计卡片混在同一行，布局混乱且空间利用不足
- **新布局分两行**：
  - **Row 1 - 全局统计横幅**：单个 MudPaper 内使用 `.home-global-stats-grid` CSS grid（6列），展示6项指标：作品总数、占用空间、创作者数、社团数、标签数、已收藏数；除占用空间外均带导航链接
  - **Row 2 - 分类统计网格**：`.home-category-grid` CSS grid（5列等宽），保留原分类卡片样式，去除 MudGrid 嵌套
- **新增 4 个统计字段**（`Home.razor.cs`）：`_totalCreatorCount`、`_totalCircleCount`、`_totalTagCount`、`_totalFavoriteCount`，通过 `LoadGlobalStats()` 并行加载
- **新增 CSS**（`home.css`）：`.home-global-stats-grid`（6→3→2→1列响应式）、`.home-stat-chip`（居中纵向排列带悬停动画）、`.home-category-grid`（5→3→2列响应式）

### 2026-04-06 - FavoritesPage 媒体展示统一化

- **问题**：FavoritesPage的媒体展示区域使用自定义flex网格和直接MediaCard渲染，与MediaOverviewPage、TagPage、CreatorPage等页面使用的MediaShownView组件不一致，缺少筛选、排序、视图切换、分页等功能
- **改进**：
  - 替换自定义媒体网格为统一的 `MediaShownView` 组件，使收藏夹内容展示与其他页面保持一致
  - 新增筛选（分类、标签、评分、日期）、排序、网格/列表视图切换、分页功能
  - 收藏夹的重命名和删除操作按钮移至 MediaShownView 的 `TitleActions` 区域
  - 使用 `@key` 指令确保切换收藏夹时组件正确重建
  - 为 `MediaShownView` 新增 `HideFavoriteButton` 参数，支持隐藏媒体卡片上的收藏按钮
- **清理**：移除不再需要的自定义CSS样式（`fav-media-grid`、`fav-remove-btn-overlay`、`fav-toolbar-title` 等）

### 2026-04-14 - HomePage 新增"待处理"导航入口

- **目标**：让用户在主页一眼看到待识别/待入库积压，并直达 `UnknownPage` 对应 Tab。定位为 inbox 提示器而非工作面板
- **布局**：在分类统计网格与最近添加之间插入 `card-base card-bordered-warning` 卡片，内部两列网格（桌面端 50/50，≤600px 堆叠为单列），每列整块作为 `MudLink` 可点击，满足 ≥44px 触目标和键盘可达
- **零状态隐藏**：`_unidentifiedCount == 0 && _pendingCount == 0` 时整块不渲染，避免空状态占位
- **半零状态弱化**：其中一边为 0 时该列追加 `home-pending-slot--muted` 修饰类，降低视觉权重但仍可点击
- **新增字段**（`Home.razor.cs`）：`_unidentifiedCount` / `_pendingCount`，通过 `LoadPendingCounts()` 并入 `LoadDashboardData` 的 `Task.WhenAll` 并行加载；依然复用现有 `IDbContextFactory<MediaDbContext>`（`MediaSources` 与 `PendingIdentifications` 由 `SourceDbContext.cs` 作为 `MediaDbContext` 的 partial 声明）
- **新增 CSS**：`wwwroot/css/components/home-pending-card.css`，已在 `App.razor` 引用。类名前缀 `home-pending-`，包含 `home-pending-header` / `home-pending-grid` / `home-pending-slot` / `home-pending-slot--muted` / `home-pending-slot__{icon,label,number,hint,arrow,skeleton}`；所有交互（hover、focus、reduce-motion）独立作用域，不污染全局
- **深链对接**：同步在 `UnknownPage` 增加 `[SupplyParameterFromQuery(Name = "tab")]`，把 `"unidentified"` / `"pending"` 映射到 `_activeTabIndex`（大小写不敏感，无效值保留默认），使主页卡片点击后直接落到对应 Tab

### 2026-04-14 - UnknownPage 批量处理期间全页交互锁定

- **问题**：批量任务（批量加入队列/批量删除/批量入库/批量丢弃）执行期间，刷新按钮、Tab 切换、筛选控件、分类统计卡片、DataGrid 行内按钮仍可点击，容易造成状态与请求错乱
- **解决**：以 `_isBatchProcessing` 为单一信号，统一锁定页面全部可写入口
  - **顶部**：刷新按钮 `Disabled="@(_isLoading || _isBatchProcessing)"`
  - **Tab 切换**：原生 `<button>` 加 `disabled="@_isBatchProcessing"`，同时在 `OnActiveTabChanged` 入口 guard
  - **筛选栏**：搜索 `MudTextField`、类型 `MudSelect`、重置按钮全部加 `Disabled="@_isBatchProcessing"`
  - **分类统计卡片**：`GetStatCardClass` 在批量运行时追加 `card-disabled`（来自 `cards.css` 的通用工具类：`opacity` 降级 + `pointer-events:none` + 灰度），键盘分支由 `SetCategoryFilter` 内部 guard 拦截
  - **待识别 Tab 行操作**：桌面 5 个 `MudIconButton` 和移动端 `MudMenu` 全部加 `Disabled="@_isBatchProcessing"`
  - **待入库 Tab 行操作**：在 `CellTemplate` 内新增本地变量 `rowDisabled = committing || _isBatchProcessing`，所有按钮改用 `rowDisabled`；单行入库 spinner 仍由原 `committing` 控制，避免批量丢弃误显示"正在入库"
  - **无障碍**：`MudCard` 的 `aria-busy` 改为 `_isLoading || _isBatchProcessing`，读屏器在两种繁忙态下表现一致
- **双重保险原则**：UI 层 `Disabled` 属性 + C# 层 guard（`SetCategoryFilter` / `ResetFilters` / `OnActiveTabChanged`）。前者给视觉反馈，后者处理 SignalR 延迟下的状态不一致兜底

### 2026-04-14 - MediaPage 头部海报背景重做（方案 B）

- **问题**：`card-header` 是横长容器（≈12:1），海报是竖长（≈2:3）。原 `.media-header-poster-bg` 用 `inset:0` + `background-size: cover` 全屏铺满，把海报放大到铺满后横向裁掉 ~95% 内容，只剩中间窄带，视觉上像被压扁的肉饼
- **方案**：海报层退化为左侧氛围带，露出 `card-header` 自带的 135° 分类色渐变
  - **左 35% 宽**：替代原 `inset: 0`，只占左侧
  - **加大模糊**：`blur(2px)` → `blur(10px)`，海报失去图像识别度成为纯色彩源
  - **opacity 提升**：`0.34` → `0.45`（hover 时 → `0.55`），补偿宽度收窄后的视觉权重损失
  - **mask 软渐隐**：`linear-gradient(to right, black 40%, transparent 100%)`，左 40% 完全不透明、向右消融，避免硬边
  - **transform: scale(1.1)**：让 blur 边缘扩展到容器外，避免 mask 边界露出锐边
  - **海报缺失兜底**：`background-color: transparent`，加载失败自动退回纯渐变
  - **响应式**：≤768px 整层 `display: none`，移动端退回 card-header 自带渐变（避免与紧凑标题/按钮挤压）
  - **无障碍**：`pointer-events: none` 让点击穿透；`prefers-reduced-motion` 禁用过渡
- **改动文件**：`wwwroot/css/components/cards.css`（`.media-header-poster-bg` 重写）+ `wwwroot/css/pages/media.css`（768px 断点追加隐藏规则）。Razor 不动

### 2026-04-14 - MediaPage 头部 typography 重建（标题 hero 化 + 分类 chip）

- **问题**：上一次海报背景重做后，标题和分类在彩色渐变背景下不够明显
  - **根因 1**：`Typo.h1` 被同时套上 `card-title` 全局类，CSS 优先级胜出把字号压成 18px（`var(--font-size-lg)`），HTML 是 h1 但视觉是普通卡片副标题大小；avatar Size.Large 56px 反而比标题大 3 倍，头重脚轻
  - **根因 2**：副标题（分类）用 `card-subtitle` → `font-size: 14px` + `var(--mud-palette-text-secondary)` 灰色，在彩色渐变背景上几乎不可见
- **方案**：根除"通用卡片字号被错套在 hero header 上"的矛盾
  - **标题**：`MediaPage.razor:100` 移除 `card-title` 类，只保留 `media-header-title`；新规则 `font-size: clamp(1.625rem, 2.5vw, 2.25rem)`（26-36px 流体）+ `font-weight: bold` + `letter-spacing: -0.015em` + `line-height: 1.15`，与 avatar 56px 形成正确的 hero 比例
  - **分类**：`MudText` 整体替换为 `MudChip`，`Variant=Filled` + `Color=GetMediaColor(...)` + `Icon=GetMediaIcon(...)`，从"灰色文字"升级为"分类色徽章"，物理意义改变后不再依赖文字对比度，且与列表页/侧边栏的分类徽章视觉一致
  - **480px 断点更新**：移除 `font-size: var(--font-size-lg) !important`，clamp 已自动把字号收到 26px；只保留 `white-space: normal` + `line-clamp: 2` 多行换行规则
- **不引入**：未加 text-shadow / glassmorphism / backdrop-filter 等"对比度补丁"，靠正确的字号 × 字重 × chip 物理形态站住
- **改动文件**：`MediaPage.razor`（行 100-101）+ `wwwroot/css/pages/media.css`（行 28 全局规则 + 行 493 480px 块）

### 2026-04-14 - MediaShownView 每页数量可切换 + localStorage 偏好

- **问题**：`MediaShownView` 共享组件的 `PageSize` 是 `[Parameter] = 20` 由调用方传入，运行时不可改；6 个调用页面（MediaOverviewPage / FavoritesPage / TagPage / CreatorPage / CirclePage 等）全部沿用默认值，用户无法根据屏幕大小或浏览意图调整密度
- **方案**：在组件级别加每页数量选择器，所有调用方自动获益
  - **状态字段**：新增 `_userPageSize`（初始 = `Parameter.PageSize`）+ `static readonly int[] AllowedPageSizes = {12, 20, 50, 100}` + `const string PageSizeStorageKey = "medialib_pagesize"`
  - **localStorage 偏好**：`OnAfterRenderAsync(firstRender)` 阶段读取（Blazor Server 在 OnInitialized 还无 JS 访问），有效值 + 与默认不同时触发一次重新加载；`OnPageSizeChanged` 写回。失败仅 `Log.Warning`，不中断使用
  - **新方法 `OnPageSizeChanged(int)`**：校验白名单、reset 到第 1 页、写 localStorage、`LoadMediasAsync`
  - **修复 ResetFilters bug**：原 `_currentQueryParams.PageSize = PageSize`（参数）会丢用户选择，改为 `= _userPageSize`
  - **OnInitialized / OnParametersSet 同步**：所有 `PageSize` 引用改成 `_userPageSize`
- **UI 布局**：`MediaShownView.razor` 把原"分页器单独居中"的 div 替换为三列 grid 容器
  - 桌面端 `1fr auto 1fr`：左 PageSize、中分页器（真正居中）、右占位列平衡
  - 总数 ≤ 1 页时切 `media-pager-bar--no-pages` 修饰类，单列让 PageSize 自动居中
  - 移动端（≤768px）单列堆叠，分页器在上 / PageSize 在下（贴近翻页操作）
- **新增 CSS**：`wwwroot/css/components/media-pager-bar.css`，已在 `App.razor` 引用。BEM 命名 `media-pager-bar__size / __pages / __spacer` + `--no-pages` modifier
- **文案**：选择器 Label "每页"，选项 12 / 20 / 50 / 100；不提供"全部"选项以避免大数据集卡死
- **依赖注入**：`IJSRuntime` 通过 `[Inject]`，`using Microsoft.JSInterop`

**注意**：本项目仍在积极开发中，部分功能可能还在完善。建议在生产环境使用前进行充分测试。

## 📄 许可证

本项目采用 [许可证名称] 许可证，详情请查看 LICENSE 文件。
