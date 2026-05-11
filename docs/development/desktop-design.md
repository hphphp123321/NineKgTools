# NineKgTools 桌面端 UI 设计指南

> 本文档面向 Avalonia 11 + FluentAvalonia 桌面端实现，与 Web 端的 [`frontend-design.md`](frontend-design.md) 平行存在。Web 用 Razor + MudBlazor + CSS，桌面端用 AXAML + FluentAvalonia + ResourceDictionary，两端**视觉语言独立**，但产品语义一致（共享 Core 业务层 + 数据模型）。

## 概述

桌面端采用 Avalonia 11 真原生渲染（Skia），目标 Windows / macOS / Linux 三平台。**视觉锚点是 Win11 Fluent 设计语言**——Mica 半透明窗口、系统 accent 色跟随、FluentAvalonia 控件库。差异化来自**项目自定义点缀**（5 类别色系统、Hero 渐变、Material Design 图标、Inter 字体）而非 wholesale 替换 Fluent。

## 设计理念

### 核心原则

1. **原生 Win11 优先** —— 99% 的颜色 / 字号 / 间距 / 控件外观走 FluentAvalonia 默认 brush，不发明新色卡。系统 accent 色变化时桌面端跟着变，是"原生应用"标志。
2. **品牌识别靠"点缀"而非"覆盖"** —— 4 个项目自定义 token：Hero 渐变（媒体详情顶部）、5 类别色（语义关联）、Inter 字体、MD 图标。其他全用系统资源。
3. **响应式 + 编辑安全** —— `AvaloniaUseCompiledBindingsByDefault=true`，编译期类型校验；运行时 ViewLocator 兜底报错（命名错也能看见警告）。
4. **分层差异**——主窗 / 子窗 / 对话框各自定位清晰：主窗是 hub，子窗是"独立深入"，对话框是"临时确认"。
5. **手感大于完美**——hover 抬起 / 平滑过渡 / cross-fade indicator > 一次性大改观。

### 视觉风格

- **氛围**：Win11 Fluent 半透明 + Mica + 系统 accent + 项目签名色点缀
- **字体**：Inter（包 `Avalonia.Fonts.Inter` 已引）替代 Segoe UI 默认，字符识别度更高
- **图标**：Material Design Icons（path data 内嵌为 StreamGeometry，无外部依赖），不用 Segoe Fluent Icons（识别度低且和系统设置 App 等同款）
- **动效**：180–280ms `CubicEaseOut`，Win11 默认时长，不卡顿不拖沓

## 项目结构与命名约定

### 目录结构（Phase 1 之后）

```
NineKgTools.Desktop/
├─ Program.cs                 # 入口 + DI 容器 + Hangfire + 数据目录
├─ App.axaml(.cs)             # Application 资源 + ViewLocator 注册
├─ ViewLocator.cs             # VM → View 命名约定映射
├─ app.manifest               # Win10/11 PerMonitorV2 DPI
├─ Themes/
│  └─ BrandResources.axaml    # 项目自定义 token（4 类）
├─ Services/
│  ├─ NavigationService.cs    # 主窗内页面切换
│  ├─ WindowManager.cs        # 非主窗（如媒体详情）管理
│  ├─ ImageCacheService.cs    # 图片 LRU 缓存
│  ├─ TopCategoryStyles.cs    # TopCategory enum → brush/icon 映射
│  └─ Messages/               # WeakReferenceMessenger 事件类型
├─ Converters/                # IValueConverter 实现
├─ ViewModels/
│  ├─ MainWindowViewModel.cs
│  ├─ PageViewModelBase.cs    # OnEnter/OnLeave 生命周期
│  ├─ Pages/                  # 各导航页 VM + 行 VM
│  └─ Dialogs/                # 对话框 ViewContext
├─ Views/
│  ├─ MainWindow.axaml(.cs)
│  ├─ Pages/                  # 各导航页 UserControl（命名约定 *Page）
│  ├─ Windows/                # 独立窗口（媒体详情等）
│  └─ Dialogs/                # 共享对话框 UserControl
```

### ViewLocator 命名约定

```
NineKgTools.Desktop.ViewModels.Pages.XxxViewModel
  →  NineKgTools.Desktop.Views.Pages.XxxPage
```

`ViewLocator.cs` 通过字符串替换实现，必须严格遵守命名：
- VM 类名 `*ViewModel` 后缀，View 类名 `*Page` 后缀（`Window` 类型用独立 Window 名而非 Page）
- 命名空间 `ViewModels` ↔ `Views`
- ViewLocator.Match 仅匹配 `PageViewModelBase` 子类——其他 VM（如 `MediaCardViewModel` 行 VM、对话框 context）不会触发

## 设计令牌（BrandResources.axaml）

99% 走 FluentAvalonia 默认 resource，仅 4 个项目自定义点：

### Hero 渐变（双主题）

```xml
<LinearGradientBrush x:Key="BrandHeroGradient">
    <!-- 浅色：浅蓝 → 浅紫 → 浅粉 -->
    <!-- 深色：深蓝 → 深紫 -->
</LinearGradientBrush>
```

**用途**：媒体详情窗顶部 Hero 区背景、首页标题区渐变背景。**禁止**用于普通卡片（会破坏 Mica 一致性）。

### 5 类别签名色

| 类别 | Brush key | 浅色 hex | 深色 hex | 应用场景 |
|---|---|---|---|---|
| 视频 | `BrandCategoryVideoBrush` | #5B6BC9 | #8B95D9 | 媒体库分类卡 / 媒体卡 fallback / 标签 chip 描边 |
| 音频 | `BrandCategoryAudioBrush` | #E89B5C | #F0B47A | 同上 |
| 游戏 | `BrandCategoryGameBrush` | #3DAA8B | #6CC5A8 | 同上 |
| 图片 | `BrandCategoryPictureBrush` | #E66B95 | #F095B0 | 同上 |
| 文本 | `BrandCategoryTextBrush` | #C4A76B | #D8C28F | 同上 |

每类还有带 alpha 的 `BrandCategory{Type}FillBrush`（用于卡片浅色背景）。**仅用于"语义关联媒体类型"的地方**，不替代 Win11 accent。

### 中性色 + 任务状态色

为统一"全部"按钮（媒体库 + 任务页过滤）和任务状态过滤 chip 的视觉，额外引入 5 个 brush（双主题）：

| Brush key | 用途 |
|---|---|
| `BrandCategoryAllBrush` / `BrandCategoryAllFillBrush` | 中性紫灰，"全部"分类与"全部"过滤（`#7B6F8E` 浅 / `#A89AC0` 深） |
| `BrandStatusRunningFillBrush` | 任务运行中 chip 选中浅蓝 fill |
| `BrandStatusSuccessFillBrush` | 任务已完成 chip 选中浅绿 fill |
| `BrandStatusFailedFillBrush` | 任务失败 chip 选中浅红 fill |

**视觉规则**：所有"切换 / 过滤"chip 选中态走 **"浅 fill 背景 + 同色（或语义同色）2px 边框 + 鲜艳 indicator 横条"** 三件套。这套规则在媒体库分类切换、任务页过滤 chip 中保持一致。

### 间距 / 圆角 / 字号

直接用 FluentAvalonia 标准 scale，**不发明新单位**：

- **间距**：4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 / 56 / 64（WinUI 8pt grid + 4pt 微调）
- **圆角**：`ControlCornerRadius`（4，按钮/输入）/ `OverlayCornerRadius`（8，卡片/Flyout）/ 自定义 `BrandLargeCornerRadius`（12，对话框/Hero）
- **字号**：12（Caption）/ 13（Body）/ 14（BodyStrong）/ 16（Subtitle）/ 20（Title）/ 28（TitleLarge）/ 40（Display）—— 通过 `Theme="{StaticResource ...TextBlockStyle}"` 引用，不写死像素

### 图标库（StreamGeometry 资源）

14 个 Material Design 图标（path data 内嵌）：

| 用途 | Resource Key |
|---|---|
| 侧栏 | `IconHome` / `IconLibrary` / `IconInbox` / `IconFolderOpen` / `IconTasks` / `IconTags` / `IconCreators` / `IconStar` / `IconWeb` |
| 类别 | `IconCategoryVideo` / `IconCategoryAudio` / `IconCategoryGame` / `IconCategoryPicture` / `IconCategoryText` |
| 通用动作 | `IconRefresh`（圆形带箭头）/ `IconInboxArrowDown`（待入库语义） |

**新增图标走这条路**：`https://materialdesignicons.com` 取 SVG path data → 写一个 `<StreamGeometry x:Key="IconXxx">{path data}</StreamGeometry>` → 在 AXAML `{StaticResource IconXxx}` 引用。Apache 2.0 许可，自由使用。

### 字体

```xml
<FontFamily x:Key="BrandFontFamily">avares://Avalonia.Fonts.Inter/Assets#Inter</FontFamily>

<Style Selector="Window">
    <Setter Property="FontFamily" Value="{StaticResource BrandFontFamily}" />
</Style>
```

Inter 替代 Segoe UI 默认，应用级生效。中文回退到系统中文字体（通常微软雅黑或 Microsoft YaHei UI）。

## 组件设计规范

### NavigationView 主窗框架

主窗用 FluentAvalonia `NavigationView`，`PaneDisplayMode="Auto"` 响应式：
- 窗宽 ≥ 1008 → `Left`（240px expanded）
- 640–1008 → `LeftCompact`（48px 图标 + 悬浮展开）
- < 640 → `LeftMinimal`（汉堡按钮）

`IsSettingsVisible="True"` 自动在 pane 底部加 Settings 入口。

**主内容区**用 `<ContentControl Content="{Binding CurrentPage}" />`，由 ViewLocator 自动渲染对应 View。**不要**在 NavigationView 内部用 `<ContentControl.ContentTemplate>` 套 DataTemplate，那样 ViewLocator 走不通。

### 媒体卡片（Phase 1.2）

- **portrait 比例**：固定 180×320，封面区 180×~270，元数据栏 ~50px
- **整卡可点击**：用透明 `Button` 包裹卡片 `Border`，`Padding="0" Background="Transparent" BorderThickness="0"`——继承 Button 的 hover/focus/keyboard tab 行为
- **封面 fallback**：`Cover` 为 null 时用类别 fill brush 背景 + 大号类别图标占位（48×48 半透明）
- **hover 反馈**：通过 ItemTemplate 内层 Border 的 hover 状态——尚未实现（候选：`translateY(-2px)` + accent 色边框）

### Tab 切换 + Indicator 系统（Phase 1.2 / 1.4）

媒体库分类切换 + 任务页过滤 chip 共享一套**底部 cross-fade indicator** 模式：

```xml
<Grid>
    <Button Classes="cat-tab" Classes.sel-video="{Binding IsCategoryVideo}" .../>
    <Border Classes="cat-indicator" Classes.active="{Binding IsCategoryVideo}"
            Background="{DynamicResource BrandCategoryVideoBrush}" />
</Grid>
```

Style 里：
- 默认：`Opacity="0"`、`RenderTransform="scaleX(0.2)"`
- `.active`：`Opacity="1"`、`RenderTransform="scaleX(1.0)"`
- `Transitions`：`DoubleTransition` (Opacity 220ms) + `TransformOperationsTransition` (RenderTransform 280ms)

切换时旧 indicator scaleX 收缩 + opacity 淡出，新 indicator scaleX 展开 + opacity 淡入——**视觉上像横条在按钮间"接力滑动"**，但实际是各自原地动。这是真"sliding indicator"的 80% 视觉效果但 0 行 C#。

**Indicator 颜色规则**：
- 媒体库分类切换：5 类别色各自的 `BrandCategory{Type}Brush`
- 任务页过滤 chip：状态语义色（`SystemFillColorAttention/Success/Critical/AccentFillColorDefault`）

### 共享对话框 NineKgConfirmDialog

所有"确认类"弹窗走 `Views/Dialogs/NineKgConfirmDialog.cs` 静态 API：

```csharp
var ok = await NineKgConfirmDialog.ShowAsync(this, title, message,
    intent: DialogIntent.Destructive,
    targetName: media.Title,
    confirmText: "确认删除");
```

**4 种 Intent + 视觉差异**：

| Intent | 图标 | 主按钮文案默认 | 是否警告条 | 是否 Hero count |
|---|---|---|---|---|
| `Info` | ⓘ 蓝 | "确认" | 否 | 否 |
| `Affirmative` | ✓ 绿 | "执行" | 否 | 否 |
| `Destructive` | ⚠ 红 | "确认删除" | 是 | 否 |
| `DestructiveBatch` | ⚠ 红 | "全部删除" | 是 | 是（Hero 大数字 + 影响列表） |

