# 桌面端 ↔ Web 端 功能对等盘点清单

> **本文档的定位**：清点 Web 端所有页面 / 组件 / 功能 + 清点桌面端当前实现。**仅是清单**——具体差距条目和优先级排序留作后续填充。
>
> **不是**：新 Phase 实施计划（Phase 1/2/3 文档已经存在并完成；4 在排期中）。
>
> **使用方式**：
>
> 1. 后续做"功能对等"工作时，逐节对照 Web 列与 Desktop 列，把缺失项填到「差距」子节里
> 2. 填完差距后再决定每条归属哪个 Phase（可能需要补 Phase 5、Phase 6 实施 plan）
> 3. 任何一边新增功能 → 同步更新这两栏，避免漂移
>
> **盘点时点**：2026-05-06
> **基线 commit**：`27242eb`

---

## 0. 总览矩阵

### 0.1 页面对照（按 Web 端路由分组）

| 模块 | Web 路由 / 页面 | Desktop 对应 | Desktop 实现状态 |
|---|---|---|---|
| 首页 | `/` Home.razor | HomePage | ✅ 已建（仪表盘 + 4 计数 + 5s 轮询） |
| 媒体库 | `/media/overview[/{Category}]` | MediaOverviewPage | ✅ 已建（分页 + 分类切换 + 搜索 + 排序） |
| 媒体详情 | `/media/{id}` | MediaDetailWindow（独立窗） | ⚠ 仅只读预览（Phase 1.3 MVP，编辑模式 Phase 2 未做） |
| 任务总览 | `/tasks` TasksPage | 📌 §12 合 Tab | 决策：作为 BackgroundTasksPage 顶部统计区 + 三 Tab 容器，不独立 |
| 后台任务 | `/tasks/background` | BackgroundTasksPage | ✅ 已建（待 Tab 化重构：运行中 / 历史 / 定时） |
| 定时任务 | `/tasks/scheduled` | 📌 §12 合 Tab | 决策：作为 BackgroundTasksPage "定时" Tab |
| 执行历史 | `/tasks/history` | 📌 §12 合 Tab | 决策：作为 BackgroundTasksPage "历史" Tab |
| 标签层级 | `/tags` TagsPage | TagsPage | ✅ 已建（顶级 + 子标签两层 + CRUD + 搜索） |
| 标签详情 | `/tag/{tagId}` | ❌ 缺 | 桌面端无单标签详情页（点击标签无关联媒体浏览） |
| 标签映射 | `/tags/mappings` | ❌ 缺 | 桌面端无映射管理 |
| 创作者列表 | `/creators` | CreatorsPage（双态） | ✅ 已建（列表 ↔ 详情双态合并） |
| 创作者详情 | `/creator/{id}` | CreatorsPage 详情态 | ✅ 已建（嵌入页内，不独立路由） |
| 社团列表 | `/circles` | ❌ 缺 | 桌面端无社团管理（只在创作者详情里读社团） |
| 社团详情 | `/circle/{id}` | ❌ 缺 | 桌面端无社团详情 |
| 媒体源管理 | `/sources` | SourcesPage | ⚠ 仅 watch_folders 列表 + 加 / 删 / 重扫，**没有**文件浏览器 / 面包屑 / 排序 / 媒体源列表（仅监视文件夹这一种"源"） |
| 媒体源详情 | `/source/{id}` | ❌ 缺 | 桌面端无单源详情 |
| 待处理 | `/source/pending` UnknownPage | PendingMediaPage | ⚠ 双 Tab 已建，但批量操作 / 详情查看 / 手动添加未完整 |
| 网站配置 | `/website` | WebsitesPage | ⚠ 已建 3 站启用 + 参数；**优先级拖拽排序未做** |
| 收藏夹 | `/favorites` | FavoritesPage | ⚠ 列表 + 创建 + 删除已建；**重命名 / 媒体批量移除 / 筛选排序**未做 |
| 全局搜索 | `/search` SearchResult | ❌ 缺 | 桌面端**完全无**搜索功能（无搜索框、无结果页、无 AI 语义搜索） |
| 设置 | `/settings` Settings | SettingsPage | ✅ 已建（覆盖比 Web 更全：外观 / 任务 / 识别 / 文件 / AI / 标签匹配 / 搜索 / 日志 / 数据库） |

### 0.2 共享对话框 / 组件对照

| 类型 | Web 端 | Desktop 端 | 状态 |
|---|---|---|---|
| 通用确认 | NineKgConfirmDialog（4 Intent） | NineKgConfirmDialog（4 Intent） | ✅ 双端语义一致 |
| 媒体卡片 | MediaCard / SimpleMediaCard | MediaCardViewModel + 模板 | ✅ 已建简化版 |
| 媒体列表 | MediaList / MediaListItem | ❌ 缺（仅网格） | 桌面端只做卡片网格，无列表布局切换 |
| 媒体筛选 | MediaFilterDialog | ❌ 缺 | 桌面端无多维筛选（标签 / 收藏 / 评分 / 日期） |
| 媒体新建 | ManualAddMediaDialog | ❌ 缺 | PendingMediaPage / SourcesPage / Overview 三入口都没接 |
| 媒体类型选 | MediaKindPickerDialog | ❌ 缺 | 桌面端无双卡片选择器 |
| 标签选择 | TagSelectorDialog | ❌ 缺 | 桌面端无标签选择 dialog |
| 创作者选择 | CreatorSelectorDialog | ❌ 缺 | 桌面端无创作者选择 dialog |
| 分类选择 | CategorySelectorDialog | ❌ 缺 | 桌面端无分类多选 dialog |
| 文件浏览 | FileExplorer + DirectoryTreeView | 原生 FolderPicker | ⚠ 用 OS 原生 picker 替代，无应用内树视图 |
| 任务详情 | TaskDetailsDialog | ❌ 缺 | 桌面端只有诊断窗口，无完整任务详情 dialog |
| 历史详情 | TaskHistoryDetailsDialog | ❌ 缺 | 桌面端无历史详情 |
| 任务快查 | TaskQuickViewDialog | ❌ 缺 | 桌面端无快速查看 |
| 任务日志 | TaskLogViewer | ❌ 缺 | 桌面端无日志查看器 |
| 任务树节点 | TaskTreeNode | ❌ 缺（任务列表是平铺） | 桌面端任务页用平铺列表，无父子树状结构 |
| 识别诊断 | IdentificationDiagnosticsView | IdentificationDiagnosticsView (axaml) | ✅ 双端等价 |
| 识别加载 | IdentificationLoadingDialog | ❌ 缺 | 桌面端无独立识别进度 dialog |
| 识别选项 | IdentificationOptionsDialog | ❌ 缺 | 桌面端无手动选择候选结果 dialog |
| 全局搜索框 | GlobalSearchBox | ❌ 缺 | 桌面端**无**搜索入口 |
| 搜索结果预览 | SearchResultPreview | ❌ 缺 | 同上 |
| 搜索筛选 | SearchFilterDialog | ❌ 缺 | 同上 |
| 图片上传 | ImageUploadDialog | ❌ 缺 | 桌面端无创作者 / 社团头像上传 |
| 图片墙 | PhotoWall | ❌ 缺 | 桌面端无相册展示 |
| 别名编辑 | EditableAliasList | ❌ 缺 | 桌面端创作者 / 社团详情无别名编辑控件 |
| 描述编辑 | DescriptionEditor | ❌ 缺 | 桌面端无 markdown / AI 翻译编辑器 |
| 信息编辑 | InfoEditor | ❌ 缺 | 桌面端 MediaDetailWindow 是只读，无编辑控件 |
| 收藏选择 | FavoriteSelector | ❌ 缺 | 桌面端无快速加入 / 移出收藏夹控件 |
| 拖拽排序 | SortableList | ❌ 缺 | 桌面端无拖拽排序基础组件（影响网站优先级） |
| 通用 PageHeader | PageHeader | 各页内联 | ⚠ 桌面端复用 Hero 区，无统一组件 |
| 通用 SectionCard | SectionCard | 各页内联 | ⚠ 桌面端无统一容器组件 |
| 通用 Loading | LoadingDialog | ❌ 缺 | 桌面端用 ProgressRing 内联 |
| 通用 Tip | Tip | ❌ 缺 | 桌面端用 InfoBar |
| Web 独有 | RedirectToLogin / Login / MudInputPassword | n/a | 桌面端无认证体系 |
| Desktop 独有 | n/a | DragDropFolderActionDialog | ✅ 桌面端独占（拖入文件夹双卡片） |
| Desktop 独有 | n/a | CreatorMergeDialog | ✅ 桌面端独占（合并创作者） |
| Desktop 独有 | n/a | TagEditorDialog / TopTagEditorDialog | ✅ 桌面端独占（标签编辑专用） |

