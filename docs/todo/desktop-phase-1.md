# Desktop Phase 1：核心信息架构

> **目标**：把日常使用频次最高的 5 个核心页面跑通，桌面端 MVP 可用——能浏览媒体库、看详情、跑识别任务、处理待识别/待入库的媒体源。
>
> **估时**：4–6 周
>
> **前置条件**：Phase 0 已完成（`AppBootstrap` 接通、Avalonia 骨架可启动、独立 db 路径生效）

---

## 视觉设计基线（先读这一节）

所有 Phase 1 页面共享如下基线决策。**任何页面 AXAML / 控件选择与下面冲突时，以基线为准**。

### 设计语言：纯 Win11 Fluent，不强加品牌色

- **不**为桌面端定制一套品牌主色覆盖 FluentAvalonia 默认。`App.axaml` 里 `<FluentAvaloniaTheme PreferUserAccentColor="True" />`——主色跟随用户系统 accent color，让用户改 Win11 主题色时桌面端跟着变（这是原生应用的核心标志）
- 品牌识别**不靠色卡**，靠：应用图标、启动闪屏、关于页、信息架构。两个端（Web/Desktop）外观不一致是合理的，强求一致反而稀释 Win11 原生感
- **唯一例外**——以下两处用一套项目自定义渐变作为**点缀**：
  - **Hero 渐变**：媒体详情页顶部封面背景虚化 + 渐变叠层（深蓝→深紫，alpha 0.85→0.4），让封面图融入窗口而不是硬切
  - **对话框 Intent accent**：四种 Intent（Info/Affirmative/Destructive/DestructiveBatch）顶部 4px 高度色条，色值为 FluentAvalonia 的 `SystemFillColorAttention/Success/Critical/Critical`——使用系统语义色不引入项目色
- **结论**：99% 的 brush 走 FluentAvalonia 默认 resource key，不做覆盖

### 信息密度：适中（默认）+ 视图模式切换（兜底）

- **媒体库默认密度**：每行 6–8 张卡片（实际数量响应式由窗宽决定，单卡最小宽 160px / 最大宽 220px，由 `UniformGrid.MinItemWidth` 控制）。1080p 下大约 6 张/行 + 一屏 3 行 = 18 条；1440p 下 7–8 张/行 + 4 行 = ~30 条
- **不做** Spacious / Comfortable / Compact 三档预设——MVP 阶段过度工程；如果用户反馈再加
- **视图模式切换** 单一开关——筛选条右端"网格 / 列表"按钮：
  - **网格视图**（默认）：上述卡片
  - **列表视图**：`DataGrid`，列：缩略图 (40x60) / 标题 / 创作者 / 评分 / 顶级标签 / 添加时间。给"我现在就要看表格"的极端场景兜底
- 切换由用户自选，记忆到 `localAppData/.../window-state.json`，不进 `config.yaml`（这是用户偏好不是应用配置）

### 主题：跟随系统

- `Application.RequestedThemeVariant="Default"`——跟随 Win11 浅色/深色设置
- Settings 页提供"跟随系统 / 浅色 / 深色"三选一，写到 window-state.json
- **所有自定义视觉**（Hero 渐变、对话框 accent）必须在浅深两套主题下都验证

### 间距 / 圆角 / 字号 scale

直接用 FluentAvalonia / WinUI 标准 scale，不发明新单位。

- **间距**：4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 / 56 / 64（WinUI 标准 8pt grid + 局部 4pt 微调）
- **圆角**：`ControlCornerRadius`（4，按钮/输入框）/ `OverlayCornerRadius`（8，Flyout/卡片）/ 自定义 `BrandLargeRadius`（12，对话框 / Hero）
- **字号**：12（Caption）/ 13（Body）/ 14（BodyStrong）/ 16（Subtitle）/ 20（Title）/ 28（TitleLarge）/ 40（Display）—— 全部用 FluentAvalonia 命名空间下 TextBlock TextStyle，不写死像素

### 动效

- **页面切换**：NavigationView 默认 EntranceNavigationTransitionInfo（0.16s）
- **卡片 hover**：scale 1.0 → 1.02，Y -2px，shadow 加深；80ms easeOut
- **对话框打开**：240ms slide-up + fade
- **Snackbar / InfoBar 入场**：200ms slide-in
- **进度条**：实时数据更新用 `Transitions` 平滑插值（150ms），避免数字跳变
- **加载骨架**：1.4s pulse loop，opacity 0.4 → 0.7 → 0.4

### 无障碍 / 键盘可达