**实现要点**：
- 基于 FluentAvalonia `ContentDialog`，**不**自渲染整个 dialog frame——`ContentDialog.Title` slot 自渲染图标 + 标题（避免 Title=null 时占位空白）
- 内容区放共享 UserControl `NineKgConfirmDialog.axaml`：消息 + 目标名预览（Destructive） + Hero count 卡（Batch） + 警告条
- Destructive 类调用必须 Serilog 结构化记录 `ex`，用户侧固定文案"操作失败，请稍后重试。"——**禁止拼 `ex.Message`**

### 双列设置布局 + 即时保存（Phase 2.3）

设置类页面（如 Settings）走"左侧分组导航 + 右侧字段区"双列布局：

```xml
<Grid ColumnDefinitions="240,1,*">
    <Grid Grid.Column="0" Background="{DynamicResource LayerFillColorAltBrush}">
        <!-- 顶部分组列表 + 底部危险操作 -->
    </Grid>
    <Border Grid.Column="1" Background="{DynamicResource ControlStrokeColorDefaultBrush}" />
    <ScrollViewer Grid.Column="2" Padding="40,24,40,24">
        <Grid>  <!-- ScrollViewer 只能有一个 Content child -->
            <StackPanel IsVisible="{Binding IsGroupX}">...</StackPanel>
            ...
        </Grid>
    </ScrollViewer>
</Grid>
```

**关键约定**：

- **左侧 nav-item Style**：默认透明背景，`active` 类用 `BrandCategoryAllFillBrush` + `BrandCategoryAllBrush` 边框（与媒体库"全部"按钮同款，统一中性色调）
- **危险操作分隔**：左侧底部用 1px Border 分隔，独立放"打开数据目录 / 清空缓存 / 重置默认"等需要慎重的入口；"重置默认"等 Destructive 入口图标和文字用 `SystemFillColorCriticalBrush` 红色
- **字段表单 Style**：`StackPanel.field` 标准三件套——`TextBlock.label`（13px SemiBold）+ `TextBlock.help`（11px opacity 0.65 帮助文案）+ 实际控件
- **InfoBar 提示**：每个分组顶部或底部用 `<ui:InfoBar>` 提醒"此字段需重启 / 此操作不可撤销 / 数据库为何只读"等元信息——比塞进 help 文案更醒目

### 即时保存机制

不要"保存"按钮。字段失焦后即时保存，500ms 防抖：

```csharp
[ObservableProperty] private int _maxConcurrentIdentificationTasks;

partial void OnMaxConcurrentIdentificationTasksChanged(int value)
{
    if (_suppressSave || _config.Tasks is null) return;
    _config.Tasks.MaxConcurrentIdentificationTasks = Math.Max(1, value);
    DebouncedSave();
}

private void DebouncedSave()
{
    _saveDebounceCts?.Cancel();
    _saveDebounceCts = new CancellationTokenSource();
    var token = _saveDebounceCts.Token;
    _ = Task.Run(async () => {
        try {
            await Task.Delay(500, token);
            if (token.IsCancellationRequested) return;
            await Dispatcher.UIThread.InvokeAsync(async () => {
                await _config.SaveConfig();
                SaveStatusText = $"已保存 · {DateTime.Now:HH:mm:ss}";
            });
        } catch (TaskCanceledException) { /* 期望中：用户继续输入触发取消 */ }
    }, token);
}
```

**`_suppressSave` 标志**：构造函数 LoadFromConfig 时设 true 避免初次加载触发保存。

**SaveStatusText**：在左侧导航顶部紧贴"设置"标题下显示，给用户即时反馈"已保存 · 14:23:45"或"保存失败"。

### 任务行卡片（Phase 1.4）

```
┌────────────────────────────────────────────────────┐
│ ⏳   任务名称（粗）        子任务统计 (5/10 完成)     │  ← Row 0: 名称
│       ████████████░░░░ 64%                          │  ← Row 1: 进度（仅运行中）
│       运行中 · 已用 12s / 预计还 7s · current.mp4  │  ← Row 2: 状态时间
│       ┌─红色背景错误条─────────────────┐           │  ← Row 3: 错误（仅失败时）
│       │ DLsite 接口超时                │           │
│       └────────────────────────────────┘           │
└────────────────────────────────────────────────────┘
```

- 状态颜色映射：运行 = `SystemFillColorAttention`（蓝）/ 完成 = `SystemFillColorSuccess`（绿）/ 失败 = `SystemFillColorCritical`（红）/ 取消 = `TextFillColorTertiary`（灰）
- 进度条仅运行中显示（`IsVisible="{Binding ShowProgressBar}"`）
- 错误消息独占 Row 3，红色背景 + 红字
- 末端动作组（取消/重试）默认显示，未来可改为 hover 行才显示

## 页面设计规范

### 媒体库（MediaOverviewPage，Phase 1.2）

**结构**（4 行 Grid）：

```
Row 0 (Auto): 类别切换栏（6 列：全部 + 5 类别）
Row 1 (Auto): 筛选条（搜索 + 排序下拉 + 刷新按钮）
Row 2 (*):    主内容（卡片网格 / 加载骨架 / 空状态 / 错误状态 四态切换）
Row 3 (Auto): 分页栏
```

**关键决策**：
- 默认密度"适中"：每行 6–8 张卡，单卡 180×320——不做 Spacious/Compact 三档预设（YAGNI）
- "全部"按钮 + Toggle 语义：再点已选 = 取消（切回"全部"）
- 类别切换栏带 indicator 横条（见 Tab Indicator 规范）
- 三态切换（empty/loading/error）共用 Row 2 单元格，IsVisible 互斥

### 待处理（PendingMediaPage，Phase 1.5）

**结构**（双 Tab + 行卡片）：

- TabControl：「⓪ 待识别 (N)」「✓ 待入库 (N)」（计数 badge 嵌在 Tab Header 文字里）
- 行卡片：图标 + 文件名 + 路径预览 + 大小 + 末端动作组
- **路径预览**：长路径用"开头 20 字 + … + 末尾 36 字"省略，hover 显示完整 ToolTip

**操作映射**：
- 待识别 Tab 行：`识别 / 手动添加 / 丢弃`
- 待入库 Tab 行：`预览 / 入库 / 重新识别 / 丢弃`
- 删类 Action 走 `NineKgConfirmDialog Destructive intent`

### 后台任务（BackgroundTasksPage，Phase 1.4）

**结构**：

```
Row 0 (Auto): 头部（标题 + "清理已完成"按钮）
Row 1 (Auto): 状态过滤 chip group（4 chip 带 indicator）
Row 2 (*):    任务列表 / 空状态
```

**实时刷新机制**：
- `OnEnterAsync` 启动 `DispatcherTimer`（500ms 间隔）调 `Refresh()`
- `OnLeaveAsync` 停 timer
- **差量更新**：移除消失的项 + 更新现有项（调 `NotifyAll()` 触发派生 binding）+ 插入新项 + `Move` 重排——保证 ItemTemplate 不重建，UI 不闪

### 设置页（SettingsPage，Phase 2.3）

**结构**（左 240 + 1 + * 三列）：

```
┌──────────────┬─────────────────────────────────────┐
│  设置         │  外观 / 主题                          │
│  已保存 · ... │  ───────                             │
│              │                                      │
│  外观[active] │  主题                                 │
│  任务         │  跟随系统会随 Win11 浅深模式切换。    │
│  识别         │  ◉ 跟随系统  ○ 浅色  ○ 深色          │
│  数据库       │                                      │
│              │                                      │
│  ────────    │                                      │
│  打开数据目录 │                                      │
│  清空缓存     │                                      │
│  重置默认 (红)│                                      │
└──────────────┴─────────────────────────────────────┘
```

**实现要点**：

- **分组**（Phase 2.3 完整 10 组）：外观 / 任务 / 识别 / 媒体源 / 文件过滤 / AI / 标签匹配 / 搜索 / 日志 / 数据库
- **左侧导航**：`ScrollViewer` 包裹 nav 列表——10+ 项时窄屏会被截断，必须滚动
- **主题切换**：直接调 `Application.Current.RequestedThemeVariant = ThemeVariant.Light/Dark/Default`，本会话不持久化（持久化等桌面端 window-state.json 体系，未实现）
- **重置默认**：备份当前 `config.yaml` → `config.backup.<timestamp>.yaml` 后从 `config.example.yaml` 覆盖，再 `Config.InitConfig()` in-place 刷新；走 `NineKgConfirmDialog Destructive intent` 确认
- **数据库分组只读**：path 字段用 `<TextBlock FontFamily="Consolas">` 等宽字体显示，外加 `<ui:InfoBar Severity="Warning">` 解释"为何只读"
- **数值字段**：用 `<NumericUpDown Value="..." Minimum=".." Maximum=".." Increment="..">` 限制有效区间，避免错误输入炸 Hangfire/识别管线
- **重启字段提示**：分组顶部用 `<ui:InfoBar Severity="Warning">` 标注（任务 → MaxConcurrent；日志 → LogLevel）

**媒体源分组 — 监视文件夹列表编辑**：

- "+ 添加文件夹"调 `topLevel.StorageProvider.OpenFolderPickerAsync` 拿原生文件夹选择器；返回的 `IStorageFolder` 通过 `TryGetLocalPath()` 拿到本机路径
- 重复路径不重复加（`WatchFolders.Contains(path)` 守卫）
- 每行右侧 ✕ 按钮调 `RemoveWatchFolder(path)`，从 `ObservableCollection` 删除并立即 `DebouncedSave`
- list 操作不会触发 `WatchFolders` 整体 setter——所以**不能**用 `partial void OnWatchFoldersChanged` 拦截，必须显式调 `PersistWatchFolders()` 把 `ObservableCollection<string>` → `_config.Source.WatchFolders` 同步

**AI 分组 — 嵌套 OpenAI 字段 + 总开关 IsEnabled 联动**：

- 总开关 `AiUseAi` 关时整个 OpenAI 详细块 `IsEnabled="{Binding AiUseAi}"` 灰化（**不**用 IsVisible 隐藏，避免布局抖动）
- ApiKey 字段用 `PasswordChar="●"`（与 Bangumi ApiKey 同款）
- BaseDomain / ApiVersion 分两字段——`v1` 等后缀单独走，避免拼接错误（与 Web Settings 同款行为）

**枚举字段（日志级别）**：

- `LogLevelChoice` 是 `string` 而不是 `LogEventLevel` enum——避开 Avalonia 的 `CommandParameter` enum 坑（Phase 2 第一轮发现），ComboBox 绑 string，VM 内部 `Enum.TryParse` 转换
- `LogLevelOptions` 是 ObservableCollection<string> 硬编码 6 项

**未在此页暴露的字段**（直接编辑 `config.yaml`）：

- `files.ignored_files` / `files.ignored_patterns` / `files.allowed_extensions`（列表编辑成本高、默认值合理）
- `cache.path` / `cache.expiration_minutes`（启动前固定，运行时改无意义）
- `log.log_path` / `log.log_server` / `log.log_template`（部署相关，不在终端用户场景里）
- `log.log_types`（多选枚举，UI 复杂度过高）

每个未暴露字段在对应分组用 `<ui:InfoBar Severity="Informational">` 提示用户去 yaml 编辑——避免给用户错觉"全部字段都在这"。

### 标签管理（TagsPage，Phase 2.4 MVP）

**结构**（单页内 IsVisible 切换"顶级列表 / 顶级详情"两态）：

```
顶级列表态：
┌──────────────────────────────────────────────────┐
│  标签                                  [↻ 刷新]   │
│  按主题组织你的媒体 · 顶级 → 子标签 → 媒体        │
├──────────────────────────────────────────────────┤
│  ┌──────┬──────┬──────┬──────┐                   │
│  │ 🏷    │ 🏷    │ 🏷    │ 🏷    │  ← 220x140 卡片  │
│  │ 风格 │ 题材 │ 角色 │ 系列 │                    │
│  │ 12个 │ 23个 │ 16个 │ 45个 │                    │
│  └──────┴──────┴──────┴──────┘                   │
└──────────────────────────────────────────────────┘

顶级详情态（点卡片后）：
┌──────────────────────────────────────────────────┐
│  ← 返回    🏷  题材  (23 个标签)                  │
├──────────────────────────────────────────────────┤
│  [过滤标签名 / 描述...                       ]   │
│                                                  │
│  ┌─ 🏷  #校园    校园背景的故事       142 媒体  ─┐│
│  ├─ 🏷  #职场                          87 媒体  ─┤│
│  ├─ 🏷  #末世                          23 媒体  ─┤│
│  └──────────────────────────────────────────────┘│
└──────────────────────────────────────────────────┘
```