> **注**：上表 ❌ 表示"完全没做"，⚠ 表示"做了但不全"，✅ 表示"基本对等"。每条具体的"差距条目"留到下面各模块详细盘点里写。

---

## 1. 媒体模块

### 1.1 首页（Home）

#### Web 端清单（`Pages/Home.razor`）

- 全局统计卡片：媒体总数、创作者数、社团数、标签数、收藏数、占用空间
- 分类统计排行展示（按媒体类型分布）
- 待处理队列入口（待识别 / 待入库快速跳转）
- 快速跳转链接到各管理页
- 数据刷新功能

#### Desktop 端清单（`Views/Pages/HomePage.axaml` + `HomeViewModel`）

- 媒体总数（DbContext 直查）
- 监视文件夹配置项数 + 实时监控状态
- 运行中任务计数（Running / Pending / Retrying）
- 失败任务计数（Failed / Timeout）
- 5s 轮询自动刷新
- 4 种 Intent 对话框演示按钮（开发遗留，应清理）
- ❌ 无创作者 / 社团 / 标签 / 收藏数统计
- ❌ 无占用空间统计
- ❌ 无分类排行
- ❌ 无快速跳转链接

#### 差距清单

> 优先级：**P0** 阻塞日常 / **P1** 高频有 workaround / **P2** 低频但影响完整 / **P3** 次要可选。

- [ ] **P2** 创作者 / 社团 / 标签 / 收藏 4 项统计计数（DbContext 直查，工作量小，缺这几张卡片首页空荡）
- [ ] **P2** 占用空间统计（聚合 `Media.FileInfo.Length`，对应 Web 端 6 卡之一）
- [ ] **P2** 分类统计排行（按 `TopCategory` 分组计数 + 视觉化 chip / bar）
- [ ] **P3** 待处理队列入口（待识别 + 待入库 chip + 计数 + 跳转）
- [ ] **P3** 各管理页快速跳转链接（媒体库 / 创作者 / 社团 / 标签 / 收藏 / 任务）
- [ ] **P3** 清理 4 种 Intent 对话框演示按钮（开发遗留，应删除）

---

### 1.2 媒体库（MediaOverview）

#### Web 端清单（`Pages/Medias/MediaOverviewPage.razor` + `Components/Medias/MediaShownView.razor`）

- 路由：`/media/overview` 和 `/media/overview/{Category}`
- 分类选择卡片（全部 + 5 顶级类型）
- 媒体网格 / 列表视图切换（MediaGrid / MediaList）
- 多维筛选（MediaFilterDialog）：分类、标签、收藏夹、评分区间、日期区间
- 排序：入库日期、发售日期、评分、文件大小、标题
- 分页：可选 24 / 48 / 96 / 192 条 + 跳页 + 总数
- 手动新建媒体按钮（PageHeader 的 Actions 插槽）→ MediaKindPickerDialog → FileExplorer → ManualAddMediaHelper
- 媒体卡 / 列表项跳转 `/media/{id}`

#### Desktop 端清单（`Views/Pages/MediaOverviewPage.axaml` + `MediaOverviewViewModel`）

- 分页加载（24 条 / 页固定）
- 5 顶级分类切换（含 toggle 取消逻辑：再次点击取消）
- 搜索框 + 300ms 防抖
- 排序下拉（基础几种）
- 上一页 / 下一页 / 当前页码显示
- 仅卡片网格（MediaCardViewModel + AXAML 模板）
- ImageCacheService 缓存封面
- ❌ 无网格 / 列表切换
- ❌ 无 MediaFilterDialog（多维筛选缺失）
- ❌ 无"手动新建媒体"入口
- ❌ 无 PageHeader Actions 插槽
- ❌ 无每页大小选择
- ❌ 无总数显示
- ⚠ 点击卡片打开 MediaDetailWindow（只读，无编辑模式）

#### 差距清单

- [ ] **P0** 手动新建媒体入口：PageHeader Actions 插槽里的"新建媒体"按钮 → MediaKindPickerDialog 等价 → FolderPicker → ManualAddMediaHelper 等价（这是补齐冷门资源的主入口，桌面端目前无任何手动添加路径）
- [ ] **P1** 多维筛选：标签 / 收藏夹 / 评分区间 / 日期区间（依赖新建 MediaFilterDialog 等价 → §10.5）
- [ ] **P2** 网格 ↔ 列表视图切换（依赖新建 MediaListView + MediaListItem 等价模板）
- [ ] **P2** 每页大小选择（24 / 48 / 96 / 192，桌面端目前固定 24）
- [ ] **P3** 总数显示（已有 TotalCount 属性，差一处 UI 绑定）
- [ ] **P3** 排序选项对齐 Web 5 种（入库 / 发售 / 评分 / 大小 / 标题）

---