- 所有交互控件 `IsTabStop="True"`，Tab 顺序：侧栏 → 命令栏 → 主内容 → 分页 → 状态栏
- 主功能快捷键：
  - `Ctrl+1..0` 跳侧栏对应项
  - `Ctrl+F` 聚焦命令栏搜索框
  - `Ctrl+,` 打开 Settings
  - `Ctrl+W` 关当前非主窗
  - `F5` 刷新当前页
- 拔掉鼠标必须能完整跑核心流程（媒体库浏览 → 详情 → 编辑保存）

### 空状态 / 加载 / 错误 — 三件套强制

每个数据驱动的页面**必须**实现这三个状态，不允许"白屏 + spinner"了事：

- **空状态**：手绘风插画 80x80 + 一句话原因 + 一个明确的 CTA 按钮
- **加载**：骨架卡片占位（媒体库网格直接放 12 个骨架卡），不用居中 spinner
- **错误**：错误图标 + 脱敏消息（"加载失败，请稍后重试。"，**不**显示 ex.Message）+ "重试"按钮 + 详情可展开（折叠区里允许显示技术信息供 debug）

---

## 1. 主窗 + NavigationView 框架

替换 Phase 0 占位主窗，搭建侧栏导航 + 主内容区的真正应用骨架。

### 任务

- [ ] **主窗骨架**：`Views/MainWindow.axaml` 重写为 `NavigationView`（`FluentAvalonia.UI.Controls.NavigationView`）+ 内容 Frame
- [ ] **侧栏菜单项** 对齐 Web 的 [`Components/Layout/NavMenu.razor`](../../NineKgTools.Web/Components/Layout/NavMenu.razor)：首页 / 媒体库 / 待处理 / 媒体源 / 任务 / 标签 / 创作者 / 收藏夹 / 网站；Settings 钉到 pane 底部（`NavigationView.IsSettingsVisible="True"` 自带）
- [ ] **窗口 Mica + 标题栏自绘**：`ExtendClientAreaToDecorationsHint=True` + `TransparencyLevelHint="Mica,AcrylicBlur,Blur"`；自定义标题栏内容（左侧 Logo + 应用名，右侧 Win 标准三键由系统 chrome 提供）
- [ ] **导航服务**：新建 `Services/NavigationService.cs`，提供 `NavigateTo<TViewModel>(params)` API；ViewModel 通过 DI 拿 NavigationService 实现页间跳转
- [ ] **路由参数体系**：媒体详情 / 创作者详情这种"带 id 跳转"的页面，NavigationService 要支持 `NavigateTo<MediaDetailViewModel>(mediaId)` 模式
- [ ] **页面 ViewModel 基类**：`ViewModels/PageViewModelBase.cs`（`ObservableObject` + `OnEnterAsync` / `OnLeaveAsync` 生命周期钩子）
- [ ] **首页 Placeholder**：`Views/Pages/HomePage.axaml` 简单仪表盘（媒体总数 / 待处理总数 / 最近任务），对应 Web 的 [`Pages/Home.razor`](../../NineKgTools.Web/Pages/Home.razor)

### 设计稿

```
┌────────────────────────────────────────────────────────────────┐
│ ◆ NineKgTools                            [Search...]   _ □ ✕  │  ← Mica + drag area, 标题栏 32px
├────────┬───────────────────────────────────────────────────────┤
│  ◇首页 │                                                       │
│  ▶媒体 │  CommandBar  [新建] [筛选] [排序▾] [视图◫]    [搜索🔍]│  ← Page chrome 48px
│  ●待处│  ─────────────────────────────────────────────────────│
│  ▦媒源 │                                                       │
│  ⚙任务 │   主内容区（Frame）                                   │
│  ＃标签│                                                       │
│  👤创作│                                                       │
│  ★收藏│                                                       │
│  🌐网站│                                                       │
│  ──── │                                                       │
│  ⚙设置 │                                                       │
└────────┴───────────────────────────────────────────────────────┘
   240px        响应式 (≥640px → Expanded, <640 → Compact 48px)
```

### 关键决策

- **PaneDisplayMode**：`Auto` 响应窗宽——窗口 ≥ 1008px 用 `Left`（240px expanded），640–1008 用 `LeftCompact`（48px 图标 + 悬浮展开），<640 用 `LeftMinimal`（汉堡按钮）
- **标题栏 vs CommandBar**：标题栏只放 Logo + 应用名 + 全局 Search（`AutoSuggestBox`，跨所有页面快查媒体）；每页面顶部各自一条 CommandBar 放页面级动作。两条不合并，避免 CommandBar 高度被全局元素挤占
- **Mica fallback**：Win11 → Mica；Win10 → Acrylic；Mac/Linux → 纯色（FluentAvalonia 自动降级）
- **首页仪表盘**：3 张卡片横排（媒体总数 / 待处理总数 / 任务运行中），点击各自跳到对应页面；下方"最近任务"用紧凑列表展示最近 10 条
- **键盘**：`Ctrl+1..9` 对应侧栏前 9 项，`Ctrl+,` 打开 Settings