**实现要点**：

- **二态切换**：用 `SelectedTopTag` 是否为 null 控制 `ShowTopList` / `ShowTopDetail`，单页内 IsVisible 切换 — 不接 NavigationService 栈，避免回退按钮歧义
- **顶级卡片色系**：按 `TopTag.Id mod 5` 在 5 类别色之间循环（不语义关联媒体类型，仅做视觉差异化）；上半色块 + `IconTags` 大图标，下半文字
- **数据加载**：`GetCopiedTopTagsAsync` + `GetAllTagsAsync` 一次性加载，`GroupBy(TopTag.Id)` 出每顶级的子标签计数，O(2) 数据库查询而非 O(N)
- **进入详情**：调 `GetTagsByTopTagIdAsync(id)`（含 `Include(Medias)`）拿子标签 + 媒体计数；本地按"媒体数 desc → 名字 asc"排序
- **过滤**：`TextBox` 防抖即时过滤，搜 Name + Description（OrdinalIgnoreCase）
- **第三层（单标签详情媒体网格）**：留待接入 MediaShownView 时再开口子；MVP 不做

**编辑能力**（Phase 2.4 第二轮）：

- **新建顶级分组**：顶级列表头部"+ 新建分组"按钮 → `TopTagEditorDialog`（仅名称字段）
- **重命名 / 删除顶级**：卡片**右键 ContextMenu**（重命名 / 删除）；删除走 `NineKgConfirmDialog Destructive intent`，文案明示"将解除子标签与媒体的关联，但媒体本身不会被删除"
- **新建子标签**：详情页头部"+ 新建标签"按钮 → `TagEditorDialog`（名称 + 顶级下拉 + 描述）；默认顶级=当前进入的那个分组
- **编辑 / 删除子标签**：每行右侧 inline 图标按钮（✏ 编辑 / ✕ 删除，14px Unicode 字符，避免给项目加额外 PathIcon）
- **修改名称的影响提示**：`TagEditorDialog` 在"编辑模式 + 关联媒体数 > 0"时显示警示条"此标签关联了 N 条媒体，修改名称会同步影响这些媒体的标签展示"

**编辑对话框设计模式**（`TopTagEditorDialog` / `TagEditorDialog`）：

- 复用 `ContentDialog` + UserControl 内容 + 自渲染 Title slot 模式（与 `NineKgConfirmDialog` 一脉相承）
- Title slot：✏️（编辑） / ✚（新建）单字符 + Attention 色 + 标题文案
- **表单校验**：监听 `view.PropertyChanged` 事件实时刷 `dialog.IsPrimaryButtonEnabled`（名字非空 / 顶级已选）。**禁止用** `GetObservable().Subscribe(lambda)`——Avalonia 11 的 IObservable 不接受裸 lambda（编译报 CS1660），需要 IObserver wrapper。直接订阅 `PropertyChanged` 是最轻量做法
- **输入字段绑定**：用 `StyledProperty<T>` + `{Binding #DialogRoot.XxxValue, Mode=TwoWay}`，避免给小弹窗多写一个 ViewModel 类
- 返回值：`record Result(...)` 或 `string?`，**null = 取消**，非 null = 确认 + 校验通过
- **AutoCompleteBox 编译绑定坑**：`AutoCompleteBox.ValueMemberBinding="{Binding Name}"` 在 `AvaloniaUseCompiledBindingsByDefault=true` 下报 `AVLN2100` ——绑定上下文从控件本身走，没有 x:DataType。改用 `ComboBox + ItemTemplate` 即可，需要模糊搜索时再切换到 AutoCompleteBox 并显式标 `x:CompileBindings="False"` 在该控件上

### 创作者详情（CreatorsPage 详情态，Phase 2.5 第二轮）

**结构**（与列表态共用 CreatorsPage，单页内 IsVisible 切换）：

```
┌──────────────────────────────────────────────────────┐
│  [头像 96]  ← 返回列表                              │
│             创作者名（28px SemiBold）                │
│             声优 · 画师 · 演员                       │
│             别名: AAA、BBB、CCC                       │
│                              [⇨ 合并到...] [✕ 删除]  │
├──────────────────────────────────────────────────────┤
│  描述（如有，14px wrap，最大 900px）                  │
├──────────────────────────────────────────────────────┤
│  ┌────┬────┬────┬────┬────┐                          │
│  │封面│封面│封面│封面│封面│  ← 复用 MediaCardViewModel  │
│  └────┴────┴────┴────┴────┘                          │
└──────────────────────────────────────────────────────┘
```

**实现要点**：

- **Hero 区**（96x96 圆形大头像）+ 占位首字母（无头像时）+ 类型 / 别名 inline 文本
- **关联媒体**：调 `GetCreatorMediasAsync(id)`（含 Poster + Category 的 Include）→ 包成 `MediaCardViewModel` 网格；点卡片走 `OpenDetailCommand`（仍由 WindowManager 开新窗口，详情态不会被遮挡——因为 WindowManager 开的是 Window，不是 OverlayLayer）
- **空作品状态**：合并后剩下的孤立创作者会零关联媒体，显示提示卡引导用户直接删除

**合并对话框（CreatorMergeDialog）**：

- Title slot：⚠ + Critical 色 + "合并创作者"
- 来源描述卡（左竖块）：来源名 + 作品数 + 别名数
- **目标 ComboBox**：候选 = 全部 - 来源；ComboBox 有 320px MaxDropDownHeight 折中"全显示 vs 滚动"
- **影响预览**（选中目标后动态显示）：
  - "{N} 件作品将迁移到「{target}」（合并后该创作者共 ≤ {N+M} 件，重叠去重）"
  - "创作者「{source}」会被删除"
  - 红色"此操作不可撤销"warning
- **核心实现**：union `sourceMedias.Ids ∪ targetMedias.Ids` → `UpdateCreatorMediasAsync(targetId, union)` → `DeleteCreatorAsync(sourceId)`。`UpdateCreatorMediasAsync` 是 absolute 设置（既加新关联也移除不在列表里的旧关联），所以一定要先 union，不能只传 source 那一份

**视觉细节**：
- 操作按钮排版：合并 ⇨（unicode "rightwards arrow"）+ 删除 ✕，Critical 色仅给删除按钮文字
- 头像 96x96 比列表卡 80x80 大一档，建立详情页"主角焦点"

### 交互式识别流程（IdentificationFlowService · A→B→C 三步链，Phase 2.0）

**结构**（在媒体详情窗"重新识别"按钮 / 待识别 Tab"识别"按钮 / 待入库 Tab"重新识别"按钮触发，依次弹三个对话框）：

```
A. 识别选项                       B. 进度 + 诊断                C. 结果预览
┌──────────────────────────┐    ┌──────────────────────┐    ┌──────────────────────┐
│ 🔍 重新识别选项           │    │ 🔎 正在识别媒体      │    │ 👁 预览待入库识别结果 │
├──────────────────────────┤    ├──────────────────────┤    ├──────────────────────┤
│ 路径：D:\…\file.wav      │    │ ▓▓▓▓▓░░░░ 52.3%      │    │ 标题  |  路径        │
│                          │    │ 正在查询 DLsite...   │    │                       │
│ ▸ 基础选项（展开）       │    │ D:\…\file.wav        │    │ chip chip chip       │
│   网站  | 网站ID         │    │                      │    │                       │
│   策略  | 类型           │    │ ┌─ 关键词解析 ────┐ │    │ 简介…                 │
│ ▸ 多网站ID映射 (2)       │    │ │ 代码 [RJ123456] │ │    │                       │
│ ▸ 高级选项               │    │ │ 关键词「标题」  │ │    │                       │
│ ▸ 网站优先级             │    │ └─────────────────┘ │    │                       │
│                          │    │ ┌─ 网站尝试 ─────┐  │    │                       │
│ [重置] [取消] [开始识别] │    │ │ ✓命中  DLsite  │  │    │                       │
└──────────────────────────┘    │ │   标题 1234ms  │  │    │                       │
                                │ │ ⊘跳过  Bangumi │  │    │                       │
                                │ └────────────────┘  │    │ [关闭] [确认入库]    │
                                │      [✕ 取消识别]   │    └──────────────────────┘
                                └──────────────────────┘
```

**实现要点**：

- **入口分流**（`IdentificationFlowKind`）：
  - `Reidentify`（详情页 / 待入库 Tab）：标题"重新识别选项"，初始 `SkipCache=true`
  - `FirstTime`（待识别 Tab）：标题"手动识别选项"，初始 `SkipCache=false`
  - 两种都强制 `AutoAddToDatabase=false`——结果走 C 让用户确认入库
- **A · 选项对话框**（`IdentificationOptionsDialog`）：
  - 4 个 `Expander` 默认只展开第 1 个（基础选项）；其他 3 个收起减视觉噪音
  - **避坑**：策略 / 类型用 ComboBox + ItemTemplate（DataTemplate 绑 enum-record `StrategyOption`），不用 RadioGroup（Avalonia 12 enum CommandParameter 陷阱见反模式章节）
  - **网站优先级**：用 `ToggleButton` WrapPanel 模拟多选；选中先后顺序通过 VM 内 `SelectionOrder` 字段维护，UI 顶部用文本 `DLsite → Bangumi → Steam` 形式实时回显
  - **验证失败**：Strategy=Manual 但未填 ID → 底部红色 InfoBar 提示；`PrimaryButtonClick.Cancel=true` 留住 dialog 让用户改
  - **重置按钮**：`SecondaryButtonClick.Cancel=true` 截获，跑 `ResetCommand` 但不关 dialog
- **B · 进度+诊断对话框**（`IdentificationProgressDialog`）：
  - **进度条**：FluentAvalonia 默认 `ProgressBar`，accent 色；右上百分比大号字加 SystemFillColorAttention 着色
  - **关键词解析**：诊断对象首次填入 `Keywords` 时显示——产品代码 / 社团 / 主关键词三组 chip
  - **网站尝试列表**：每行 `ItemTemplate` 含状态徽章（图标 + 文本 + brushKey 着色，复用 IdentificationDiagnostics 系统语义色）+ 网站名 + Top N 标记 + 命中标题 / 失败原因 + 右侧 ms 耗时（等宽字体）
  - **取消按钮**：红框 outline + `SystemFillColorCriticalBrush` 着色，不用 Critical class（桌面端目前未定义此 class）；点取消触发 `CancellationTokenSource.Cancel()` + UI 切"正在取消..."
  - **节流**：VM 内 100ms `DispatcherTimer`，Reporter `OnProgress` handler 只 `_pendingEntry = entry` 覆盖式写入；Tick 在 UI 线程统一应用 + 增量同步 `Diagnostics.WebsiteAttempts` 到 `ObservableCollection<WebsiteAttemptItemVm>`（最后两条 `Refresh` 而非新增——活跃 attempt 状态会变更）
  - **不可关闭**：FAContentDialog 三组按钮全 Hide（`DefaultButton=None`），唯一关闭路径 = 内嵌"取消识别"按钮 / `dialog.Hide()`（由 flow service 在识别完成时调）
- **C · 结果预览**：完全复用 `PendingMediaPreviewDialog`，用户点"确认入库"→ `FilesService.AddMediaToDatabase`，取消则丢弃（与 Web 一致）
- **共享流程编排**（`IdentificationFlowService.RunInteractiveAsync`）：
  1. A 对话框 await（阻塞）
  2. new `IdentificationDiagnostics` + `DialogProgressReporter`
  3. **必须** `using (IdentificationDiagnosticsContext.BeginScope(diagnostics))`——AsyncLocal 作用域是网站层 `RecordKeywords` / `RecordCandidates` / `MarkChosen` 上报的唯一入口
  4. B 对话框 fire-and-forget show + await `FilesService.GetMediaByPath(...)`
  5. 完成后 `await handle.CloseAsync()` 关 B
  6. 成功且 media != null → 弹 C 预览；用户确认 → AddMediaToDatabase
- **返回结果**（`IdentificationFlowResult`）：Canceled / NoMatch / Failed / UserDeclined / Imported；调用方仅在 `Imported` 时 RefreshAsync 刷新列表