### 1.3 媒体详情（MediaPage）

#### Web 端清单（`Pages/Medias/MediaPage.razor`）

- 路由：`/media/{id}`，支持 `?edit=true` 查询参数自动进入编辑模式
- 海报展示 + Hero 区
- 编辑模式切换（编辑 / 保存 / 取消 / 删除）
- 创作者列表（CreatorList，可编辑）
- 社团列表（CircleList，可编辑）
- 标签编辑（TagAdder + TagSelector + TagSelectorDialog）
- 收藏夹快速操作（FavoriteSelector）
- 分类与等级（CategorySelector）编辑
- 描述编辑器（DescriptionEditor，含 AI 翻译按钮）
- 备注 / 别名 / 发售日期 / 入库日期 / 文件大小 / 路径
- 海报图上传（ImageUploadDialog）
- 删除确认 → NineKgConfirmDialog Destructive

#### Desktop 端清单（`Views/Windows/MediaDetailWindow.axaml` + `MediaDetailViewModel`）

- 独立窗口（多媒体可同开 + 位置记忆）
- Hero 区（封面 + 标题 + 圆名 + 创作者文字串 + 评分 + 标签 chip）
- 摘要 / 描述 / 别名 / 路径 / 大小 / 发售日期 / 入库日期 / 顶级分类 / 收藏夹只读展示
- "在文件管理器打开"按钮
- "重新识别"按钮
- 加载态 / 错误态 / 主内容态切换
- ❌ 无编辑模式
- ❌ 无创作者 / 社团 / 标签 / 收藏 / 分类 编辑
- ❌ 无描述编辑器（无 AI 翻译）
- ❌ 无海报上传
- ❌ 无删除按钮（只能去 BackgroundTasks 之外的入口删？实际上无删除入口）

#### 差距清单

- [ ] **P0** 编辑模式切换（顶部"编辑 / 保存 / 取消 / 删除"按钮组），把 MediaDetailWindow 从只读升级为编辑器
- [ ] **P0** 创作者编辑（CreatorList 等价：列表 + 添加 + 移除 + 跳转 + 依赖 CreatorSelectorDialog 等价）
- [ ] **P0** 社团编辑（CircleList 等价 + CircleSelectorDialog，可与 CreatorSelector 复用控件抽象）
- [ ] **P0** 标签编辑（TagSelector + TagSelectorDialog 等价，按顶级标签分组多选）
- [ ] **P1** 收藏夹快速操作（FavoriteSelector 等价：multi-select 加 / 移）
- [ ] **P1** 分类与等级编辑（CategorySelector + CategorySelectorDialog 等价，覆盖 TopCategory + SubCategory + Rating）
- [ ] **P1** 描述编辑器（DescriptionEditor 等价：textarea + AI 翻译按钮）
- [ ] **P2** 海报上传（ImageUploadDialog 等价：本地选图 + 裁剪 + 写 `Image.Content`）
- [ ] **P2** 删除按钮 + Destructive 确认（NineKgConfirmDialog 已有，差入口）
- [ ] **P3** 别名 / 备注 / 发售日期 / 入库日期 / 路径 inline 编辑（小字段批量做 InfoEditor 等价）
- [ ] **P3** 决策：编辑模式做在独立窗口 vs 切换主窗 → 关系到多窗口同时编辑的并发 / 锁

---

## 2. 任务模块

### 2.1 任务总览（TasksPage）

#### Web 端清单（`Pages/Tasks/TasksPage.razor`）

- 执行统计卡片（总执行数、成功率）
- 后台任务 / 定时任务 / 历史 三个子页快速导航
- 运行中任务**树状**展示（TaskTreeNode 父子关系，支持展开折叠）
- 实时进度订阅（TaskProgressService）
- 任务取消操作
- 任务详情查看（TaskDetailsDialog / TaskQuickViewDialog）
- 跳转到执行历史

#### Desktop 端清单

- ❌ 桌面端**无任务总览页**——直接跳到 BackgroundTasks

#### 差距清单

> §12 已决策：**合并为 BackgroundTasksPage 的 3 Tab**（运行中 / 历史 / 定时），不做独立 TasksPage。下列条目并入 §2.2 BackgroundTasks 的 Tab 化重构。

- [ ] **P2** 总执行数 / 成功率 / 失败率 统计卡片（顶部 Hero 区，覆盖三 Tab）
- [x] ~~**P2** 决策：单独的任务总览页 vs 合并~~ — **§12 已决策：合并 Tab**
- [x] ~~**P2** 后台 / 定时 / 历史 三页快速导航 chip~~ — **§12 已决策：直接 TabControl，无需 chip 跳转**

---

### 2.2 后台任务（BackgroundTasksPage）

#### Web 端清单（`Pages/Tasks/BackgroundTasksPage.razor`）

- 运行中任务统计：运行数、总处理数、失败数、最长运行时间
- 文件夹监控任务列表
- 处理进度（已处理 / 失败 / 成功率）
- 任务详情展开（嵌入式）
- 任务停止按钮
- 日志查看（TaskLogViewer）

#### Desktop 端清单（`Views/Pages/BackgroundTasksPage.axaml` + `BackgroundTasksViewModel`）

- 4 状态过滤 chip（All / Running / Succeeded / Failed）+ 计数
- 任务列表（平铺，500ms 轮询差量更新）
- 取消任务（Hangfire + 运行时双向取消）
- 打开诊断窗口（TaskDiagnosticsWindow）
- 清理 1 分钟前已完成任务
- ❌ 无文件夹监控任务的"处理进度 / 已处理 / 失败 / 成功率"统计
- ❌ 无任务父子树状结构（TaskTreeNode）
- ❌ 无任务日志查看器（TaskLogViewer）
- ❌ 无任务详情 dialog（TaskDetailsDialog）
- ❌ 无最长运行时间统计

#### 差距清单

> §12 已决策：BackgroundTasksPage 升级为 **3-Tab hub**（运行中 / 历史 / 定时），整合 §2.1 / §2.3 / §2.4。

- [ ] **P1** Tab 化重构：现有平铺列表 → 三 Tab（运行中 / 历史 / 定时），共享顶部统计 Hero
- [ ] **P1** 父子任务树（TaskTreeNode 等价控件）：批量识别父任务展开后能看到所有子任务的实时进度
- [ ] **P1** 任务详情 dialog（TaskDetailsDialog 等价）：详细参数 / 重试历史 / 错误信息 / 子任务列表
- [ ] **P1** 任务日志查看器（TaskLogViewer 等价）：嵌入 Serilog 输出 + 级别过滤 + 关键词搜索
- [ ] **P2** 文件夹监控的"已处理 / 失败 / 成功率"统计（Web 端是这页核心 KPI）
- [ ] **P3** 最长运行时间统计

---

### 2.3 定时任务（ScheduledTasksPage）