### 验收

1. 启动主窗显示 Mica 背景 + 侧栏 + 首页占位
2. 点击侧栏每一项能切换内容区
3. 窗口大小拖拽时 NavigationView 自动从 Expanded 折叠到 Compact / Minimal
4. 标题栏 drag region 能拖动窗口；Logo + 应用名区域不会拦截拖动
5. `Ctrl+1` 跳到首页，`Ctrl+2` 跳媒体库……

---

## 2. 媒体库总览（最高频页面）

对应 Web 的 [`Pages/Medias/MediaOverviewPage.razor`](../../NineKgTools.Web/Pages/Medias/MediaOverviewPage.razor) + [`Components/Medias/MediaShownView.razor`](../../NineKgTools.Web/Components/Medias/MediaShownView.razor)。

### 任务

- [ ] **VM**：`ViewModels/Pages/MediaOverviewViewModel.cs`，依赖 `MediaService` + `IDbContextFactory<MediaDbContext>`
- [ ] **分类切换栏**：5 张可选大卡（视频 / 音频 / 游戏 / 图片 / 文本），各显示分类计数；选中态有 accent 色边框 + 浅色背景
- [ ] **筛选条**：`AutoSuggestBox`（标签）+ `ComboBox`（创作者 / 社团）+ 排序下拉 + 收藏夹下拉 + 视图模式按钮（网格 / 列表切换）
- [ ] **媒体卡片网格**：`ItemsRepeater` + `UniformGrid`；卡片 portrait 比例 2:3（封面图）+ 元数据栏；详情见下方设计稿
- [ ] **列表视图**（次选）：`DataGrid` 列：缩略图 / 标题 / 创作者 / 评分 / 顶级标签 / 添加时间；用户切换后记到 window-state
- [ ] **封面图加载**：`Image` Source 绑定 `BitmapAsync` converter，从 `ImageService.GetImageContentAsync(imageId)` 拉 BLOB；走任务 9 的 LRU 缓存
- [ ] **分页**：底部 FluentAvalonia `Pagination` 控件，与现有 X.PagedList 后端兼容；分页 + 顶部"共 N 条"统计
- [ ] **空 / 加载 / 错误三态**：参考视觉基线"三件套"
- [ ] **路由参数**：`/media/overview/{category}` 支持直链
- [ ] **新建媒体按钮**：CommandBar 左端"新建"按钮 → `MediaKindPickerDialog` → 文件浏览 → `ManualAddMediaDialog`

### 设计稿

```
分类切换栏（高 110px）
┌──────────┬──────────┬──────────┬──────────┬──────────┐
│ 🎬 视频   │ 🎧 音频   │ 🎮 游戏   │ 🖼 图片   │ 📖 文本   │
│  1,247    │   382     │   89      │   215     │   46      │
└──────────┴──[选中态]─┴──────────┴──────────┴──────────┘   ← 选中卡 accent 边框 2px

筛选条（高 56px，CommandBar 风格）
┌────────────────────────────────────────────────────────────┐
│ 标签🔍 [体育, 纪录片  ▾]  创作者[全部▾] 排序[最近添加▾] [◫][≡]│
└────────────────────────────────────────────────────────────┘

媒体卡片网格（自适应列数 6–8 列；单卡 ~190x320）
┌────────┬────────┬────────┬────────┬────────┬────────┬────────┐
│  封面  │  封面  │  封面  │  封面  │  封面  │  封面  │  封面  │  ← cover 190x270
│  2:3   │  2:3   │  2:3   │  2:3   │  2:3   │  2:3   │  2:3   │
│        │        │        │        │        │        │        │
├────────┤        │        │        │        │        │        │
│ 标题…  │        │        │        │        │        │        │  ← Title 14px ellipsis
│ 创作者 │        │        │        │        │        │        │  ← Subtitle 12px opacity 0.7
│ ★4.2   │        │        │        │        │        │        │  ← Rating row
│#tag1#t2│        │        │        │        │        │        │  ← Tags row (max 2 chips)
└────────┴────────┴────────┴────────┴────────┴────────┴────────┘

底部分页（高 56px）
                ┌──────────────────────────────┐
                │  共 1,247 条  [‹ 1 2 3 ... 64 ›] │
                └──────────────────────────────┘
```