**视觉细节**：
- A 对话框 `PlaceholderText="例如：RJ123456 / 22905 / 730"`——直接给出三个识别源的真实 ID 范例，比抽象 "网站特定 ID" 文案易懂得多
- B 对话框关键词 chip：产品代码用 `SystemFillColorAttentionBackgroundBrush`（蓝调）强调，主关键词用 `SubtleFillColorTertiaryBrush`（中性灰）次要
- B 对话框 attempt 状态徽章 brushKey 映射：Success / CacheHit → success/attention；NoMatch / Skipped → neutral；Exception → critical。设计意图：用户一眼能看出"哪个站点失败了"
- B 对话框完成后**不立刻关**——`Finalise()` 停 timer 但保留终态（红框取消按钮隐藏 + 显示最终诊断），flow service 主动调 `CloseAsync()` 后才关闭

**修改这部分时**：增删 `IdentificationOptions` 属性 → 同步 `IdentificationOptionsDialogContext.RestoreFromOptions/BuildOptions` + axaml 三个面板。`IdentificationStrategy` 枚举值变化 → 更新 `StrategyOption` 名称/描述映射 + 帮助文案。

### 识别诊断面板（IdentificationDiagnosticsView，Phase 2.1）

**结构**（独立 Window，从 BackgroundTasksPage 行的"🔍 诊断"按钮触发）：

```
┌─────────────────────────────────────────────────────┐
│  识别诊断 · 任务名                            _ □ ✕ │  ← 系统标题栏
├─────────────────────────────────────────────────────┤
│  ┌─ Hero（accent 边框：成功绿 / 失败红）────────────┐│
│  │ ✓ 识别成功 · DLsite · "标题"  得分 0.92         ││
│  │   分类 Audio · 耗时 5.42s                        ││
│  │   D:\…\file.wav                                  ││
│  └────────────────────────────────────────────────┘ │
│                                                     │
│  ┌─ 关键词解析 ──────────────────────────────────┐ │
│  │ 产品代码  [RJ01081508]                          ││
│  │ 主关键词  视频名 完整版                          ││
│  │ 次要      关键字 · 备选                          ││
│  │ 清理后    视频名                                 ││
│  │ 检测语言  ja                                     ││
│  └────────────────────────────────────────────────┘ │
│                                                     │
│  网站尝试（共 3 次）                                 │
│  ┌─ #1 DLsite [命中] [搜索] 1234ms ────── (绿边框) │
│  │  ✓ 标题 完整版                                   ││
│  │  RJ01081508    得分 0.918                        ││
│  │  扫描 12 · 过滤 3 · 展示 Top 5                  ││
│  │  ┌── 0.918  ✓ 标题 完整版                       ││
│  │  │  RJ01081508  通过「视频名」                   ││
│  │  │── 0.741    标题 简短版                        ││
│  │  └── ...                                         ││
│  └────────────────────────────────────────────────┘ │
│  ┌─ #2 Bangumi [跳过] · 优先级低于已命中 DLsite     ││
│  └────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

**实现要点**：

- **入口**：`BackgroundTasksPage` 每行右侧动作组多一个"🔍 诊断"按钮，`IsVisible="{Binding HasDiagnostics}"` 仅识别类任务出现（非识别任务的 `Progress.IdentificationDiagnostics` 永远是 null）
- **数据源双轨**：先尝试 live `progress.IdentificationDiagnostics`（覆盖运行中 + 刚完成尚未 ClearCompleted 的任务）；回落到 `_taskService.GetExecutionHistory().FirstOrDefault(h => h.TaskId == ...).GetIdentificationDiagnostics()`（已 ClearCompleted 的历史）
- **独立窗口（不是 ContentDialog）**：`TaskDiagnosticsWindow` 继承 Window，`Show(MainWindow)` 设主窗为 Owner——让用户能边看诊断边操作其他任务（modal 体验太重）
- **VM 结构**：`IdentificationDiagnosticsViewModel` 包装 `IdentificationDiagnostics` 模型，预先 materialize 出 `WebsiteAttemptItemViewModel[]`（每条 attempt 包一层）和 `CandidateItemViewModel[]`（每条候选包一层）；避免在 AXAML 里写大量 enum 转换器或 inline 字符串拼接
- **状态色 / 图标映射**：
  - 命中 / 缓存命中 → Success 绿 + ✓
  - 未匹配 → Caution 黄
  - 跳过 → Tertiary 灰 + ↳
  - 异常 → Critical 红
  - final attempt 卡片 BorderThickness=2 + Success 绿边框（强视觉强调）
- **候选行 chosen 高亮**：`Border.candidate-row.chosen` 套 Success 边框 + Success 浅背景 + ✓ 前缀；非 chosen 的行用普通 Layer 浅底
- **Hero 路径中部省略**：`TruncateMid(80)` 留头留尾，移植 Web 的 `diag-hero__path` 行为，鼠标悬停看完整路径
- **空状态**：`Source is null` 时整个面板替换为单卡"本次任务没有识别诊断"+ 解释"只有 SingleSourceIdentificationTask 上下文里的任务才上报"
- **等宽字体**（Consolas）只用于 Score 列、ID 字段、产品代码——产生"数据感"且让数字列右对齐看齐

### 网站配置（WebsitesPage，Phase 2.2 MVP）

**结构**（顶部头部 + 单滚动列，每张网站一卡）：

```
┌────────────────────────────────────────────────────┐
│  识别网站                          已保存 · 14:32  │
│  配置 DLsite / Bangumi / Steam · 凭证立即生效       │
├────────────────────────────────────────────────────┤
│  ┌─ DLsite ──────────────────────────────────────┐ │
│  │ [DL]  DLsite             ● 启用       [启用◉] │ │
│  │       同人音声 · 无认证                        │ │
│  │   ──────────────────────────────              │ │
│  │   Selenium 抓评分 (需 Chromium)         [关◯] │ │
│  └────────────────────────────────────────────────┘ │
│                                                    │
│  ┌─ Bangumi ─────────────────────────────────────┐ │
│  │ [bgm]  Bangumi           ● 启用       [启用◉] │ │
│  │       番组计划 · 需 ApiKey                     │ │
│  │   ──────────────                                │ │
│  │   API Key:  [●●●●●●●●●●●●●●●●●●●●●]            │ │
│  │   已配置 · 末 4 位 …a3f9   [去申请 ApiKey →]  │ │
│  └────────────────────────────────────────────────┘ │
│                                                    │
│  ┌─ Steam ───────────────────────────────────────┐ │
│  │ [STM]  Steam             ○ 禁用       [禁用◯] │ │
│  │   ──────────────                                │ │
│  │   语言 [简体中文 ▾]   国家 [美国 (US) ▾]       │ │
│  │   ⚠ CN 区已禁用（部分游戏屏蔽 CN）              │ │
│  └────────────────────────────────────────────────┘ │
│                                                    │
│  ┌─ 识别优先级（即将到来）─────────────────────┐ │
│  │ ⓘ 拖拽优先级配置 — 当前先编辑 yaml 生效       │ │
│  └────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────┘
```

**实现要点**：

- **三张网站卡 + 一卡占位**：DLsite / Bangumi / Steam 各一张 site-card；顶部头像方块用各自类别色（图片色 / 视频色 / 游戏色），仅做视觉差异
- **状态点 Ellipse 8x8**：`Classes.on="{Binding XxxEnable}"` 绑成功色 / 灰色，比 chip 更轻量
- **ToggleSwitch** 显式"启用 / 禁用"标签，比 CheckBox 大且语义更清晰
- **ApiKey 输入**：`PasswordChar="●"` 隐藏字符；`BangumiApiKeyMasked` 计算属性"已配置 · 末 4 位 …xxxx"显式确认配置成功
- **去申请 ApiKey →** 按钮：`Process.Start(ProcessStartInfo { UseShellExecute = true })` 调用系统默认浏览器打开 `https://next.bgm.tv/demo/access-token`
- **CN 区屏蔽**：直接从 `SteamCountries` 列表里**剔除 cn**，配警示文案——比"显示但 disabled"清晰，少一个 ItemContainer disabled 实现的复杂度。即便用户改 yaml 强写 cn 进 config，VM `OnSteamCountryCodeChanged` 也会兜底重置为 us
- **依赖启用开关**：`IsEnabled="{Binding XxxEnable}"` 让禁用网站时整个详细配置块自动灰化
- **拖拽优先级未实现**：占位卡 InfoBar 明示"先编辑 config.yaml"，后续 session 接 Avalonia DragDrop API 时补——大块工作量不值得在 MVP 阶段挤进来
- **保存策略**：与 SettingsPage 同款 `DebouncedSave` 500ms，右上角 `SaveStatusText` 显示"已保存 · HH:mm:ss"

### 媒体详情独立窗口（MediaDetailWindow，Phase 1.3）

**结构**（独立 Window，不在 NavigationView 内）：

```
┌──────────────────────────────────────────────────┐
│ NineKgTools · 标题…                  📌 _ □ ✕   │  ← OS 系统标题栏 + Hero 浮动图钉
├──────────────────────────────────────────────────┤
│  Hero 区                                          │
│   [200x280 封面]   [🎬 视频] / 子分类  ✏修改      │  ← 类别色 chip（pill）
│                    标题 (28px)                    │
│                    🏢 社团（小 chip，单独一行）     │  ← 仅社团；其他创作者下放右栏
│                    ★ 4.5                         │
│                    ⭐渐变 pill: [待看] [喜爱] ...  │  ← hash(name) 派生彩色
│                    [编辑] [文件管理器] [重新识别]  │
├──────────────────────────────────────────────┬───┤
│  左侧：简介 / 图片画廊 / 别名 / 文件路径      │右│ 标签
│                                              │侧│ ─── 创作者 ───
│                                              │栏│ 🎬 导演 / 🎤 声优
│                                              │  │ ✏️ 编剧 / 🎨 原画
│                                              │  │ 🎵 音乐 / 👥 演员
│                                              │  │ 📖 作者 / 🏢 制作公司
│                                              │  │ 元数据
└──────────────────────────────────────────────┴───┘
```

**自定义标题栏（ExtendClientArea）**：
- `Window` 加 `ExtendClientAreaToDecorationsHint="True"` + `ExtendClientAreaTitleBarHeightHint="40"`（**Avalonia 12 已移除 `ExtendClientAreaChromeHints` 属性**——只设这两个就够，系统按钮自动渲染）
- 主 Grid 改 `RowDefinitions="40,*"`，Row 0 是自定义 title bar（左侧 logo icon + 标题，右侧图钉按钮，最右 150px 给系统按钮预留）
- 图钉走 `ToggleButton.IsChecked={Binding $parent[Window].Topmost, Mode=TwoWay}` 与 Window 顶置双向同步——比 Hero 区独立按钮更省空间，也接近 Win11 用户预期
- 透明 Background 让 Mica 透过来；空白区域默认可拖动窗口（OS handle，不需 PointerPressed）

**类别 chip**（顶部分类徽章）：
- `Border CornerRadius="999"` pill 形 + 1px accent 描边 + 类别 fill 背景（带 alpha）+ 类别 accent 前景文字/icon
- 5 种 TopCategory 各占一种颜色，与 `BrandResources.axaml` 里 `BrandCategory{Type}Brush` / `BrandCategory{Type}FillBrush` 资源一一对应——MediaCard / 分类切换栏 / 标签管理 也复用同一套 token，全局一致
- ViewModel 暴露 `CategoryBrush` (accent) + `CategoryFillBrush` (fill) + `CategoryIcon` (geometry) 三个 computed 属性；`TopCategory` 字段加 `[NotifyPropertyChangedFor]` 链，编辑时切分类自动重算 chip 颜色

**图片画廊（Pictures slider，取代原 Description）**：
- 主图区（深色 box + 8px 圆角，min 320 / max 480 高度）+ 缩略图条（64x64 + 6px spacing + 选中态 2px accent 描边）+ 翻页 ←/→ 按钮（38px 半透黑底圆形）
- `Pictures: ObservableCollection<MediaPictureItemViewModel>` + `SelectedPictureIndex: int` 双向同步
- `MediaPictureItemViewModel` 两种 ctor：已入库走 `ImageCacheService.GetOrLoadAsync(name)`；新加图走 `(image, byte[] inMemoryBytes)` 立即解码 in-memory Bitmap，`IsPendingNew=true`
- 编辑模式增删图片：
  - **添加**：图片 section 右上 [+ 添加图片] 按钮 + 无图时空状态卡引导
  - **删除**：每张缩略图右上角 18x18 半透黑底圆形 × 浮在右上 + 主图区右上 32x32 大 × 浮按钮——点击均触发 `RemovePictureCommand` 弹 `NineKgConfirmDialog Destructive` 确认