#### Web 端清单（`Pages/Tasks/ScheduledTasksPage.razor`）

- 定时任务列表（PendingIdentificationCleanup / CacheCleanup 等）
- 启用 / 禁用开关
- Cron 表达式编辑
- 下次执行时间计算
- 上次执行时间显示
- 立即执行按钮
- Cron 帮助对话框（语法 / 示例）
- 批量保存配置

#### Desktop 端清单

- ❌ 完全无对应页

#### 差距清单

> §12 已决策：作为 **BackgroundTasksPage 的"定时" Tab** 实现，不另建独立页。

- [x] **P2** "定时" Tab 内容：定时任务列表（绑定 `Config.Tasks.ScheduledTasks`，启用 chip 展示）— 启用 / 禁用开关待后续
- [x] **P2** 下次执行时间预览（用 Hangfire `GetRecurringJobs().NextExecution`，比 Web 的 cron 启发式更准）+ Cron 描述回退；Cron 编辑 + 校验待后续
- [x] **P2** 上次执行时间显示（Hangfire `RecurringJobDto.LastExecution`）
- [x] **P3** "立即执行"按钮（`TriggerScheduledCommand` → `UnifiedTaskService.ExecuteScheduledTaskAsync`，后台线程跑，进度进"运行中" Tab）
- [ ] **P3** Cron 帮助 dialog（语法速查 + 5-10 个常用模板）
- [ ] 启用 / 禁用开关 + Cron 编辑（需写回 yaml + 重新 AddOrUpdate/RemoveIfExists recurring job）

---

### 2.4 执行历史（TaskExecutionHistoryPage）

#### Web 端清单（`Pages/Tasks/TaskExecutionHistoryPage.razor`）

- 任务执行记录表格：任务名、类型、状态、耗时、处理数、失败数、开始 / 结束时间
- 搜索 + 多维筛选（名称、状态、任务类型）
- 分页 + 排序
- 失败任务"重试识别"按钮
- 执行详情查看（TaskHistoryDetailsDialog）

#### Desktop 端清单

- ❌ 完全无对应页

#### 差距清单

> §12 已决策：作为 **BackgroundTasksPage 的"历史" Tab** 实现，不另建独立页。

- [ ] **P2** "历史" Tab 内容：执行记录表格（DataGrid）—— 任务名 / 类型 / 状态 / 耗时 / 处理数 / 失败数 / 开始 / 结束
- [ ] **P2** 搜索 + 多维筛选（任务名 + 状态 + 类型）
- [ ] **P2** 分页 + 排序（按时间 desc 默认，可切其他列）
- [ ] **P3** 失败任务"重试识别"快捷入口（仅识别类任务）
- [ ] **P3** 执行详情 dialog（TaskHistoryDetailsDialog 等价：诊断 + 子任务结果 + 日志）

---

## 3. 标签模块

### 3.1 标签层级（TagsPage）

#### Web 端清单（`Pages/Tags/TagsPage.razor`）

- 顶层标签 + 子标签树形展示
- 标签搜索（即时筛选）
- 排序选项：编号 / 名称 / 数量
- 编辑模式开关
- 顶层标签：增（TopTagAdder）/ 删（NineKgConfirmDialog）/ 改（TopTagEditor）
- 子标签：增（TagAdder）/ 删 / 改（TagEditor）
- 跳转标签映射页
- 加载进度条

#### Desktop 端清单（`Views/Pages/TagsPage.axaml` + `TagsViewModel`）

- 顶级标签列表 ↔ 子标签列表 两态切换
- 顶级 CRUD（TopTagEditorDialog）+ 删除级联确认
- 子标签 CRUD（TagEditorDialog，含选择父级）
- 搜索 + 实时过滤
- ❌ 无排序选项
- ❌ 无树形展开（是双层独立列表）
- ❌ 无跳转标签映射页（因为映射页没做）

#### 差距清单

- [ ] **P2** 排序选项（编号 / 名称 / 数量），桌面端目前无任何排序
- [ ] **P3** 决策：树形展开折叠 vs 双层独立列表（桌面端目前是双层；Web 是树形——是否对齐取决于用户偏好）
- [ ] **P3** 跳转标签映射页（依赖 §3.3 整页落地）

---

### 3.2 标签详情（TagPage）

#### Web 端清单（`Pages/Tags/TagPage.razor`）

- 路由：`/tag/{tagId}`
- 标签基本信息编辑：名称、描述
- 上级标签显示
- 关联媒体列表（MediaShownView，支持筛选 + 分页）
- 保存修改
- 返回标签管理链接

#### Desktop 端清单

- ❌ 完全无对应页（点击子标签无地方"看这个标签关联的所有媒体"）

#### 差距清单

- [ ] **P2** 整页新建：`TagDetailPage.axaml` + ViewModel（或独立窗口 TagDetailWindow，参考 MediaDetailWindow 模式）
- [ ] **P2** 标签基本信息编辑（名称 + 描述）
- [ ] **P2** 关联媒体网格（复用 MediaCardViewModel + 分页 + 筛选）
- [ ] **P3** 上级标签显示 + 跳转

---

### 3.3 标签映射（TagsMappingsPage）

#### Web 端清单（`Pages/Tags/TagsMappingsPage.razor`）

- 映射统计卡片：总计、启用、禁用、总命中、未使用
- 映射表格 + 编辑（TagMappingEditorDialog）
- 搜索 + 状态筛选
- 批量启用 / 禁用 / 删除
- 添加新映射 dialog
- 清理未使用映射

#### Desktop 端清单

- ❌ 完全无对应页

#### 差距清单

- [ ] **P2** 整页新建：`TagsMappingsPage.axaml` + ViewModel（数据源 `TagMapping` 表）
- [ ] **P2** 映射统计卡片（总计 / 启用 / 禁用 / 总命中 / 未使用）
- [ ] **P2** 映射表格（DataGrid）+ CRUD（依赖 TagMappingEditorDialog 等价控件）
- [ ] **P2** 搜索 + 状态筛选（启用 / 禁用 / 未使用）
- [ ] **P3** 批量启用 / 禁用 / 删除（NineKgConfirmDialog DestructiveBatch 已有）
- [ ] **P3** 清理未使用映射快捷按钮

---

## 4. 创作者与社团模块

### 4.1 创作者列表（CreatorsPage）

#### Web 端清单（`Pages/Creators/CreatorsPage.razor`）

- 创作者卡片网格
- 搜索 + 类型筛选
- 分页 + 每页大小选择
- 头像 + 名字 + 别名计数
- 快速删除按钮
- 新增创作者 dialog

#### Desktop 端清单（`Views/Pages/CreatorsPage.axaml` + `CreatorsViewModel`）