### 关键决策

- **卡片 hover 态**：scale 1.02 + 阴影深一档 + 右上角浮现"⋯"按钮（更多动作 flyout）；过渡 80ms
- **封面 fallback**：没封面 → 浅灰底 + 居中分类图标（视频/音频/游戏图标），不显示文字
- **分类切换栏可隐藏**：用户在某个分类久了可以折叠分类栏（Settings 选项），节省垂直空间
- **筛选条不固定**：随主内容滚动一起滚走，不做 sticky——纯滚动期间不需要它
- **分页位置**：固定在底部，不和滚动区一起滚——避免要"往下拉到底"才能换页
- **列表视图复用 ItemsRepeater 数据源**——只是换 ItemTemplate，VM 不变

### 验收

1. 切换分类卡片，列表实时更新
2. 输入筛选条件 + 排序，列表正确刷新（VM state 内部记录，不动 URL）
3. 点击卡片打开媒体详情独立窗（任务 3）
4. 1000 条媒体的库滚动 60fps（虚拟化生效）
5. 切换页码不闪屏（图片走 LRU 缓存命中即时显示，未命中先 placeholder）
6. 网格 ↔ 列表切换 → 关窗重开 → 上次的视图模式恢复

---

## 3. 媒体详情页（独立窗口体验）

对应 Web 的 [`Pages/Medias/MediaPage.razor`](../../NineKgTools.Web/Pages/Medias/MediaPage.razor)。**桌面端差异化点**：每点开一个媒体就一个独立 `Window`，不是替换主窗。

### 任务

- [ ] **VM**：`ViewModels/Pages/MediaDetailViewModel.cs`，依赖 `MediaService` / `ImageService` / `TagService` / `FavoriteService`
- [ ] **新窗口**：`Views/Windows/MediaDetailWindow.axaml`（独立 `Window`，不在 NavigationView 内）
- [ ] **顶部 Hero 区**：高 380px，封面图 blur fill + 项目自定义 Hero 渐变叠层；右下角悬浮"在文件管理器打开 / 立即识别 / 加入收藏夹"动作组
- [ ] **Hero 上的标题栏内容**：标题（28px）/ 副标题（创作者 · 社团 · 评分）/ 操作按钮组
- [ ] **下方主内容**：`TabView`（FluentAvalonia） — Tab："详情" / "媒体内容"（图集/集数/平台依类型）/ "文件" / "历史"
- [ ] **右侧标签栏**（默认展开，可折叠）：标签 chip cloud + 收藏夹 + 元数据 key-value
- [ ] **编辑模式**：右上"编辑"图标按钮切换；切换后 Hero 内容 chrome 替换为输入控件，Tab 内容替换为表单
- [ ] **保存条**：编辑模式下底部固定一条 `CommandBar` —— 取消 / 保存按钮
- [ ] **`forceEditMode` 入口**：从 `ManualAddMediaDialog` 返回 `FullyFilled=false` 直接进入编辑态——VM 构造时的 `forceEditMode` 参数

### 设计稿

```
独立窗口 (≥ 1024×720)
┌─────────────────────────────────────────────────────────────┐
│ NineKgTools · 标题…                              📌 ✏️ _□✕ │  ← 自绘 chrome 32px (置顶 / 编辑)
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   [封面 blur fill, 380px tall]                              │  ← Hero region
│   ╔═══════════════════════════════════════════════════════╗ │      渐变叠层（深→透明）
│   ║                                                       ║ │
│   ║  巨大封面缩略 (240x320)   📺 标题（28px）            ║ │
│   ║                            创作者 · 社团              ║ │
│   ║                            ★ 4.2 (123 评)            ║ │
│   ║                                                       ║ │
│   ║                            [📁 文件管理器] [🔍 重识别] ║ │  ← 主操作组
│   ║                            [★ 收藏夹]   [⋯]            ║ │
│   ╚═══════════════════════════════════════════════════════╝ │
│                                                             │
├──────────────────────────────────────────────┬──────────────┤
│  Tab: [详情] [媒体内容] [文件] [历史]        │  右侧栏 320px│
│  ────────────────────────────                │  ──────────  │
│                                              │  标签 chips  │
│  简介                                        │  #tag1 #tag2 │
│  ─────────────────                           │  #tag3 #tag4 │
│  Lorem ipsum...                              │              │
│                                              │  收藏夹      │
│  别名                                        │  ── ──       │
│  · 别名 1                                    │  ★ 默认收藏 │
│  · 别名 2                                    │              │
│                                              │  元数据      │
│  ...                                         │  添加: 5/3   │
│                                              │  路径: …     │
└──────────────────────────────────────────────┴──────────────┘

编辑模式（虚框 = 输入控件）：
┌─ Hero ────────────────────────┐
│ ╔══════ 封面（点击换图）════╗ │
│ ║ [📺 标题输入框 ─────────]  ║ │
│ ║ [创作者输入框 ──────────]  ║ │
│ ║ ★ 评分滑块 ─────────       ║ │
│ ╚════════════════════════════╝ │
└────────────────────────────────┘
... Tab 内容也替换为表单 ...
┌─ 底部固定 SaveBar ────────────┐
│             [取消]  [保存修改] │  ← 50px 高，accent 色
└────────────────────────────────┘
```