- `_editingPictures: List<Image>` draft：EnterEdit 拷贝 _media.Pictures 引用；AddPicture 命令读 byte[] + 新建 `Image(bytes, "picture_{Guid:N}{ext}")`（ctor 自动算 hash）+ append；RemovePictureAsync(item) 按引用从 draft 和 Pictures 同步移除（缩略图传当前 VM；主图区不传 → 兜底取 SelectedPicture）
- Save 时 `ApplyPictureDiffAsync` 计算增删：旧里有 / draft 里没 → `ImageService.RemoveImageAsync` 清 db + cache 文件；draft 里 Id==0 + 带 Content → `ImageService.AddOrFindImagesAsync(toAdd, mediaTitle)` 入库 + 落 cache；汇总后 `_media.Pictures.Clear()` + `Add(...)` 维护 EF tracker；最后 `MediaService.UpdateMediaAsync` 持久化关联

**收藏夹（独立编辑流 + 与分类 chip 同款三色 pill 视觉）**：
- 位置：Hero 区评分下方（不再是右栏次要信息）；情感优先级与"评分"同级
- 视觉：`FavoritePillViewModel` 暴露 3 个 `IBrush`（Background / BorderBrush / Foreground），由 `FavoriteGradientHelper.Get(name)` 按 hash(name) FNV-1a → hue 派生：
  - **浅色主题**：浅 fill `HSL(h, 50%, 92%)` + 实色 border `HSL(h, 55%, 55%)` + 深字 `HSL(h, 65%, 32%)`
  - **暗色主题**：深 fill（带 alpha）`HSL(h, 40%, 30%, 0.35)` + 中亮度 border `HSL(h, 55%, 55%)` + 浅字 `HSL(h, 55%, 78%)`
  - 同 (name, theme) 缓存——主题切换时一次性重派生
- pill 渲染：`Border CornerRadius=999 Padding=10,4 BorderThickness=1` + ⭐ icon 用 Foreground brush（不再白色），无渐变 / 无阴影 —— 与"分类 chip"完全统一视觉系
- 整 pill 即按钮——点击直接 `EditFavoritesCommand`（任何模式）；空状态 = 单个虚线 ➕ "加入收藏夹"
- 双模式分支：`IsEditMode==true` 时只改 `_editingFavorites` draft；`IsEditMode==false` 时弹 dialog → 直接 commit + `MediaService.UpdateMediaAsync`

**评分（5 颗整星，与 Web MudRating 同语义）**：
- 5 个 `Button > PathIcon` 横排，PathIcon.Data 绑 `RatingStar1..RatingStar5` computed Geometry（`(int)Math.Round(Rating) >= idx ? IconStarFilled : IconStarOutline`，从 BrandResources 取）
- `[NotifyPropertyChangedFor]` 链：`_rating` → 5 个 `RatingStarN`，rating 改时全部 5 颗自动重算
- `SetRatingCommand(string?)` Avalonia 12 enum CommandParameter 坑规避法——CommandParameter 用 "1".."5"，命令内 `int.TryParse`；点同一星 → toggle 清零（与 Web 一致）
- 编辑态 `IsEnabled={Binding IsEditMode}` 才可点击；浏览态 fill 仅显示
- 取代原 NumericUpDown 0.5 步进——精度损失（float → int round）但 UX 一致性远更值

**别名（Hero 标题正下方，不再在左栏底部）**：
- 浏览态：单行 `<Run Text="又名: " /><Run Text="{Binding AliasText}" />` 12px Opacity 0.65 + `TextTrimming=CharacterEllipsis MaxLines=1`，`HasAlias=false` 时整行隐藏
- 编辑态：复用现成 `<comp:EditableAliasList Aliases="{Binding EditingAliases}" IsEditable="True" />` chip 编辑器
- 标题→别名→社团→评分→收藏夹 五行垂直递减信息密度（标识 / 标识 / 元数据 / 元数据 / 用户归属）

**社团编辑（InputDialog 复用，不做完整 CircleSelectorDialog）**：
- 编辑模式社团 chip 旁出现 `[修改社团]` + `[✕]` 两个按钮
- 修改社团：`InputDialog.ShowAsync` 弹简单文本框（initial = current CircleName）→ `CreatorService.AddOrUpdateCircle({Name})` 入库（已存在同名复用 db 实体）→ `_media.Circle = result`
- 清除社团：直接 `_media.Circle = null`（同步 UI CircleName=null）
- Save 时 _media.Circle 随其他字段一起持久化
- 简化决策：用户场景以"为这媒体改个社团名"为主而非"选已有 N 个 Circle 之一"——InputDialog 比完整 selector 工作量小 10 倍且够用

**chip 跨页跳转（点 chip → 对应实体的详情页，与 Web 端 /tag/{id} /creator/{id} /circle/{id} 路由语义一致）**：
- 标签 / 创作者 / 社团 chip 整体即 `<Button>`（不是 Border），点击 → `NavigationService.NavigateToAsync<TagsViewModel/CreatorsViewModel/CirclesViewModel>` 切主窗到对应列表页并直达详情态
- 详情页复用各列表 VM 已有的 master-detail 结构（TagsViewModel 三层 / Creators 与 Circles 两层 ShowList vs ShowDetail）
- 三个列表 VM 都加 `_pendingDetailId: int?` 字段 + `RequestOpenDetail(int)` setter + `OnEnterAsync` 拦截逻辑：
  - configureBeforeEnter 阶段（同步 Action）调 `vm.RequestOpenDetail(id)` 仅写字段
  - OnEnterAsync 阶段（异步）读字段：有值 → `await OpenDetailByIdAsync(id)` 走详情加载；无值 → 走默认 list 加载
  - 这样避免 configureBeforeEnter Action 直接 `_ = OpenDetailByIdAsync(...)` fire-and-forget 与 OnEnterAsync 自动 LoadAsync 的 race condition
- 各 VM 的 `OpenDetailByIdAsync(int)` 公开入口：
  - **CreatorsViewModel**：直接调原私有 `LoadDetailByIdAsync` —— 内部 GetCreatorAsync + 头像 + GetCreatorMediasAsync
  - **CirclesViewModel**：抽出 `LoadDetailByIdAsync` 私有 helper（原 OpenCircleDetailAsync 内部逻辑提炼）+ 公开 wrap
  - **TagsViewModel**：fetch 全 Tag 找匹配 → 若有 TopTag 先 `SelectTopTagAsync`（让 GoBack 有正确层级栈）→ `LoadTagDetailAsync(new TagItemViewModel(tag))`
- ViewModel 加 3 跳转命令：`OpenTagAsync` / `OpenCreatorAsync` / `OpenCircleAsync` —— 通过 `_media.Tags / _media.Creators / _media.Circle` 拿 db 实体的 **Id**（同名实体精确区分），调 `NavigationService.NavigateToAsync<XxxViewModel>(vm => vm.RequestOpenDetail(id))`
- `MediaOverviewViewModel.ApplyXxxFilter` helper 仍保留（潜在的"按属性筛选媒体"入口）但 chip 跳转不再走它
- 嵌套 Button (chip + 内 × 删除) 在 Avalonia 12 不冒泡——`ButtonBase.OnPointerReleased` 在触发 OnClick 后 `e.Handled=true`；× 删除 / chip 整体跳转互不干扰
- 编辑模式下 × 仍优先（命中区域 14x14 在 chip 右侧）；浏览态点 chip 中央触发跳转

**chip 内联编辑（与图片画廊 ×/+ 同款交互）**：
- 每个 Tag / Creator chip 编辑模式右侧出 14x14 圆形 × 按钮（chip 内部 Grid 右列，不浮在外）
- × 点击 → `NineKgConfirmDialog Destructive(targetName=chipName)` → 用户确认后从 draft 删除（按 Name FindIndex 删第一项）
- section header 原"编辑"按钮改 `Padding=8,1 FontSize=14 FontWeight=Bold Content="+"` 显示"+"——纯添加语义；点击仍走原 `EditTagsCommand` / `EditCreatorsByRoleCommand` selector dialog
- ViewModel 加 1 + 7 个 Remove 命令：`RemoveTagAsync(string?)` + 7 个 `RemoveDirectorAsync` / `RemoveVoiceActorAsync` / `RemoveScreenWriterAsync` / `RemoveIllustratorAsync` / `RemoveActorAsync` / `RemoveMusicianAsync` / `RemoveAuthorAsync`，共享 `RemoveCreatorByRoleAsync(CreatorType, string)` helper
- 不支持取消（Cancel 编辑会通过 ApplyToProperties 从 _media 还原 → 撤销所有 chip × 删除）

**右栏元数据扩展为"文件信息 + 文件操作中心"**：
- 元数据 section 在原"发布日期 / 入库时间 / 文件大小"下方加 1px divider + "路径"行（small monospace + Wrap）+ 操作按钮组 `[📁 打开] [🔄 重新识别]`
- 操作按钮浏览态 + 编辑态都可点（不修改 _media，无需进编辑模式）
- Hero 主操作组砍掉 [文件管理器]/[重新识别]，浏览态只剩 `[✏ 编辑]`（视觉聚焦"修改"动作）
- 左栏底原"文件" section 删除（FilePath / FileSize 已合并到右栏元数据）

**编辑模式所有支持的职责 section 都显示（不只是有数据的）**：
- ViewModel 加 8 个 `Supports{Role}` 派生属性（基于 TopCategory 决定该类型支持哪些职责）
  - `SupportsDirectors = TopCategory == Video`
  - `SupportsVoiceActors = TopCategory in (Audio, Game)`
  - `SupportsScreenWriters = (Video, Audio, Game)` / `SupportsMusicians = 同上`
  - `SupportsIllustrators = (全部 5 类)` / `SupportsAuthors = (Audio, Game, Picture, Text)`
  - `SupportsActors = (Video, Picture)` / `SupportsMakers = Video`
- 8 个 `Show{Role}Section = Supports{Role} && (Has{Role} || IsEditMode)`（Makers 例外 —— 暂只读，仅 `&& Has{Role}`）
- AXAML 各 section IsVisible 从 `Has{Role}` → `Show{Role}Section`
- `TopCategory` 字段 `[NotifyPropertyChangedFor]` 8 个 Supports + 8 个 Show；`IsEditMode` 同样链；7 个 ObservableCollection 字段也加对应 Show notify —— 切分类 / 切编辑模式 / 集合替换都自动重算可见性

**创作者按职责分组（取代旧 `CreatorsText` 单字符串扁平化）**：
- 数据：8 个 `ObservableCollection<string>` per role（Directors / VoiceActors / ScreenWriters / Illustrators / Actors / Musicians / Authors / Makers）+ 8 个 `Has*` 派生 visibility flag + 1 个 `HasAnyCreators` 控制整组 header
- `ApplyCreatorsByType(MediaBase)` 按 `_media is VideoMedia / AudioMedia / GameMedia / PictureMedia / TextMedia` 分发数据到对应字段（其他职责清空避免 stale 残留）
- TopCategory 职责映射（与 Web `EditableCreatorList` 一致）：
  - Video: Directors / ScreenWriters / Illustrators / Actors / Musicians / **Makers (List<Circle>)**
  - Audio: VoiceActors / ScreenWriters / Illustrators / Musicians / Authors
  - Game: ScreenWriters / Illustrators / VoiceActors / Musicians / Authors
  - Picture: Illustrators / Actors / Authors
  - Text: Illustrators / Author（单 → Authors 列表至多 1 项）
- 每个职责一个右栏 section：13px Material icon + 13px label SemiBold + "编辑"按钮（IsEditMode 才显示）+ chip WrapPanel（复用现有 Tags chip 浅灰 pill 样式，让 icon/header 主导分组语义）
- 编辑命令统一走 `EditCreatorsByRoleAsync(string?)`：CommandParameter 用 enum **字符串名**（"Director"/"VoiceActor"/...）避免 Avalonia 12 enum CommandParameter 的渲染坑；内部 `Enum.TryParse<CreatorType>` → `ResolveDraftByType` 路由 8 个 draft + UI ObservableCollection setter。`CreatorSelectorDialog.ShowAsync(initialFilterType: type)` 让 dialog 默认筛选到该 type
- `_editingDirectors / _editingVoiceActors / ... / _editingAuthors` 8 个 per-role draft List；`InitCreatorDraftsByType` (EnterEdit) + `ApplyCreatorDraftsToMedia` (Save) 两个 helper 各做一遍 type switch；Save 后调 `media.SyncCreators()` 把分散字段聚合到 `MediaBase.Creators`（搜索/向量等下游用）
- **Makers（Video 类的 List<Circle>）暂只读**——Phase 2 加 `CircleSelectorDialog` 后再开放编辑