- 列表态：分页（30 条 / 页）+ 搜索防抖 + 网格
- 详情态：头像 + 别名 + 关联媒体网格（嵌入页内，**非独立路由**）
- 删除创作者（保留关联媒体）
- 合并创作者（CreatorMergeDialog，桌面端独占）
- 列表 ↔ 详情 双态切换 + GoBackToList
- ❌ 无类型筛选
- ❌ 无每页大小选择
- ❌ 无新增创作者入口（仅自动创建于识别流程）
- ❌ 无独立创作者详情路由（多窗口浏览不能并存）

#### 差距清单

- [ ] **P2** 创作者类型筛选（已有 `CreatorType` 枚举，UI 缺筛选 chip）
- [ ] **P2** 每页大小选择（30 / 60 / 120）
- [ ] **P2** 新增创作者入口（虽然识别会自动产生，但 manual 添加是合理诉求）
- [x] ~~**P3** 决策：列表 ↔ 详情双态嵌入 vs 独立路由 / 独立窗口~~ — **§12 已决策：双态嵌入主窗**

---

### 4.2 创作者详情（CreatorPage）

#### Web 端清单（`Pages/Creators/CreatorPage.razor`）

- 路由：`/creator/{id}`
- 头像更新（ImageUploadDialog）
- 别名列表编辑（EditableAliasList）
- 创作者类型管理（增删改）
- 描述编辑器（DescriptionEditor + AI 翻译）
- 关联社团列表
- 关联媒体网格
- 保存修改

#### Desktop 端清单（CreatorsPage 详情态嵌入）

- 头像展示（不可编辑）
- 别名展示（不可编辑）
- 关联媒体网格
- ❌ 无头像更新
- ❌ 无别名编辑
- ❌ 无创作者类型管理
- ❌ 无描述编辑（无 AI 翻译）
- ❌ 无关联社团列表

#### 差距清单

- [ ] **P1** 别名编辑（EditableAliasList 等价 → §10.5）
- [ ] **P1** 头像更新（ImageUploadDialog 等价：本地图 + 裁剪 + 写 `Image.Content`）
- [ ] **P1** 描述编辑（DescriptionEditor 等价 + AI 翻译按钮）
- [ ] **P2** 创作者类型管理（增 / 删 / 改：CreatorTypeEntity）
- [ ] **P2** 关联社团列表（依赖 §4.3 / §4.4 落地后才有意义）
- [ ] **P3** 编辑模式切换 + 保存 / 取消（与 §1.3 编辑模式对齐）

---

### 4.3 社团列表（CirclesPage）

#### Web 端清单（`Pages/Creators/CirclesPage.razor`）

- 社团卡片网格
- 搜索
- 分页 + 每页大小
- 头像 + 名字 + 别名计数
- 快速删除
- 新增社团 dialog

#### Desktop 端清单

- ❌ 完全无社团管理页

#### 差距清单

- [ ] **P2** 整页新建：`CirclesPage.axaml` + ViewModel（卡片网格 + 搜索 + 分页 + 删除 / 新增）
- [ ] **P2** 与 CreatorsPage 共享布局：考虑抽取 `EntityListView` 通用控件（卡片 + 头像 + 别名计数 + 删除）减少重复代码 → §10.5
- [ ] **P3** 列表 ↔ 详情双态嵌入（参考 CreatorsPage 模式）

---

### 4.4 社团详情（CirclePage）

#### Web 端清单（`Pages/Creators/CirclePage.razor`）

- 路由：`/circle/{id}`
- 头像更新
- 别名列表编辑
- 描述编辑（含 AI 翻译）
- 关联媒体网格
- 保存修改

#### Desktop 端清单

- ❌ 完全无对应页

#### 差距清单

- [ ] **P2** 整页新建：`CircleDetailPage.axaml` + ViewModel（或独立窗口）
- [ ] **P2** 头像 / 别名 / 描述编辑（与 §4.2 共享 EditableAliasList / ImageUploadDialog / DescriptionEditor 等价控件）
- [ ] **P2** 关联媒体网格（复用 MediaCardViewModel）
- [ ] **P3** 编辑模式切换 + 保存 / 取消

---

## 5. 媒体源模块

### 5.1 媒体源管理（SourcesPage）

#### Web 端清单（`Pages/Sources/SourcesPage.razor`）

- 文件浏览器（FileExplorer + DirectoryTreeView）：驱动器 / 目录 / 文件三栏
- 面包屑导航
- 排序选项：名称 / 修改日期 / 类型
- 媒体源列表管理：启用 / 禁用 / 新增 / 删除
- 文件夹操作：识别 / 手动添加 / 加入队列 / 监控
- 文件操作：识别 / 手动添加
- 保存配置

#### Desktop 端清单（`Views/Pages/SourcesPage.axaml` + `SourcesViewModel`）

- 监视文件夹列表（差量刷新）
- 5s 轮询监控状态
- AddFolderAsync：原生 FolderPicker → 加入监视 + 提交批量识别
- RemoveAsync：停监控 + 删配置
- RescanAsync：强制重扫（SkipCache=true）
- OpenInExplorer：打开文件管理器
- ❌ 无文件浏览器（无树视图 / 无面包屑 / 无文件列表）
- ❌ 无排序
- ❌ 无单文件识别 / 添加（仅文件夹粒度）
- ❌ 无"手动添加媒体"按钮（即使从拖拽进来也走 DragDropFolderActionDialog）

#### 差距清单

- [ ] **P1** 文件浏览器（树视图 + 面包屑 + 文件列表 → 等价 FileExplorer，支持驱动器 / 目录 / 文件三栏）
- [ ] **P1** 单文件粒度操作（识别 / 手动添加），目前只支持文件夹粒度
- [ ] **P1** "媒体源"概念扩展：Web 端 SourcesPage 不只是监视文件夹，还包括单次扫描 / 单文件源；桌面端只支持监视文件夹这一种
- [ ] **P2** 排序选项（名称 / 修改日期 / 类型）
- [ ] **P2** 启用 / 禁用监视开关（不删配置只暂停监控）
- [ ] **P3** "手动添加媒体"按钮（依赖 ManualAddMediaDialog 等价 → §10.5）

---

### 5.2 媒体源详情（SourceDetailPage）

#### Web 端清单（`Pages/Sources/SourceDetailPage.razor`）

- 路由：`/source/{id}`
- 基本信息：路径 / 大小 / 文件数
- 媒体类型选择
- 识别状态显示
- 关联媒体查看
- 重新识别
- 打开文件位置

#### Desktop 端清单

- ❌ 完全无对应页

#### 差距清单

- [ ] **P3** 整页新建：`SourceDetailPage.axaml` + ViewModel（或并入 §1.3 MediaDetailWindow 的"源信息"区段）
- [ ] **P3** 取决于 §5.1 是否扩展到完整"媒体源"概念——如只保留监视文件夹，本节可降级为 §5.1 行项 expand