### 关键决策

- **窗口默认尺寸** 1024×720；最小 720×540；可记忆每窗口的 size + position 到 window-state.json
- **Hero 渐变** 是设计基线唯一品牌点缀点（见基线"设计语言"）；浅色主题渐变浅蓝/浅紫，深色主题深蓝/深紫
- **多窗口隔离**：每个 detail 窗有自己的 DI scope（独立 DbContext scope）；关窗时 `Dispose` 释放 scope
- **不做"上一个/下一个媒体"快捷键**——多窗口模式下用户应该用 Alt+Tab 切窗口，而不是在单个窗口内翻
- **置顶图钉**：右上角 chrome 的 📌 按钮设 `Topmost=True`，便于一边看详情一边操作主窗
- **类型特定内容** 在"媒体内容" Tab 里，用 partial DataTemplate（`PictureMediaContentView` / `VideoMediaContentView` / 等）按 `TopCategory` 选模板

### 验收

1. 主窗点击 3 张卡片 → 桌面 3 个独立详情窗 + Alt+Tab 列表正确显示
2. 关闭其中一个不影响其他
3. 编辑保存 → 主窗对应卡片自动刷新（用 CommunityToolkit.Mvvm `WeakReferenceMessenger` 发 `MediaUpdatedMessage`）
4. 主窗关闭时所有 detail 窗一并关闭
5. 关掉某个 detail 窗 → 应用关闭后再启动并打开同一 media → 窗口位置和大小恢复到上次

---

## 4. 后台任务（BackgroundTasksPage）

对应 Web 的 [`Pages/Tasks/`](../../NineKgTools.Web/Pages/Tasks/) + [`Components/Tasks/`](../../NineKgTools.Web/Components/Tasks/)。

### 任务

- [ ] **VM**：`ViewModels/Pages/BackgroundTasksViewModel.cs`，订阅 `TaskProgressService.OnProgressChanged`
- [ ] **顶部状态过滤器** chip group：全部 / 运行中 / 排队 / 已完成 / 失败；选中状态高亮
- [ ] **任务行卡片**：状态图标 + 名称 + 类型 badge + 进度条（仅运行中）+ 时间信息 + 末端动作组
- [ ] **任务详情对话框**：`Views/Dialogs/TaskDetailsDialog.axaml`，含识别诊断 Tab（依赖 Phase 2 任务 1）
- [ ] **进度推送**：用 `Dispatcher.UIThread.Post` 切回 UI 线程；用 `Transitions` 平滑插值进度数字
- [ ] **批量操作**：行选中 checkbox + 底部浮现 ActionBar（"全部取消 / 全部清除"）
- [ ] **空状态**：[设计基线"三件套"]，CTA "去媒体源识别"跳到 SourcesPage

### 设计稿

```
状态过滤器（高 48px）
┌──────────────────────────────────────────────────────┐
│ [全部 142]  [运行中 5]  [排队 8]  [已完成 120]  [失败 9] │  ← chip group, 选中态 accent 填充
└──────────────────────────────────────────────────────┘

任务行（高 72px，列表项）
┌────────────────────────────────────────────────────────────┐
│ ⏳  识别 video_001.mp4                            [取消] [⋯] │  ← 单行 hover 时显示动作
│     ████████████░░░░░░░ 64%   ·  已用 12s / 预计还 7s     │  ← 进度条 + 时间
└────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────┐
│ ✅  识别 video_002.mp4                          [详情] [⋯] │
│     · DLsite 命中 RJ01081508  ·  耗时 8s                   │
└────────────────────────────────────────────────────────────┘
┌────────────────────────────────────────────────────────────┐
│ ❌  识别 video_003.mp4                       [重试] [详情]  │  ← 失败行左侧 accent 色条
│     · 全部网站未命中  ·  3 次尝试失败                      │
└────────────────────────────────────────────────────────────┘

批量选中态（≥ 2 行选中时底部浮现）：
                                 ┌──────────────────────────────┐
                                 │ 已选 5 项   [全部取消] [清除] │  ← Bottom ActionBar 56px
                                 └──────────────────────────────┘
```