**Hero 区社团 chip（取代原 `CircleName · CreatorsText` 扁平字符串行）**：
- 位置：标题正下方，单独小 chip（左对齐）
- 视觉：`Border CornerRadius=999 Padding=10,4` + `SubtleFillColorTertiaryBrush` 背景 + 1px 描边 + `IconBusiness` 图标 + 名字
- `HasCircle` 派生（`!string.IsNullOrEmpty(CircleName)`）控制整 chip 显隐——无社团时不占空间

**元数据区（右侧栏）**：
- 只读模式：`label + value` 同行（`Grid ColumnDefinitions="Auto,12,*"`），三行分别为发布日期 / 入库时间 / 文件大小
- 编辑模式：发布日期单独拆成「label 一行 + DatePicker 占满下行」上下两行布局（`StackPanel Spacing="4"`），避免 DatePicker 在 320px 右栏里与 4 字 label 同行展示导致的 UI 重叠；入库时间 / 文件大小不可编辑保持原样
- `EditingReleaseDate: DateTimeOffset?` ↔ DatePicker.SelectedDate 双向；Save 时 `EditingReleaseDate?.DateTime` 转回 DateTime?

**多窗口管理**：`Services/WindowManager.cs` 维护 `Dictionary<string, Window>`，key 为 `media:{id}`。同一 media 重复点击 → `Activate()` 现有窗口，不重复开。主窗 `Closing` 触发 `WindowManager.CloseAll()`。

## 交互模式

### 平滑过渡（Transitions）

Win11 默认风格的过渡时长：

| 场景 | 时长 | Easing |
|---|---|---|
| 页面切换 | 160ms | NavigationView 默认 EntranceNavigationTransitionInfo |
| 类别切换 / chip 切换（brush） | 220ms | `CubicEaseOut` |
| Indicator scaleX | 280ms | `CubicEaseOut` |
| 卡片 hover lift | 80ms | 默认 |
| 对话框打开 | 240ms | ContentDialog 默认 |

### 三态强制（empty / loading / error）

每个数据驱动页面**必须**实现三态，**禁止**白屏 + spinner 了事：

- **空状态**：手绘图标 64–72px + 一句话原因 + 一个明确 CTA 按钮（如"去媒体源添加文件夹"）
- **加载**：骨架卡片占位（媒体库网格直接放 6–12 个骨架），不用居中 spinner
- **错误**：错误图标 + 脱敏消息（"加载失败，请稍后重试。"）+ "重试"按钮

### 键盘可达

- 所有交互控件 `IsTabStop="True"`，Tab 顺序：侧栏 → 命令栏 → 主内容 → 分页 → 状态栏
- 拔掉鼠标只用键盘必须能完整跑核心流程（媒体库浏览 → 详情 → 编辑保存）

## 反模式与高级坑（必读）

### 🔴 `CommandParameter` 字符串字面量 → enum 参数

**现象**：编译绑定下，`<Button Command="{Binding XxxCommand}" CommandParameter="Video" />` 如果对应 `[RelayCommand]` 方法接 `TopCategory` enum 参数，**整个 UserControl 静默渲染失败**——无任何运行时报错日志，全链路 binding 流程正常但 View 不显示。

**修法**：让 `[RelayCommand]` 接 `string?` 参数，方法内自己 `Enum.TryParse`：

```csharp
[RelayCommand]
private Task SelectCategoryAsync(string? categoryName)
{
    if (!Enum.TryParse<TopCategory>(categoryName, ignoreCase: true, out var cat))
        return Task.CompletedTask;
    ...
}
```

### 🔴 Avalonia Grid / UniformGrid 没有 ColumnSpacing/RowSpacing

WPF/WinUI 有，**Avalonia 11 没有**。用 gutter 列代替：

```xml
<!-- 5 列 + 4 个 12px gutter -->
<Grid ColumnDefinitions="*,12,*,12,*,12,*,12,*">
```

`StackPanel` 自带 `Spacing` 属性，能用就用。`WrapPanel` 也没 ItemSpacing/LineSpacing，需要在子项上加 Margin 模拟。

### 🔴 PathIcon 命名空间

`PathIcon` 是 Avalonia 自带（`Avalonia.Controls`），**不在 FluentAvalonia 命名空间**。直接用：

```xml
<PathIcon Data="{StaticResource IconHome}" />  <!-- ✓ 正确 -->
<ui:PathIcon Data="..." />                       <!-- ✗ 错误，会编译失败 -->
```

但 `PathIconSource` 是 FluentAvalonia 提供（用于 NavigationViewItem.IconSource）：

```xml
<ui:NavigationViewItem.IconSource>
    <ui:PathIconSource Data="{StaticResource IconHome}" />
</ui:NavigationViewItem.IconSource>
```

### 🔴 StaticResource vs DynamicResource

- **StaticResource**：编译期解析，找不到 key 在 AXAML 加载时**抛异常导致整个 View 渲染失败**
- **DynamicResource**：运行期解析，找不到 key 返回 null/默认值，不影响其他渲染

**经验法则**：Geometry / 自定义 Brush key（确定存在的 BrandResources）用 StaticResource；FluentAvalonia 系统 brush（可能因 theme/版本变化）用 DynamicResource。

### ⚠ DbContext 不是线程安全的

不要 `Task.Run(() => _mediaService.GetPagedMediaList(...))` 这样把 EF 调用扔到非 UI 线程——MediaService 持有的 DbContext 是 Scoped，多个 Task.Run 并发会冲突。改为 UI 线程同步调（分页 24 条 + AsNoTracking + Includes 在中等库 < 100ms，可接受）；万条级以上库再考虑切 `IDbContextFactory<MediaDbContext>` + Task.Run。

### 🔴 桌面端 Hangfire **必须用 MemoryStorage**——SQLite storage 在高并发 worker 下严重竞态

**症状演化（多轮排查的真实历史）**：

1. 第一轮发现：用户提交任务后日志卡在「任务已提交」无下文 → 修：手动驱动 IHostedService 启动 BackgroundJobServer
2. 第二轮发现：BackgroundJobServer 启动后大量「未找到任务元数据」+「Failed to process the job '71'」 → 怀疑历史孤儿，加 `CleanupOrphanHangfireJobs` 启动清理
3. 第三轮发现（**真因**）：清理后新提交的任务**仍重复执行**——单一父任务 jobId=247 被 `ExecuteParentTaskAsync` 调用 60 次，每次 `CreateChildTasksAsync` 都 enqueue 7 个子任务，雪崩 60×7=420 个孤儿子任务
4. 第四轮观察：worker return 后 IFetchedJob.RemoveFromQueue 失效，**同一 jobId 永远在 enqueued 状态被反复 fetch**（每秒 5–10 次）

**根因**：`Hangfire.SQLite` 1.4.2 在桌面端高 WorkerCount（`ProcessorCount * 2 = 64+`）下：

- `FetchNextJob` 的 SQLite SELECT...UPDATE 行锁不可靠 → 同 jobId 被多 worker 同时 fetch
- worker 完成后 `IFetchedJob.RemoveFromQueue` 没把 JobQueue 行真正删除 → 该 jobId 永远 visible，被反复 fetch 不进入终态
- ASP.NET Web 端用 `UseMemoryStorage` 没暴露此问题，迁移桌面时直接照搬 SQLite 是错误决策

**最终修法 = 桌面端用 `UseMemoryStorage`**（与 Web 端一致）：

```csharp
services.AddHangfire(c => c
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSerilogLogProvider()
    .UseMemoryStorage());  // 不用 UseSQLiteStorage
```

桌面端用户心智下"关进程=任务停止"——不需要跨重启续跑。MemoryStorage 与 `TaskMetadataStore.IMemoryCache` 生命周期天然对齐，关进程即丢任务，重启从干净状态开始，**零孤儿、零竞态**。

**保留的辅助防御**：

- `Program.CleanupOrphanHangfireJobs()` 仍调用——MemoryStorage 下应返回 `无 Hangfire 孤儿 job`，作为"切回 SQLite"时的 fail-safe
- `UnifiedTaskService` 的 `_runningTaskIds` `ConcurrentDictionary` 进程内互斥锁仍保留——任意持久化方案下都防御性兜底，同 taskId 进入 ExecuteTaskAsync / ExecuteParentTaskAsync 时 `TryAdd` 失败直接 return
- `[DisableConcurrentExecution(timeoutInSeconds: 60)]` 加在两个入口方法上——双重保护
- `ExecuteTaskAsync` 找不到 metadata 时**优雅 return 而非 throw**——避免 Hangfire 把孤儿 job 标记 Failed 后 invisibility timeout 反复 fetch

**牺牲**：

- `MaxConcurrentIdentificationTasks` 严格 N 并发限制不再保证——桌面单进程下识别受网络限速主导，无伤大雅
- 关进程时 in-flight 任务丢失——但 metadata 本就是 in-memory 同样会丢，行为一致

**判定指标**：

- 启动日志应有 `启动清理: 无 Hangfire 孤儿 job`
- `Hangfire.MemoryStorage.MemoryStorage` 出现在 storage 行
- 后续运行**不应该**有 `未找到任务元数据` 或 `跳过本次重复调度`（如出现 = `_runningTaskIds` 兜底触发，说明仍有竞态需查）

**长期重新评估**：如果桌面端真要"任务跨重启续跑"（场景：用户挂大批量识别任务再合上电脑），需要换稳定的持久化存储——`Hangfire.Storage.SQLite`（不同 fork）/ LiteDB / 自实现 IMemoryCache → JSON 文件。但成本高，当前 MemoryStorage 已覆盖 95% 场景。

### 🔴 System.Text.Json 反序列化要求构造参数对应 public property

**现象**：`DesktopPreferences 加载失败，回退默认值` 每次启动 Warning，附 `InvalidOperationException: Each parameter in the deserialization constructor must bind to an object property or field`。

**根因**：定义类时用主构造 `public DesktopPreferences(string filePath)` 把 `filePath` 存进 private field `_filePath`。System.Text.Json 反序列化时挑构造函数 → 看到唯一的有参构造 → 试图把 JSON 里的 `filePath` 字段映射给 ctor 参数 → 但类本身没有 `FilePath` public property → 报错。

**修法**：把"运行期注入"字段（路径 / 文件句柄 / 临时上下文）从构造参数移出，改用：
- 加无参 `public DesktopPreferences() { }` 给反序列化用
- field 标 `[JsonIgnore]` 不参与序列化
- `Load` 静态方法反序列化后**手动赋值**给 `_filePath`：

```csharp
public DesktopPreferences() { }  // JSON 用

public static DesktopPreferences Load(string dataDir)
{
    var filePath = Path.Combine(dataDir, "desktop-preferences.json");
    var loaded = ... JsonSerializer.Deserialize<DesktopPreferences>(json) ...;
    var result = loaded ?? new DesktopPreferences();
    result._filePath = filePath; // 反序列化后注入
    return result;
}
```

任何"非数据状态"的字段都该走这个模式——不要让序列化感知它们。

### 🔴 IHostedService 在 Avalonia 不会自动启动

**现象**：`services.AddHangfireServer(...)` 注册成功 + Hangfire SQLite 表都建好 + 任务能 enqueue 进 `hangfire.db`，但**永远不会被 worker 取出执行**——日志卡在"任务已提交"之后没下文。

**根因**：`AddHangfireServer` 内部注册的是 `BackgroundJobServerHostedService`，它是 `IHostedService`。ASP.NET 下 `WebApplication.Run()` 自动遍历所有 `IHostedService.StartAsync`；Avalonia 用普通 `IServiceProvider` 没有 IHost runner，**没人调 StartAsync**，BackgroundJobServer 永远不启动。

**修法**：手动驱动 IHostedService 生命周期。`Program.Main` 在 `BuildAvaloniaApp().StartWithClassicDesktopLifetime` 之前调：

```csharp
private static async Task StartHostedServicesAsync()
{
    var hosted = Services.GetServices<IHostedService>().ToList();
    foreach (var svc in hosted)
        await svc.StartAsync(CancellationToken.None);
}
```

退出时反向 `StopAsync`——Hangfire 进行中的 job 优雅 abort，重启后由 SQLite 存储的 ProcessingState 检测自动 retry。