---

### 5.3 待处理（PendingMediaPage / UnknownPage）

#### Web 端清单（`Pages/Sources/UnknownPage.razor`）

- 路由：`/source/pending`（兼容 `/source/unknown`）
- 双 Tab：待识别（Identified=false）+ 待入库（Identified=true && InDatabase=false）
- 分类统计卡片 + 快速筛选
- 搜索 + 类型筛选
- 媒体源列表
- 批量入库 / 删除
- 单个识别 / 入库 / 重识别 / 丢弃
- 详情查看
- 手动添加媒体（每行）

#### Desktop 端清单（`Views/Pages/PendingMediaPage.axaml` + `PendingMediaViewModel`）

- 双 Tab：待识别 + 待入库
- 待识别：IdentifyAsync / DiscardIdentifyAsync
- 待入库：ApproveDatabaseAsync / ReidentifyAsync / DiscardDatabaseAsync
- 刷新两个列表
- ❌ ManualAddAsync 空实现（标 TODO Phase 2）
- ❌ PreviewDatabaseAsync 空实现（标 TODO Phase 1.3）
- ❌ 无分类统计 / 快速筛选
- ❌ 无搜索 / 类型筛选
- ❌ 无批量操作（批量入库 / 批量丢弃）

#### 差距清单

- [ ] **P0** ManualAddAsync 实现：补齐桌面端代码里 TODO Phase 2 的空实现，依赖 ManualAddMediaDialog 等价 → §10.5
- [ ] **P0** PreviewDatabaseAsync 实现：补齐 TODO Phase 1.3 的空实现（弹小窗预览 PendingIdentification 反序列化的 MediaBase）
- [ ] **P1** 批量操作：批量入库 / 批量丢弃（依赖 NineKgConfirmDialog DestructiveBatch，已有）
- [ ] **P1** 搜索 + 类型筛选（按 TopCategory chip）
- [ ] **P2** 分类统计卡 + 快速筛选（视频 / 音频 / 游戏 / 图片 / 文本 chip 计数）

---

## 6. 网站配置模块

### 6.1 网站页（WebsitePage）

#### Web 端清单（`Pages/Websites/WebsitePage.razor`）

- DLsite / Steam / Bangumi 启用开关
- 各网站支持的媒体类型 chip
- 网站优先级**拖拽排序**（SortableList）
- 各网站独立设置 dialog（DLsite Selenium / Bangumi ApiKey / Steam 区域语言）
- 配置保存

#### Desktop 端清单（`Views/Pages/WebsitesPage.axaml` + `WebsitesViewModel`）

- DLsite：启用 + Selenium 评分开关
- Bangumi：启用 + ApiKey 输入（掩码末 4 位）+ 跳转申请页
- Steam：启用 + 语言选择（4 选）+ 区域选择（6 选屏蔽 cn）
- 500ms 防抖自动保存
- ❌ 无优先级拖拽排序（需要 SortableList 等价控件）

#### 差距清单

- [ ] **P2** 优先级拖拽排序（依赖 SortableList 等价控件 → §10.5；Avalonia 的 ItemsRepeater + DragDrop API 可实现，参考 FluentAvalonia ListBox 拖拽样例）
- [ ] **P3** 网站启用前自动检测：Bangumi ApiKey 测试 / Steam CountryCode 验证 / DLsite 网络可达性

---

## 7. 收藏夹模块

### 7.1 收藏夹页（FavoritesPage）

#### Web 端清单（`Pages/Favorites/FavoritesPage.razor`）

- 收藏夹列表侧边栏
- 创建 / 重命名 / 删除
- 收藏夹媒体列表
- 媒体批量操作：移除、批量删除
- 媒体筛选 + 排序

#### Desktop 端清单（`Views/Pages/FavoritesPage.axaml` + `FavoritesViewModel`）

- 侧栏列表
- 选中收藏夹 → 主区加载关联媒体
- NewFavoriteAsync：创建（自动生成名）
- DeleteSelectedAsync：删除（保留媒体）
- 默认收藏夹自动选中
- ❌ 无重命名
- ❌ 无媒体批量移除
- ❌ 无筛选 / 排序
- ❌ 无 Hero 区或统计

#### 差距清单

- [ ] **P1** 收藏夹重命名（侧栏右键或选中后顶部按钮）
- [ ] **P1** 媒体批量移除（多选 + "从此收藏夹移除"，区别于"删除媒体"）
- [ ] **P2** 媒体筛选（标签 / 评分）+ 排序（加入时间 / 标题 / 评分）
- [ ] **P3** Hero 区 / 收藏夹统计（媒体数 / 占用空间 / 类别分布）

---

## 8. 全局搜索模块

### 8.1 搜索结果页（SearchResult）+ 搜索框（GlobalSearchBox）

#### Web 端清单（`Pages/Search/SearchResult.razor` + `Components/Search/*`）

- 路由：`/search`
- GlobalSearchBox（导航栏全局搜索框）
- 搜索关键词输入
- AI 语义搜索开关
- 搜索类型选择：媒体 / 标签 / 社团 / 创作者
- 结果分组展示
- 筛选器对话框（SearchFilterDialog）
- 分页

#### Desktop 端清单

- ❌ **完全没有搜索功能**：无搜索入口、无结果页、无 AI 语义搜索、无 GlobalSearchBox 等价控件
- ⚠ 设置页里有"搜索"组（缓存 / 并发数 / 超时 / 最小相关度等参数）但**没有真正的搜索 UI 入口**

#### 差距清单

> **这是桌面端最大的功能空白**——后端已有 SearchService + 向量库，纯缺 UI。

- [ ] **P0** 全局搜索入口（§12 已决策：双入口）：①左侧栏底部 SearchBar（NavigationView FooterMenuItems）②Ctrl+K 全局快捷键唤出命令面板风格搜索
- [ ] **P0** 搜索结果整页：`SearchResultPage.axaml` + ViewModel（媒体 / 标签 / 社团 / 创作者 4 类型分组展示）
- [ ] **P0** AI 语义搜索开关 + 调用：复用 Core 已有 `SearchService` + 向量库；桌面端只缺 UI
- [ ] **P1** 搜索筛选 dialog（SearchFilterDialog 等价：类型 + 时间范围 + 评分）
- [ ] **P1** 分页（结果可能上千条）
- [ ] **P2** 搜索结果预览卡（SearchResultPreview 等价：高亮匹配字段 + 上下文）
- [ ] **P2** 搜索历史 / 建议（桌面端可比 Web 做得更原生：本地 SQLite 缓存）
- [ ] **P3** 可选 Ctrl+F 作为页内查找快捷键（区别于 Ctrl+K 全局搜索）

---

## 9. 设置模块

### 9.1 系统设置（Settings）