### 关键决策

- **不用 DataGrid，用 ItemsRepeater + 自定义行模板**——任务行的进度条 + 多状态切换太定制，DataGrid 列模式束手束脚
- **状态色**：运行中 = `SystemFillColorAttention`（蓝），完成 = `SystemFillColorSuccess`（绿），失败 = `SystemFillColorCritical`（红），排队 = `TextFillColorTertiary`（灰）
- **进度条** 用 `ProgressBar` 显示百分比 + 旁边文本"已用 X / 预计还 Y"——比单纯百分比更有信息量
- **末端动作** 默认隐藏，hover 行时滑入；防止视觉噪音
- **失败行**：左侧 4px 红色 accent 条 + 失败原因（`TaskExecutionInfo.LastError` 脱敏后短描述）
- **任务详情对话框**：`ContentDialog`，4 个 Tab：概览 / 识别诊断 / 执行历史 / 原始 JSON

### 验收

1. SourcesPage 触发批量识别 → 本页面实时出现新行 + 进度平滑增长
2. 取消按钮 → 任务真停止（`UnifiedTaskService.CancelTask`）
3. 关窗 → 重开 → 进行中任务**状态恢复**（依赖任务 6 Hangfire 持久化）
4. 多选 5 行 → 底部浮现 ActionBar + 全部取消生效
5. 点击任务行 → 详情对话框；识别诊断 Tab 显示尝试过的网站序列（Phase 2 完工后）

---

## 5. 待处理媒体源（双 Tab）

对应 Web 的 [`Pages/Sources/UnknownPage.razor`](../../NineKgTools.Web/Pages/Sources/UnknownPage.razor)。

### 任务

- [ ] **VM**：`ViewModels/Pages/PendingMediaViewModel.cs`，依赖 `SourceService` + `PendingIdentificationService`
- [ ] **双 Tab 容器**：FluentAvalonia `TabView`，标签上显示计数 badge：「待识别 (12)」「待入库 (5)」
- [ ] **列表行设计**：文件图标 + 名称 + 路径（小字 opacity 0.7）+ 大小 / 修改时间 + 末端动作组
- [ ] **预览对话框**：`Views/Dialogs/PendingPreviewDialog.axaml`，从 `PendingIdentification.MediaBaseJson` 反序列化渲染只读详情
- [ ] **手动添加流程**：复用任务 7 的 `ManualAddMediaDialog`
- [ ] **批量操作**：复用任务 4 同款"行选中 + 底部 ActionBar"模式

### 设计稿

```
Tab 容器
┌──────────────────────────────────────────────────────┐
│ ⚠ 待识别 (12)  │  ✓ 待入库 (5)                        │  ← Tab 头，计数 badge
├──┴──────────────┴─────────────────────────────────────┤

待识别 Tab 行示例：
┌────────────────────────────────────────────────────────┐
│ 🎬  movie_unknown_03.mkv                                │
│     C:\Media\Inbox\Unsorted\movie_…  · 2.1 GB · 5/3    │
│                              [识别] [手动添加] [丢弃] │
└────────────────────────────────────────────────────────┘

待入库 Tab 行示例（已识别但未入库）：
┌────────────────────────────────────────────────────────┐
│ 🎬  movie_unknown_03.mkv → 《XXXX 第二季 第 5 集》     │
│     ↑ DLsite 匹配  ★ 4.5  · 已识别 5 分钟前           │
│                       [预览] [入库] [重新识别] [丢弃] │
└────────────────────────────────────────────────────────┘
```

### 关键决策

- **不显示完整路径**——路径只显示中间省略的精简版（开头 8 字 + … + 末尾 16 字），鼠标悬停显示完整 tooltip
- **待入库行的"识别结果"** 直接展示，不需要点击预览才看——降低用户决策成本
- **预览对话框** 用 `ContentDialog` 而不是开新窗——这是"决定要不要入库"的临时审视，不应该消耗一个窗口
- **批量入库** 走 `NineKgConfirmDialog Affirmative intent`（见任务 7）
- **批量丢弃** 走 `NineKgConfirmDialog Destructive intent`，目标名预览卡列前 5 个文件 + "等共 N 项"

### 验收