**判定指标**：日志里有 `Server xxx successfully announced` + `all the dispatchers started`（Worker / DelayedJobScheduler / RecurringJobScheduler 等）才算真启动。

### ⚠ ViewLocator 类型解析

`Type.GetType(string)` 不带 assembly 限定符时只在调用栈所在 assembly 找——从 Avalonia 内部跨 assembly 调用时找不到我们的 View 类型。改用 `vmType.Assembly.GetType(viewName)`：

```csharp
var viewType = vmType.Assembly.GetType(viewName);
```

## 反模式自查（Phase 2.8）

Phase 2 收尾审计——对照 [`docs/development/frontend-design.md`](frontend-design.md) 的"组件开发反模式 Checklist"过一轮。Phase 2 完成快照如下：

| 检查项 | 状态 | 说明 |
|---|---|---|
| **错误消息脱敏** | ✅ | 用户侧文案统一 `"操作失败，请稍后重试。"` 类静态字符串；详细 `ex` 全部 `Log.Error(ex, ...)` 结构化记录。**禁止** `Snackbar.Add($"...{ex.Message}")` 风格 |
| **样式外部化（颜色 token）** | ✅ | AXAML 全用 `{DynamicResource XxxBrush}`；硬编码颜色仅出现在 `BrandResources.axaml`（设计 token 源）。**例外**：`ViewLocator.cs` 的"View not found / instance failed"开发占位用 `Brushes.OrangeRed`——dev-only fallback，生产不应触发 |
| **InfoBar Severity 语义** | ✅ | Informational = 提示 / 引导；Warning = 重启 / 只读约束；未滥用 Error / Critical |
| **HashSet 选中** | N/A | 当前 Phase 2 页面无多选场景；后续若加批量操作，多选状态务必用 `HashSet<int>` 而非 `List<int>`（`Contains` O(1) vs O(n)） |
| **多选 ContextMenu 陷阱** | N/A | TagsPage 的 ContextMenu 是 per-item 而非依赖选择状态——不会出现"右键时未选中任何项"的尴尬 |
| **键盘可达** | ⚠ 默认通过 | Avalonia 控件默认 `IsTabStop=true`；自定义 Style 没强制 `IsTabStop=false`。**未做完整 Tab 顺序回归**——后续如有键盘可达性诉求，需要逐页拔鼠标走一遍 |
| **触目标尺寸 ≥ 32×32** | ✅ | FluentAvalonia Button 默认 MinHeight 32；inline ✏/✕ 按钮用 `Padding="8,4"` 满足；CreatorsPage 的"← 返回列表"原本 `Padding="0"`（可能 24×17）已改为 `Padding="10,6"` 保证触目标 |
| **hover-only 信息** | ✅ | `ToolTip.Tip` 仅作"全文展开 / 操作释义"补充——主要信息（标题 / 路径 / 名称）始终可见；触屏笔记本 fallback 不影响功能 |
| **样式内联 `Style="..."`** | ✅ | 全 Selector 风格；未发现 inline Style 串字符串 |

**审计扫描方法（可复用）**：

```bash
# 1. ex.Message 泄漏到用户消息
grep -rn 'ex\.Message' NineKgTools.Desktop/ --include='*.cs' | grep -v 'Log\.'

# 2. 硬编码颜色（应只命中 BrandResources.axaml）
grep -rEn 'Color="#|Background="#|Foreground="#' NineKgTools.Desktop/ --include='*.axaml'

# 3. 命名色 keyword（Red/Green/Blue/...）—— 应只命中 ViewLocator dev fallback
grep -rEn 'Foreground="(Red|Green|Blue|Black|White|Gray|Yellow|Orange)"' NineKgTools.Desktop/ --include='*.axaml'

# 4. 内联 Style 串字符串
grep -rEn 'Style="(?!Static)[^"]{20,}"' NineKgTools.Desktop/Views/ --include='*.axaml'

# 5. 触目标过小的按钮（直接 Width/Height < 32）
grep -rEn '<Button[^>]*(Width|Height)="(1?[0-9]|2[0-9])"' NineKgTools.Desktop/Views/ --include='*.axaml'

# 6. List<int> 选中（应 HashSet<int>）
grep -rEn 'List<int>.*[Ss]elect' NineKgTools.Desktop/ --include='*.cs'
```

每次 Phase 收尾跑这 6 条 grep 一遍——零结果（除已知 dev exception）才 PASS。

## 多窗口体系（Phase 1.3）

桌面端的差异化体验之一：媒体详情独立窗口。

### WindowManager 模式

```csharp
public sealed class WindowManager
{
    private readonly Dictionary<string, Window> _openWindows = new();

    public void OpenMediaDetail(int mediaId)
    {
        var key = $"media:{mediaId}";
        if (_openWindows.TryGetValue(key, out var existing) && existing.IsVisible)
        {
            existing.Activate();   // 已开 → 拉前台，不重复开
            return;
        }
        // 否则新建 + 注册 + Closed 时清理
    }

    public void CloseAll() { /* 主窗关闭时调，清理所有子窗 */ }
}
```

注册 Singleton，主窗 `Closing` 事件调 `CloseAll()`。

### 子窗 vs 主窗导航

- **主窗内页面切换**（媒体库 / 任务 / 待处理 等）走 `NavigationService.NavigateToAsync<TViewModel>()`，触发 `CurrentPage` 更新 + ViewLocator 渲染
- **独立窗口**（媒体详情、任务进度分离窗）走 `WindowManager.OpenXxx(...)`，新建 `Window` 实例直接 `Show()`

两种导航不互通：媒体详情**不**作为主窗页面（不在侧栏 NavigationView 里），点击主窗媒体卡 → 开新窗口而非替换内容。

## 系统集成（Phase 3）

桌面端的差异化兑现 Phase——把 OS 集成层做扎实，让用户感觉这是"真正的桌面应用"而不是"网页套壳"。

### 系统托盘（TrayService）

**入口**：`Services/TrayService.cs`，Singleton，由 `App.OnFrameworkInitializationCompleted` 在主窗 `Opened` 后调 `Initialize()` 启动（macOS 需要先有 Window 才能挂 NSStatusBar）。

**功能**：
- TrayIcon 通过 `Avalonia.Controls.TrayIcon` + `NativeMenu` 跨平台原生（Win / macOS NSStatusBar / Ubuntu / 大多数 GNOME 桌面）
- 4 状态动态切色 + 改 ToolTipText：Idle（accent）/ Running（attention 蓝）/ HasFailures（critical 红）/ Paused
- 1s `DispatcherTimer` 轮询 `TaskProgressService.GetAllRootTasks()`——状态变了才重渲染图标（RenderTargetBitmap 不是免费的）
- 菜单：状态头部 / 「打开主窗」/ 实时计数子项 / 「退出 NineKgTools」

**图标动态生成**：

```csharp
// 用 IconLibrary StreamGeometry → Path → RenderTargetBitmap → WindowIcon
// 不需要打包 .ico 资产文件；颜色按 TrayState 切换 brush
var path = new AvaloniaPath { Data = geometry, Fill = brush, Width = 32, Height = 32, Stretch = Uniform };
path.Measure(new Size(32, 32));
path.Arrange(new Rect(0, 0, 32, 32));
var rtb = new RenderTargetBitmap(new PixelSize(32, 32));
rtb.Render(path);
using var ms = new MemoryStream();
rtb.Save(ms);
ms.Position = 0;
return new WindowIcon(ms);
```

**关窗 → 最小化到托盘**：通过 `desktop-preferences.json` 的 `CloseAction` 控制（默认 `MinimizeToTray`）。`App.axaml.cs` 在 MainWindow.Closing 检查 `TrayService.IsExitRequested`：

- 用户从主窗 X 关 + CloseAction=MinimizeToTray → `e.Cancel = true; window.Hide()`，应用继续
- 托盘菜单「退出」→ `TrayService.RequestExit()` 置 `IsExitRequested=true`，再调 `lifetime.Shutdown()`，主窗 Closing 检查到标记后放行

**`ShutdownMode = OnExplicitShutdown`** 是这个机制的前提——默认 `OnLastWindowClose` 会在 `window.Hide()` 后误判为"无窗口"直接退出整个 App。

### 媒体源页（SourcesPage，Phase 3）

监视文件夹的"中心管理页"。**展示** `Config.Source.WatchFolders`（配置项，长期持久化）+ MonitorService 实时状态。

**结构**：

```
┌────────────────────────────────────────────────────────┐
│  媒体源              [+ 添加监视文件夹] [↻ 刷新]       │
│  长期跟踪的文件夹列表 · 新文件加入会自动触发识别        │
├────────────────────────────────────────────────────────┤
│  📥  拖拽快捷方式                                       │
│     把文件夹拖到主窗任意位置 → 弹双卡片选择            │
├────────────────────────────────────────────────────────┤
│  ●  Inbox                          [监控中]            │
│     D:\Media\Inbox                                     │
│     已处理 142 · 失败 3 · 启动于 14:32                 │
│                              [📂] [↻] [✕]              │
│  ⚠  OldArchive                  [路径不存在]            │
│     E:\Media\OldArchive (已断开盘符)                   │
│     请检查路径是否被移动 / 删除                         │
│                                  [✕]                   │
├────────────────────────────────────────────────────────┤
│  已加入监视：Inbox（任务 abc123）            ← Status  │
└────────────────────────────────────────────────────────┘
```

**实现要点**：

- **数据源 = 配置 + 状态**：列表来自 `Config.Source.WatchFolders`（持久化的"应该监控什么"）；每行状态来自 `MonitorService.IsMonitoring/GetMonitoringStats`（运行时的"实际在监控吗"）。两者**不一定一致** —— 比如盘符断开 / 启动失败，配置里有但 MonitorService 没挂上 watcher
- **4 状态视觉**：
  - `Active` 绿 ● + "监控中"
  - `Stopped` 灰 ○ + "已停止"（配置存在但 watcher 没起）
  - `Missing` 红 ⚠ + "路径不存在"（盘符断 / 文件夹被删）
  - `Pending` 蓝 · + "等待启动"（启动期还没跑到 StartProcessConfiguredFolders）
- **5s 轮询**：进入页面起 `DispatcherTimer`，离开 `OnLeaveAsync` 停。状态变化（启动 / 暂停 / 断盘）跟手刷新；配置变化（用户在 Settings 加监视）需手动点"刷新"重读
- **差量更新**：`Refresh()` 保留现有 `WatchFolderItemViewModel` 实例（按 Path 去重），只增减；防止 ItemsControl 全量重渲闪屏
- **添加流程**：FolderPicker → 加入 `WatchFolders` → `SaveConfig` → `IdentifyBatchMedia(path, startMonitoringAfterCompletion: true)` —— 与 `DragDropDispatcher.AddToWatchAsync` 一致路径
- **移除流程**：`NineKgConfirmDialog Destructive` → `MonitorService.StopMonitoring(path)` → `WatchFolders.RemoveAll` → `SaveConfig`。文案明示"已识别入库的媒体不会被删除"
- **重新扫描**：`IdentifyBatchMedia(path, startMonitoringAfterCompletion: false)` 触发一次性识别，**不影响**当前监视状态——纯补扫
- **拖拽快捷方式 InfoBar**：明示"拖拽到主窗任意位置即可加入监视"，避免用户找不到拖入位置（拖到 SourcesPage 内也可，因为 MainWindow 全局接受）

**与 BackgroundTasksPage 的分工**：

| 页面 | 数据源 | 用途 |
|---|---|---|
| **SourcesPage** | `Config.Source.WatchFolders` + `MonitorService.IsMonitoring` | 编辑"应该监控什么" |
| **BackgroundTasksPage** | `TaskProgressService.GetAllRootTasks()` | 看实时识别 / 监控任务进度 |

修改 SourcesPage 的添加 / 删除逻辑时**必须**两边对齐——Settings"媒体源"分组的列表编辑器走同一份 `_config.Source.WatchFolders`，行为保持一致。

### 拖拽接收（DragDropDispatcher + Overlay）

**入口**：MainWindow ctor `DragDrop.SetAllowDrop(this, true)` + `AddHandler(DragEnterEvent / DragLeaveEvent / DropEvent)`。

**Overlay 防误触**：`DragEnter` 启动 200ms `DispatcherTimer`，超时仍在拖（没 leave）才显示半透明 overlay；`DragLeave` 或 `Drop` 立即 stop + 隐藏。避免快速划过窗口时闪现 overlay。