#### Web 端清单（`Pages/Settings/Settings.razor`）

- 应用基础配置：代理、User-Agent、账号管理
- 日志输出配置
- AI 与识别配置
- 网站优先级管理（嵌入设置页内）
- 修改用户名 / 密码
- 代理连接测试
- 配置全部保存 / 重置

#### Desktop 端清单（`Views/Pages/SettingsPage.axaml` + `SettingsViewModel`）

桌面端覆盖比 Web **更全**：

- **外观**：主题（System/Light/Dark）+ 关窗行为（最小化到托盘 / 退出）+ Shell 集成（Win）
- **任务**：并发数、重试次数
- **识别**：超时、自动入库、待处理保留天数、跳过缓存、相似度阈值
- **媒体源**：监视文件夹列表编辑（与 SourcesPage 重叠）
- **文件**：最小尺寸、跳过隐藏 / 系统文件
- **AI**：启用 + 关键词拆分 + OpenAI Key/BaseDomain/ApiVersion/Model
- **标签匹配**：模糊 + 相似度 + 包含 + 归一化 + 最大结果数
- **搜索**：全局搜索、缓存、过期时间、并发、超时、最小相关度
- **日志**：日志级别
- **数据库**：路径只读
- TestShellIntegration / ResetShellRegistration / ToggleShellIntegration
- OpenDataDirectory / ClearCache / ResetDefaults（危险操作）
- 500ms 防抖保存
- ❌ 无代理配置 / User-Agent 配置 / 账号管理 / 修改密码（桌面端无认证体系，可以忽略）
- ❌ 无代理连接测试

#### 差距清单

> 设置页是桌面端少数比 Web 还全的页面，差距条目主要是 Web 端 Web 特有项。

- [x] ~~**P3** 代理 / User-Agent / 账号管理~~ — **§12 已决策：不做**（单用户场景不需要 Web 端管理后台功能）
- [x] ~~**P3** 代理连接测试按钮~~ — **§12 已决策：不做**（依赖项已不做）
- [ ] **P3** 分组级"重置默认"（目前 ResetDefaults 是全量重置，可考虑加分组粒度）

---

## 10. 共享基础设施

### 10.1 对话框体系

#### Web 端

- NineKgConfirmDialog（4 Intent：Info / Affirmative / Destructive / DestructiveBatch）
- LoadingDialog
- 各业务 dialog（详见 0.2 矩阵）

#### Desktop 端

- NineKgConfirmDialog（4 Intent，与 Web 语义一致）
- DragDropFolderActionDialog（桌面独占）
- CreatorMergeDialog（桌面独占）
- TagEditorDialog / TopTagEditorDialog（桌面独占）
- ❌ 无 LoadingDialog 等价（用 ProgressRing 内联）

### 10.2 媒体卡片 / 列表 / 网格

| 形态 | Web | Desktop |
|---|---|---|
| 完整卡片 | MediaCard | MediaCardViewModel + AXAML 模板 |
| 简化卡片 | SimpleMediaCard | ❌ |
| 完整列表项 | MediaListItem | ❌ |
| 简化列表项 | SimpleMediaListItem | ❌ |
| 网格容器 | MediaGrid（含 Simple 切换） | ItemsRepeater + 模板 |
| 列表容器 | MediaList（含 Simple 切换） | ❌ |
| 统一展示 | MediaShownView（封装筛选 / 排序 / 分页 / 视图切换） | MediaOverviewViewModel + 自实现 |

### 10.3 通用组件

| 组件 | Web | Desktop |
|---|---|---|
| 页面头 | PageHeader | 各页内联 Hero 区 |
| 分区卡 | SectionCard | 各页内联 Border |
| 提示行 | Tip | InfoBar（FluentAvalonia） |
| 加载弹窗 | LoadingDialog | ProgressRing 内联 |
| 别名编辑 | EditableAliasList | ❌ |
| 描述编辑 | DescriptionEditor | ❌ |
| 信息编辑 | InfoEditor | ❌ |
| 收藏选择 | FavoriteSelector | ❌ |
| 拖拽排序 | SortableList | ❌ |
| 图片上传 | ImageUploadDialog | ❌ |
| 图片墙 | PhotoWall | ❌ |

### 10.4 桌面端独占基础设施

- **NavigationService**：主窗内页面切换 + OnEnter/OnLeave 生命周期
- **WindowManager**：非主窗管理（位置记忆 / Activate 已有窗口 / CloseAll）
- **TrayService**：系统托盘 + 4 状态图标
- **DragDropDispatcher**：原生文件拖拽分发（单文件 / 文件夹 / 多项三分支）
- **IpcService**：单实例 + NamedPipe 命令转发
- **ShellIntegrationService**：Windows 资源管理器右键集成
- **WindowStateService**：窗口位置 / 大小持久化
- **DesktopPreferences**：桌面独有 UI 偏好
- **ImageCacheService**：图片 LRU 缓存（200 张）
- **TopCategoryStyles**：TopCategory enum → brush / icon 映射
- **WeakReferenceMessenger**：跨 VM 事件（如 ImageInvalidatedMessage）

> 这些都是 Web 端不需要的桌面差异化能力，纯增量。

### 10.5 桌面端待补的共享控件清单（按依赖关系反推）

> 多个页面差距条目共同依赖的"基础组件"，先做这些再做页面差距事半功倍。优先级与"被多少 Pn 条目依赖"挂钩。

| 优先级 | 控件 | Web 等价 | 影响的差距条目 |
|---|---|---|---|
| **P0** | ManualAddMediaDialog | ManualAddMediaDialog | §1.2 / §1.3 / §5.1 / §5.3 |
| **P0** | MediaFilterDialog | MediaFilterDialog | §1.2 |
| **P0** | TagSelectorDialog | TagSelectorDialog | §1.3 + 多处编辑 |
| **P0** | CreatorSelectorDialog | CreatorSelectorDialog | §1.3 / §4.x |
| **P0** | CategorySelectorDialog | CategorySelectorDialog | §1.3 |
| **P1** | FileExplorer 等价 | FileExplorer + DirectoryTreeView | §5.1（也可考虑直接用 OS 原生 picker） |
| **P1** | FavoriteSelector | FavoriteSelector | §1.3 |
| **P1** | EditableAliasList | EditableAliasList | §4.2 / §4.4 |
| **P1** | DescriptionEditor + AI 翻译 | DescriptionEditor | §1.3 / §4.2 / §4.4 |
| **P1** | ImageUploadDialog | ImageUploadDialog | §1.3 / §4.2 / §4.4 |
| **P1** | TaskTreeNode 等价 | TaskTreeNode | §2.1 / §2.2 |
| **P1** | TaskDetailsDialog | TaskDetailsDialog | §2.2 |
| **P1** | TaskLogViewer | TaskLogViewer | §2.2 |
| **P1** | SortableList | SortableList | §6.1 |
| **P2** | IdentificationOptionsDialog | IdentificationOptionsDialog | 手动选候选场景 |
| **P2** | MediaListView + MediaListItem | MediaList + MediaListItem | §1.2（视图切换） |
| **P2** | MediaShownView 等价封装 | MediaShownView | §1.2 全面（可选：把 MediaOverviewViewModel 重构成可复用容器） |
| **P2** | TaskQuickViewDialog | TaskQuickViewDialog | §2.x |
| **P2** | TaskHistoryDetailsDialog | TaskHistoryDetailsDialog | §2.4 |
| **P2** | EntityListView 通用控件 | n/a（Web 是各页内联） | §4.1 / §4.3（Creators / Circles 共享） |
| **P3** | PhotoWall | PhotoWall | 创作者 / 社团相册 |
| **P3** | IdentificationLoadingDialog | IdentificationLoadingDialog | 单文件识别独立 dialog |
| **P3** | SimpleMediaCard / SimpleMediaListItem | SimpleMediaCard / SimpleMediaListItem | 紧凑视图（可选） |