1. SourcesPage 加监视文件夹塞 5 个未识别文件 → 待识别 Tab 出现 5 行
2. 关掉自动入库后跑识别 → 待入库 Tab 出现识别成功条目
3. 批量入库 → 主窗媒体库出现新条目，对应 PendingIdentification 记录被清理
4. 重启应用 → 待入库 Tab 状态保留（`PendingIdentificationCleanupTask` 没误删未过期记录）

---

## 6. Hangfire 切换到 SQLite 持久化

Phase 0 用 `Hangfire.MemoryStorage`，桌面端关窗即丢任务。MVP 必须修。**纯技术任务，无 UI 设计**。

### 任务

- [ ] **包**：`NineKgTools.Desktop.csproj` 加 `<PackageReference Include="Hangfire.SQLite" Version="..." />`
- [ ] **存储路径**：`config.Database.HangfirePath`（已有字段，默认 `Database/hangfire.db`）
- [ ] **`Program.ConfigureHangfire`**：`UseMemoryStorage()` 换成 `UseSQLiteStorage(config.Database.GetHangfireConnectionString())`
- [ ] **首次启动建库**：Hangfire.SQLite 首次连接会自建 schema，无需额外迁移
- [ ] **关窗清理**：确认 BackgroundJobServer 也优雅退出（`JobStorage.Dispose` 由 `IServiceProvider.Dispose` 触发，已经在 `AppBootstrap.ShutdownCleanup` 后续）

### 验收

1. 启动桌面端，触发长任务（100 个文件批量识别）
2. 跑到一半 X 关窗
3. 重开 → BackgroundTasksPage 列表里这个任务**继续跑**且进度从中断点恢复
4. `dataDir/Database/hangfire.db` 文件存在且 size > 0

---

## 7. 共享对话框体系（基础设施）

Web 端 `NineKgConfirmDialog` / `MediaKindPickerDialog` / `ManualAddMediaDialog` 全部需要 Avalonia 重写——所有页面共用，必须做。

### 任务

- [ ] **`Views/Dialogs/NineKgConfirmDialog.axaml`** 对应 [`Components/Common/NineKgConfirmDialog`](../../NineKgTools.Web/Components/Common/)
  - Intent 四种：Info / Affirmative / Destructive / DestructiveBatch
  - Hero 顶部 4px Intent accent 色条
  - Destructive 类含"此操作不可撤销"警告行
  - DestructiveBatch 含 Hero 大号 count
  - 静态调用：`await NineKgConfirmDialog.ShowAsync(ownerWindow, title, message, intent: ..., targetName: ..., affectedCount: ...)` 返回 `bool`
- [ ] **`Views/Dialogs/MediaKindPickerDialog.axaml`** —— 文件夹 / 单文件双卡片选择
- [ ] **`Views/Dialogs/ManualAddMediaDialog.axaml`** —— Hero + 必填 Title + TopCategory + 可展开手风琴；返回 `ManualAddMediaResult(MediaId, FullyFilled)`
- [ ] **`Services/ManualAddMediaHelper.cs`** —— 复刻 Web `OpenByPathAsync` 逻辑

### 设计稿（NineKgConfirmDialog）

```
┌────────────────────────────────────────────────┐
│ █████████ accent 色条 4px █████████████████████│  ← Intent accent (蓝/绿/红)
├────────────────────────────────────────────────┤
│                                                │
│   ⚠  确认删除？        (24px Title)            │
│                                                │
│   你将永久删除媒体「视频名称_xxxxxxxxxx」      │
│   及其全部关联数据（标签 / 评分 / 收藏夹）。   │
│                                                │
│  ┌─[Destructive 专属]─────────────────────┐    │
│  │ ⚠ 此操作不可撤销。                     │    │
│  └────────────────────────────────────────┘    │
│                                                │
│   [DestructiveBatch 专属]                      │
│   ┌─Hero count card─────────────────────┐     │
│   │           23                        │     │
│   │       将被永久删除                  │     │
│   │  · 视频名 1                         │     │
│   │  · 视频名 2                         │     │
│   │  · 视频名 3                         │     │
│   │  · 等共 23 项                       │     │
│   └─────────────────────────────────────┘     │
│                                                │
│                          [取消]  [确认删除]    │  ← Destructive: 主按钮红
└────────────────────────────────────────────────┘
```

### 关键决策

- **基于 FluentAvalonia `ContentDialog`** 不自己造轮子；定制只在 ContentTemplate 里
- **Intent → 主按钮色 + 默认图标** 映射：
  - Info（蓝）：ℹ 默认按钮"确认"
  - Affirmative（绿）：✓ "执行"
  - Destructive（红）：⚠ "确认删除/丢弃"
  - DestructiveBatch（红，Hero count）：⚠ "全部删除/丢弃"