**路径分发**（`DragDropDispatcher.HandleDropAsync`）：
- 单文件 → `FilesService.IdentifySingleMedia(path)`，无对话框（最快路径）
- 单文件夹 → 弹 `DragDropFolderActionDialog` 双卡片（加入监视 / 一次性识别）
- 多个项 → `NineKgConfirmDialog Affirmative` 确认后逐项分类处理

**`IStorageItem.TryGetLocalPath()`** 是 Avalonia.Platform.Storage 命名空间的扩展方法——容易漏 using，导致编译报"未包含定义"。

### 命令行 + IPC 单实例（IpcService）

**单实例**：Program.Main 用 `Mutex(initiallyOwned: true, name: "Local\\NineKgTools.Desktop.{user}")` 检查；Mutex 已存在 → 转发命令到现有进程后退出。

**IPC 通道**：NamedPipe（跨平台 .NET 抽象，Win / Mac / Linux 都能用）：
- 通道名：`NineKgTools.Desktop.IPC.{username}`（多用户系统不冲突）
- 协议：JSON-Lines，`IpcCommand { Cmd, Path }`
- Server：后台 `Task` loop，`NamedPipeServerStream.WaitForConnectionAsync` → 读一行 → 反序列化 → 调 handler
- Client：`NamedPipeClientStream` + 2s connect 超时（超时即认为没现有进程）

**支持命令**：`--identify <path>` / `--show-main` / `--quit`。

**启动时携带的命令** 走 `Program.Pending` 静态字段；`App.OnFrameworkInitializationCompleted` 在主窗 `Opened` 后消费，避免在 IpcService 还没起来时就调度。

### Windows Shell 右键集成（ShellIntegrationService）

**注册位置**：`HKEY_CURRENT_USER\Software\Classes\*\shell\NineKgToolsIdentify` 和 `Directory\shell\NineKgToolsIdentify`——HKCU 不需要 UAC 提权。

**写入字段**：
- `(默认)` = 「用 NineKgTools 识别」（菜单显示文案）
- `Icon` = exe 路径（让右键菜单旁显示项目图标）
- `command\(默认)` = `"<exe_path>" --identify "%1"`

**Settings 切换**：「外观」分组的 ToggleSwitch 调 `ToggleShellIntegration` 命令，注册 / 卸载 + 把状态同步到 `DesktopPreferences.ShellIntegrationRegistered`。

**非 Windows 平台**：`ShellIntegrationService.IsSupported` 返回 false，Settings UI `IsVisible` 自动隐藏整个分组。

**Win11 紧凑菜单坑**：默认隐藏旧式 verb，用户需要「显示更多选项」才能看到「用 NineKgTools 识别」——这是 Win11 OS 行为，不是项目缺陷。新的 `IExplorerCommand` API 可以让 verb 进紧凑菜单，但需要 packaged COM extension（Phase 3 不做）。

### 多窗口增强

**WindowStateService**：把 `Window` 的 size + position 持久化到 `DesktopPreferences.WindowStates[key]`：

- key 选择：主窗 = `"main"`；MediaDetailWindow = `"media"`（同类型窗口共享一份位置，避免每个 mediaId 各占一份冗余）；TaskDiagnosticsWindow = `"diagnostics"`
- 还原时机：`window.Opened` 之前——读取持久化的 X/Y/Width/Height + IsMaximized
- 保存时机：`window.PositionChanged` / `window.ClientSizeProperty` observable / `window.Closing`——拖动调整也保存（防应用崩溃丢位置）
- 屏幕边界检查：还原前确认窗口至少有 50px×50px 在某个屏幕内，避免恢复到已断开的副屏外（笔记本拔屏等）

**子窗共享行为**（`WindowExtensions.EnableChildWindowFeatures(window, key)`）：
- 自动接 WindowStateService
- `Ctrl+W` 关自己；主窗不响应 `Ctrl+W`（避免误关）
- `Ctrl+T` 切 `Topmost` 置顶

**主窗快捷键**：`Ctrl+1..9` 跳到 NavigationView 的第 N 个 MenuItem——直接通过 `NavView.SelectedItem = items[idx]` 触发原本的 `OnNavigationSelectionChanged`，复用导航逻辑，零特殊路径。

### 首页（HomePage）状态卡

桌面端用户启动应用后**第一眼**应该看到"应用真在工作"——所以 HomePage 4 张状态卡都接 Phase 3 实时数据：

| 卡片 | 数据源 | 用途 |
|---|---|---|
| 媒体总数 | `MediaDbContext.Medias.Count`（`OnEnter` 一次） | 库规模 |
| 监视文件夹 | `Config.Source.WatchFolders.Count` + `MonitorService.IsMonitoring` | 配置 vs 实际运行（active/total） |
| 运行中任务 | `TaskProgressService.GetAllRootTasks()` filter Running/Pending/Retrying | 桌面端关窗后台仍在跑的可视化 |
| 失败任务 | 同上 filter Failed/Timeout | Critical 色突出，**HasFailedTasks** 控制副标题颜色 |

**5s 轮询**：监视状态 / 任务计数高变化频率，进入页面起 timer，离开 stop。媒体总数变化频率低，仅在 `OnEnter` 拉一次。

### 子窗 📌 置顶按钮

`MediaDetailWindow` 和 `TaskDiagnosticsWindow` 都加了 `<ToggleButton IsChecked="{Binding $parent[Window].Topmost, Mode=TwoWay}" />`——`$parent[Window]` ancestor binding 直接绑 Window 的 Topmost 属性，省一个 ViewModel 字段。

放置位置：

- TaskDiagnosticsWindow：头部右侧（紧凑场景，按钮 + "置顶"文字）
- MediaDetailWindow：Hero 区主操作组里（与「文件管理器打开」「重新识别」并排）

`Ctrl+T` 快捷键由 `WindowExtensions.EnableChildWindowFeatures` 提供——按钮和快捷键都触发同一份 `Topmost` 属性，UI 双向同步。

### 首次关窗到托盘的引导

`DesktopPreferences.TrayHintShown` 控制——第一次按 X 时：

1. `e.Cancel = true` 阻止关闭走原路径
2. 置 `TrayHintShown = true` + `RequestSave()`
3. fire-and-forget 弹 `NineKgConfirmDialog Info` 解释"应用仍在托盘运行"
4. `window.Hide()` 立刻最小化（不等 dialog 关闭——ContentDialog 的 OverlayLayer 浮在桌面上层，不依赖主窗可见）

二次起按 X 直接 Hide 不再提示。用户在 Settings 改回"退出应用"模式后该提示也不再出现（CloseAction 检查在前）。

### Settings Shell 集成的辅助按钮

`Settings / 外观 / 集成 Windows 资源管理器` 下方多两个按钮（仅在已注册时显示）：

- **🔌 测试通道**：`IpcService.TrySendAsync(new IpcCommand { Cmd = "show-main" })` 自连发送，验证 IPC 通道能收到——这意味着右键菜单触发 `--identify` 时也能被现有进程接住
- **↻ 重置注册**：`Unregister()` 后 `Register()`，用于应用被移动 / 注册表被外部修改导致 verb 失效。走 `NineKgConfirmDialog Affirmative` 确认

### 桌面端独立持久化（DesktopPreferences）

`config.yaml` 是 Web/Desktop 共享的，桌面端独有的 UI 偏好（关窗行为、主题、Shell 集成状态、窗口位置）通过 `dataDir/desktop-preferences.json` 单独落盘，**不污染** `config.yaml`。

```csharp
public class DesktopPreferences
{
    public CloseAction CloseAction { get; set; } = CloseAction.MinimizeToTray;
    public string? Theme { get; set; }
    public bool TrayHintShown { get; set; }
    public bool ShellIntegrationRegistered { get; set; }
    public Dictionary<string, WindowState> WindowStates { get; set; } = new();
}
```

500ms 防抖落盘（`RequestSave()`），错误吞掉只 Log.Warning——保存失败不该阻塞 UI。

### 平台能力对照表（Phase 3）

| 功能 | Win11 | Win10 | macOS 14+ | Ubuntu 24.04 |
|---|---|---|---|---|
| 系统托盘 | ✓ | ✓ | ✓ (NSStatusBar) | △ (依赖桌面环境) |
| 关窗最小化到托盘 | ✓ | ✓ | ✓ | △ |
| 文件拖拽接收 | ✓ | ✓ | ✓ | ✓ |
| Ctrl+W / Ctrl+1..9 / Ctrl+T 快捷键 | ✓ | ✓ | ✓（Cmd 键也支持？需测） | ✓ |
| 窗口位置记忆 | ✓ | ✓ | ✓ | ✓ |
| 单实例 + IPC | ✓ | ✓ | ✓ | ✓ |
| 右键 Shell 集成 | ✓ (HKCU verb) | ✓ | ✗ (Phase 3 不做 macOS Service) | ✗ |
| 命令行 --identify | ✓ | ✓ | ✓ | ✓ |

**降级原则**：所有平台特定调用都包 try/catch + Log.Warning——「功能缺失但不崩」。例如 TrayService.Initialize 在 GNOME 无 indicator extension 时静默失败，主窗仍能用。

## 主题适配（双主题强制）

`Application.RequestedThemeVariant="Default"`——跟随 Win11 浅色/深色设置。**所有自定义视觉**（Hero 渐变、Intent accent、5 类别色）必须在浅深两套主题下都验证。

`BrandResources.axaml` 内 `<ResourceDictionary.ThemeDictionaries>` 块按 Light/Dark 区分定义资源；非主题敏感资源（StreamGeometry、CornerRadius 等）放在外层。

```xml
<ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
        <SolidColorBrush x:Key="BrandCategoryVideoBrush" Color="#5B6BC9" />
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
        <SolidColorBrush x:Key="BrandCategoryVideoBrush" Color="#8B95D9" />
    </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

切主题时这些 brush 自动 swap。**禁止**写死 hex 色值，全走 brush key。

## 动画设计规范

### 卡片 hover lift

```xml
<Style Selector="Border.media-card:pointerover">
    <Setter Property="BorderBrush" Value="{DynamicResource AccentFillColorDefaultBrush}" />
    <Setter Property="RenderTransform" Value="translateY(-2px)" />
</Style>
```

需要在父 Style 加 Transitions：

```xml
<Style Selector="Border.media-card">
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.12" />
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.12" />
        </Transitions>
    </Setter>
</Style>
```

### 加载骨架 pulse

骨架 Border `Opacity="0.5"`——Phase 1 不做真 pulse 动画（会让其他 transitions 难调），后续可加 `OpacityTransition` 做 0.4 → 0.7 → 0.4 循环。

## 与 Web 端的对照

| 维度 | Web 端 | 桌面端 |
|---|---|---|
| 渲染管线 | Blazor + MudBlazor + CSS | Avalonia 11 + FluentAvalonia + AXAML |
| 设计令牌位置 | `wwwroot/css/` | `Themes/BrandResources.axaml` |
| 类名/选择器 | CSS 类（`.nk-dialog-*` 等） | Avalonia Style Selector（`Button.cat-tab.sel-video`） |
| 主色 | MudBlazor Primary（紫色） | 系统 accent（用户改 Win11 主题色跟变） |
| 卡片体系 | `MediaCard / SimpleMediaCard` Razor 组件 | `MediaCardViewModel` + ItemTemplate |
| 弹窗 | `NineKgConfirmDialog.razor`（DialogService） | `NineKgConfirmDialog.cs`（ContentDialog 静态 ShowAsync） |
| 路由 | Blazor `@page "/media/{id}"` | NavigationService + 独立 Window |

**两端视觉不要求一致**——共享业务语义（4 Intent、5 类别、识别诊断结构）+ 各自原生平台的视觉语言。强制一致反而会稀释各端原生感。

## 后续 Phase 扩展点

- **Phase 2** 大量页面（识别诊断 / 网站配置 / 设置 / 标签 / 创作者 / 收藏夹）
- **Phase 2.6** 视觉 token 增量：可能增加 `BrandActionBarHeight`（批量 ActionBar）、`BrandHeroOverlay`（媒体详情封面叠层）
- **Phase 3** 桌面端独占体验（系统托盘 / 文件拖拽 / Shell 集成 / 多窗口快捷键）
- **Phase 1.3 编辑模式** 细化（媒体详情进入可编辑态 + SaveBar）

详见 [`docs/todo/desktop-phase-1.md`](../todo/desktop-phase-1.md) 至 [`desktop-phase-4.md`](../todo/desktop-phase-4.md) 的具体 task + 设计稿。