### 10.6 桌面端待补的通用基础组件

| 优先级 | 通用组件 | Web 等价 | 当前替代 |
|---|---|---|---|
| **P3** | 统一 PageHeader | PageHeader | 各页内联 Hero 区，可抽 BrandHeroSection 减少重复 |
| **P3** | 统一 SectionCard | SectionCard | 各页内联 Border |
| **P3** | LoadingDialog 等价 | LoadingDialog | ProgressRing 内联，可统一为带 backdrop 的 modal |
| **P3** | Tip / 提示行 | Tip | 直接用 FluentAvalonia InfoBar，可考虑包一层主题统一的 NineKgTip |

---

## 11. 待补 / 未盘点

以下子区域**本次盘点未深入展开**，做对比时需要二次确认：

- **MediaShownView 内部细节**：Web 端的 MediaShownView 是个非常重的复合组件（封装筛选 / 排序 / 分页 / 视图切换 / 选择 / 批量操作），桌面端 MediaOverviewViewModel 是自实现简化版，**两者具体的功能 diff 需要逐字段对比**而非"页面级"对比
- **TaskTreeNode 父子关系**：Web 端任务页是树状（一个父任务展开多个子任务），桌面端是平铺；**这影响后台任务页的整个交互模型**
- **识别诊断的展示完整度**：双端都有 IdentificationDiagnosticsView，但具体字段（关键词 / 候选 / 选中标记）是否完全等价需 diff（视觉上桌面端更紧凑）
- **Settings 字段**：桌面端比 Web 全，但 Web 端的"代理 / User-Agent / 账号管理 / 修改密码"在桌面端是否真的不需要，要确认
- **路由设计差异**：Web 是基于 URL 的多路由（创作者详情独立路由 / 标签详情独立路由 / 媒体详情独立路由 + edit query），桌面端是双态嵌入 + 独立窗口；**信息架构差异**比单页面差距更深，对比时需要分两层（IA 层 / 功能层）

## 12. 决策（已拍板，2026-05-06）

- **Web 端功能复刻范围**：仅复刻"功能层"等价；桌面端单用户场景**不复刻**认证 / 账号管理 / 修改密码 / 代理测试等 Web 独有项
- **优先级**：先做"媒体详情编辑模式（§1.3）+ 系列 P0 dialog（§10.5）"，再做"全局搜索（§8.1）"。理由：§1.3 是基础正确性（已能进库的媒体必须能管理），且 §1.3 落地的 5 个 P0 dialog 副产物高（顺带解锁 §1.2 / §5.1 / §5.3 多个差距）
- **创作者 / 社团详情**：嵌入主窗，**不**开独立窗口。CreatorsPage 现有"列表 ↔ 详情双态"模式扩展到 CirclesPage 即可
- **任务总览 + 历史 + 定时**：合并为 BackgroundTasksPage 的 **3 个 Tab**（运行中 / 历史 / 定时）。**不**做独立的 TasksPage / TaskExecutionHistoryPage / ScheduledTasksPage 三页
- **全局搜索入口**：**双入口** —— 左侧栏底部 SearchBar（NavigationView FooterMenuItems）+ Ctrl+K 全局快捷键唤出命令面板风格搜索

## 13. 推荐实施顺序（基于 §12 决策）

按"先打地基（§10.5 P0 共享控件）→ 再补关键页面（§1.3 媒体编辑闭环）→ 然后体验差异化（§8.1 搜索 + §2.x Tab 化任务）"的节奏。

### 第一波：打地基（§10.5 P0 共享 dialog）

1. **ManualAddMediaDialog** — 解锁 §1.2 / §1.3 / §5.1 / §5.3
2. **TagSelectorDialog** — 解锁 §1.3 标签编辑
3. **CreatorSelectorDialog** — 解锁 §1.3 + §4.x
4. **CategorySelectorDialog** — 解锁 §1.3 分类编辑
5. **MediaFilterDialog** — 解锁 §1.2 多维筛选

### 第二波：媒体管理闭环（§1.3 + §1.2 + §5.3 P0）

6. MediaDetailWindow **编辑模式**（创作者 / 社团 / 标签 / 收藏 / 分类编辑）
7. MediaOverview **多维筛选** + **手动新建入口**
8. PendingMedia **ManualAddAsync** + **PreviewDatabaseAsync**（清两个 TODO）

### 第三波：任务页 Tab 化（§2.x 合并）

9. BackgroundTasksPage 重构为 **运行中 / 历史 / 定时** 三 Tab
10. 顺带补 P1 共享：**TaskTreeNode** + **TaskDetailsDialog** + **TaskLogViewer**（§10.5）

### 第四波：全局搜索（§8.1）

11. **侧栏底部 SearchBar 入口** + **Ctrl+K 命令面板**
12. **SearchResultPage** 整页 + AI 语义搜索复用 Core SearchService

### 第五波：辅助页面 + 通用组件（§3.x / §4.x / §6 / §7）

13. **CirclesPage** 整页（§4.3）+ 创作者 / 社团详情态扩展编辑（§4.2 / §4.4）
14. **标签详情页**（§3.2）+ **标签映射**（§3.3）
15. 收藏夹重命名 + 批量移除（§7.1）
16. 网站优先级拖拽（§6.1，依赖 SortableList 等价控件）

### 已确认不做（§12 决策）

- 桌面端**不做**：认证 / 修改密码 / 代理测试 / 账号管理（Web 端独有的服务端管理后台性质功能）
- **不做**：独立的 TasksPage / TaskExecutionHistoryPage / ScheduledTasksPage 三页（合 Tab）
- **不做**：创作者 / 社团独立窗口（嵌入主窗双态）