- **错误日志** 由调用方负责：所有 Destructive 类必须用 Serilog 结构化字段记录 `ex`，用户侧 Snackbar 固定文案"操作失败，请稍后重试。"，**禁止拼 `ex.Message`**（反模式 #3）

### 验收

1. 删除媒体走 Destructive intent，弹窗显示红色 accent + "此操作不可撤销"警告条
2. 批量删除走 DestructiveBatch + Hero count 显示"23"
3. 手动添加流程走 `ManualAddMediaHelper.OpenByPathAsync` 三种入口都通

---

## 8. 视觉 token 文档与资源字典（轻量版）

由于纯 Win11 Fluent 决策，Token 工作量比初版预估**小很多**——主要是给 FluentAvalonia 默认 brush 加几个项目自定义 key，**不**重写一套色卡。

### 任务

- [ ] **新建** `docs/development/desktop-design-tokens.md`，包含：
  - **不覆盖** FluentAvalonia 任何 brush——只列出 Phase 1 用到的 brush key 清单作参考（让团队知道"主操作色用 `AccentFillColorDefaultBrush`"，不用每次去查）
  - **项目自定义部分**（仅 4 项）：
    1. `BrandHeroGradient`（媒体详情 Hero 用）—— 浅 / 深主题各一份 LinearGradientBrush
    2. `BrandLargeRadius=12`（对话框 / Hero 圆角，区别于 FluentAvalonia 的 `OverlayCornerRadius=8`）
    3. `BrandActionBarHeight=56`（任务/待处理页底部浮现的批量 ActionBar 标准高度）
    4. `BrandHeroHeight=380`（媒体详情 Hero 区高度）
  - **间距 / 字号 scale 对照表** —— 不发明，直接列 FluentAvalonia 已有 `TextStyle` / `Thickness` 资源
- [ ] **`Themes/BrandResources.axaml`** —— 仅 4 个项目自定义 key 落到 ResourceDictionary
- [ ] **不做** 与 Web `wwwroot/css/` 的对照表 —— 设计基线已经声明"两端视觉不强求一致"

### 验收

1. Phase 1 全部页面只引用 token，不出现魔数（hex 颜色 / 像素硬编码）
2. 切主题（Light / Dark / Default）所有页面颜色都跟着变（FluentAvaloniaTheme 自动管，token 不写死即可）
3. Hero 渐变在浅/深两种主题下都视觉舒服（不会刺眼或对比度过低）

---

## 9. 图片 LRU 缓存（性能基础设施）

媒体库网格切页 / 滚回旧位置时，封面图必须秒显——不能每次都从 SQLite 拉 BLOB 解码。

### 任务

- [ ] **`Services/ImageCacheService.cs`**：内存级 LRU，capacity 200 张 Bitmap（约 50–100 MB 内存）
- [ ] **接口**：`Task<Bitmap?> GetOrLoadAsync(int imageId)`，未命中走 `ImageService.GetImageContentAsync`
- [ ] **Avalonia Converter**：`BitmapAsyncConverter` 给 AXAML `Image.Source` 用，自动从 `imageId` → `Bitmap`
- [ ] **失效策略**：编辑媒体保存后用 `WeakReferenceMessenger` 广播 `ImageInvalidatedMessage(imageId)`，Cache 收到后驱逐对应条目
- [ ] **disk cache** 暂不做 —— Phase 1 验证内存 LRU 是否够用，不够再加（开 docs/todo 新条目）

### 验收

1. 媒体库滚动 1000 条 → 内存增长 ≤ 100 MB（命中率 ≥ 80%）
2. 编辑封面后保存 → 主窗对应卡片在 1s 内更新成新封面（不需要刷整页）

---

## 未解决问题（Phase 1 期间消化）

- **多窗口的 DI scope 释放**：每个 detail 窗的 Dispose 路径必须验证不泄漏 DbContext / 订阅 messenger 不解绑
- **Avalonia 的 InfoBar 控件** 是否在 FluentAvalonia 2.2.0 提供？如果没有需要自己实现一个简版（纯色背景 + 图标 + 文字 + 关闭按钮）
- **Phase 0 留下的 LocalAppData 数据**：迁移期间用户可能装多个版本，Settings 加"清空缓存"和"重置应用"按钮把 dataDir 整理工具放到 Phase 2

## 下一步

进入 [Phase 2](desktop-phase-2.md)：识别诊断 / 网站配置 / 设置 / 标签 / 创作者。
