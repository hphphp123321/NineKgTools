# NineKgTools 前端设计风格指南

## 概述

NineKgTools 采用现代化的前端设计系统，基于 Material Design 原则，结合自定义的设计令牌和组件系统，创造出一致、美观、易用的用户界面。

## 设计理念

### 核心原则
1. **一致性优先** - 统一的视觉语言和交互模式
2. **用户体验至上** - 流畅的动画和直观的操作
3. **响应式设计** - 适配各种屏幕尺寸和设备
4. **可访问性** - 支持键盘导航和屏幕阅读器
5. **性能优化** - 轻量级动画和高效渲染

### 视觉风格
- **现代简约** - 清晰的层次结构，充足的留白
- **Material Design 3.0** - 基于 MudBlazor 的 Material 组件
- **渐变与阴影** - 营造深度感和层次感
- **微交互** - 细腻的悬停效果和状态反馈

## 设计系统架构

### CSS 架构
```
wwwroot/css/
├── variables.css          # 设计令牌系统
├── utilities.css          # 原子化工具类
├── global.css            # 全局基础样式
├── components/           # 组件级样式
│   ├── cards.css        # 卡片组件系统
│   ├── animations.css   # 动画效果库
│   ├── forms.css        # 表单组件样式
│   ├── photo-wall.css   # 图片墙组件
│   └── search.css       # 搜索组件样式
└── pages/               # 页面特定样式
    ├── home.css         # 首页样式
    ├── media.css        # 媒体详情页
    ├── settings.css     # 设置页面
    └── creators.css     # 创作者页面
```

## 设计令牌系统

### 间距系统
```css
--spacing-xs: 4px;    /* 最小间距 */
--spacing-sm: 8px;    /* 小间距 */
--spacing-md: 12px;   /* 中等间距 */
--spacing-lg: 16px;   /* 大间距 */
--spacing-xl: 20px;   /* 超大间距 */
--spacing-2xl: 24px;  /* 2倍超大间距 */
--spacing-3xl: 32px;  /* 3倍超大间距 */
--spacing-4xl: 48px;  /* 4倍超大间距 */
```

### 圆角系统
```css
--radius-xs: 2px;     /* 最小圆角 */
--radius-sm: 4px;     /* 小圆角 */
--radius-md: 6px;     /* 中等圆角 */
--radius-lg: 8px;     /* 大圆角 */
--radius-xl: 12px;    /* 超大圆角 */
--radius-2xl: 16px;   /* 2倍超大圆角 */
--radius-full: 9999px; /* 完全圆角 */
```

### 阴影系统
```css
--shadow-xs: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
--shadow-sm: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px -1px rgba(0, 0, 0, 0.1);
--shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1);
--shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -4px rgba(0, 0, 0, 0.1);
--shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1);
```

### 动画系统
```css
--transition-fast: 0.15s ease;     /* 快速过渡 */
--transition-normal: 0.2s ease;    /* 标准过渡 */
--transition-slow: 0.3s ease;      /* 慢速过渡 */
--transition-slower: 0.5s ease;    /* 最慢过渡 */
```

## 组件设计规范

### 卡片组件系统

#### 基础卡片 (.card-base)
```css
.card-base {
    background-color: var(--mud-palette-surface);
    border-radius: var(--radius-lg);
    padding: var(--spacing-lg);
    transition: all var(--transition-slow);
    overflow: hidden;
    position: relative;
}

.card-base:hover {
    box-shadow: var(--shadow-lg);
    transform: translateY(-2px);
}
```

#### 卡片变体
- **边框卡片** - 左侧彩色边框标识不同类型
  - `.card-bordered-primary` - 主色边框
  - `.card-bordered-success` - 成功色边框
  - `.card-bordered-warning` - 警告色边框
  - `.card-bordered-info` - 信息色边框

- **渐变卡片** - 渐变背景营造视觉层次
  - `.card-gradient-primary` - 主色渐变
  - `.card-gradient-success` - 成功色渐变

- **功能卡片** - 特定功能的卡片样式
  - `.card-header` - 页面头部卡片
  - `.card-media` - 媒体内容卡片
  - `.card-creator-main` - 创作者主卡片

### 媒体卡片组件

#### MediaCard 设计特点
- **固定尺寸** - 宽度 210px，保持一致性
- **悬停效果** - 上升 5px + 阴影增强
- **海报缩放** - 图片 1.05 倍缩放效果
- **覆盖层** - 渐变遮罩显示操作按钮
- **类型标识** - 左上角彩色标签

```css
.media-card {
    width: 210px; 
    transition: all var(--transition-slow);
    height: 100%;
}

.media-card:hover {
    transform: translateY(-5px);
    box-shadow: 0 8px 15px rgba(0,0,0,0.1);
}
```

#### SimpleMediaCard 设计特点
- **紧凑布局** - 140x140px 海报 + 60px 内容区
- **快速预览** - 标题 + 发布日期
- **类型标识** - 彩色标签区分媒体类型

### 搜索组件设计

#### GlobalSearchBox 特点
- **现代化输入框** - 圆角填充样式
- **实时搜索** - 输入即搜索，无需提交
- **状态指示** - 搜索状态的视觉反馈
- **结果预览** - 下拉式搜索结果展示
- **高级筛选** - 浮动操作按钮

```css
.ultra-modern-search-container {
    display: flex;
    align-items: center;
    gap: var(--spacing-md);
    position: relative;
}

.search-input-wrapper {
    flex: 1;
    position: relative;
    transition: all var(--transition-slow);
}
```

### 图片墙组件 (PhotoWall)

#### 设计特点
- **瀑布流布局** - 自适应列数和图片比例
- **位置计算** - 基于图片原始尺寸的智能布局
- **懒加载** - 性能优化的图片加载
- **悬停效果** - 上升 + 缩放 + 阴影
- **信息覆盖** - 渐变遮罩显示媒体信息

```css
.photo-wall-item:hover {
    transform: translateZ(0) translateY(-4px) scale(1.02);
    box-shadow: var(--shadow-xl);
    z-index: 10;
}
```

## 页面设计规范

### 首页 (Home.razor)

#### 设计特点
- **仪表板布局** - 统计卡片 + 内容预览
- **渐进式动画** - 卡片依次进入动画
- **统计可视化** - 数字展示 + 图标标识
- **快速访问** - 最近内容和随机图片墙

#### 关键样式
```css
.dashboard-item {
    animation: slideInUp 0.6s ease-out;
}

.dashboard-item:nth-child(1) { animation-delay: 0.1s; }
.dashboard-item:nth-child(2) { animation-delay: 0.2s; }
.dashboard-item:nth-child(3) { animation-delay: 0.3s; }
```

### 媒体详情页 (MediaPage.razor)

#### 设计特点
- **三栏布局** - 海报 + 详情 + 操作
- **粘性侧边栏** - 左侧海报区域粘性定位
- **动态标题** - 下划线动画效果
- **标签交互** - 悬停上升效果
- **链接样式** - 渐变边框 + 悬停效果

#### 关键样式
```css
.media-title::after {
    content: '';
    position: absolute;
    width: 0;
    height: 2px;
    bottom: 0;
    left: 0;
    background-color: var(--mud-palette-primary);
    transition: width var(--transition-slower);
}

.media-main-card:hover .media-title::after {
    width: 100%;
}
```

### 设置页面 (Settings.razor)

#### 设计特点
- **现代化标签页** - 渐变头部 + 滑动指示器
- **卡片分组** - 功能模块卡片化
- **表单增强** - 悬停和焦点状态
- **状态指示** - 连接状态的视觉反馈

#### 标签页样式
```css
.modern-tabs-header {
    background: linear-gradient(135deg, 
        rgba(var(--mud-palette-primary-rgb), 0.08) 0%,
        rgba(var(--mud-palette-primary-rgb), 0.04) 30%,
        rgba(var(--mud-palette-info-rgb), 0.05) 70%,
        rgba(var(--mud-palette-secondary-rgb), 0.03) 100%);
    border-bottom: 2px solid rgba(var(--mud-palette-primary-rgb), 0.15);
}
```

## 动画设计规范

### 进场动画
- **淡入动画** - 透明度 + 轻微位移
- **缩放进入** - 从 0.95 倍缩放到 1 倍
- **滑入动画** - 从左/右滑入

### 交互动画
- **悬停上升** - translateY(-2px 到 -5px)
- **悬停缩放** - scale(1.02 到 1.05)
- **悬停阴影** - 阴影强度增加

### 状态动画
- **加载动画** - 脉动效果
- **骨架屏** - 闪烁加载效果
- **进度指示** - 圆形进度条

## 响应式设计

### 断点系统
- **xs**: < 600px (手机)
- **sm**: 600px - 960px (平板)
- **md**: 960px - 1280px (小桌面)
- **lg**: 1280px - 1920px (大桌面)
- **xl**: > 1920px (超大屏)

### 适配策略
- **移动端优先** - 从小屏幕开始设计
- **渐进增强** - 大屏幕添加更多功能
- **触摸友好** - 足够大的点击区域
- **简化交互** - 移动端简化悬停效果

## 颜色系统

### 媒体类型颜色
```css
Game: Color.Primary (蓝色)
Audio: Color.Success (绿色)
Video: Color.Warning (橙色)
Picture: Color.Secondary (紫色)
Text: Color.Info (青色)
```

### 状态颜色
- **成功状态** - 绿色系
- **警告状态** - 橙色系
- **错误状态** - 红色系
- **信息状态** - 蓝色系

## 性能优化

### CSS 优化
- **CSS 变量** - 减少重复值
- **GPU 加速** - transform 和 opacity 动画
- **选择器优化** - 避免深层嵌套

### 动画优化
- **will-change** - 提前声明变化属性
- **transform3d** - 启用硬件加速
- **防抖节流** - 限制动画频率

## 组件使用指南

### 卡片组件使用规范

#### 基础用法
```html
<!-- 标准卡片 -->
<MudCard Class="card-base">
    <MudCardContent>
        <!-- 内容 -->
    </MudCardContent>
</MudCard>

<!-- 带边框的卡片 -->
<MudCard Class="card-base card-bordered-primary">
    <MudCardContent>
        <!-- 主要内容 -->
    </MudCardContent>
</MudCard>

<!-- 页面头部卡片 -->
<MudCard Class="card-base card-header">
    <MudCardContent>
        <div class="d-flex align-center">
            <MudAvatar Size="Size.Large" Color="Color.Primary" Class="mr-4 header-icon">
                <MudIcon Icon="@Icons.Material.Filled.Dashboard" Size="Size.Large"/>
            </MudAvatar>
            <div>
                <MudText Typo="Typo.h4" Class="card-title">页面标题</MudText>
                <MudText Typo="Typo.body1" Class="card-subtitle">页面描述</MudText>
            </div>
        </div>
    </MudCardContent>
</MudCard>
```

#### 媒体卡片使用
```html
<!-- 标准媒体卡片 -->
<MediaCard Media="@mediaItem" HideFavoriteButton="false" />

<!-- 简化媒体卡片 -->
<SimpleMediaCard Media="@mediaItem" />
```

### 搜索组件使用

#### 全局搜索框
```html
<GlobalSearchBox OnSearchResultSelected="HandleSearchResult" />
```

#### 搜索结果预览
```html
<SearchResultPreview SearchResult="@searchResult"
                     OnMediaSelected="HandleMediaSelected"
                     OnTagSelected="HandleTagSelected" />
```

### 图片墙组件使用

#### 基础图片墙
```html
<PhotoWall Images="@imageList"
           Title="随机图片墙"
           ImageCount="20"
           Columns="0" />
```

#### 自定义图片墙
```html
<PhotoWall Images="@imageList"
           Title="最新作品"
           ImageCount="40"
           Columns="4"
           ShowControls="true" />
```

## 表单设计规范

### 输入框样式
```html
<!-- 标准输入框 -->
<MudTextField T="string"
              Label="标签名称"
              Variant="Variant.Outlined"
              Class="mb-3" />

<!-- 带图标的输入框 -->
<MudTextField T="string"
              Label="搜索"
              Variant="Variant.Outlined"
              Adornment="Adornment.Start"
              AdornmentIcon="@Icons.Material.Filled.Search"
              AdornmentColor="Color.Primary" />
```

### 选择器样式
```html
<!-- 下拉选择 -->
<MudSelect T="string"
           Label="选择类型"
           Variant="Variant.Outlined"
           AnchorOrigin="Origin.BottomCenter">
    <MudSelectItem Value="@("option1")">选项1</MudSelectItem>
    <MudSelectItem Value="@("option2")">选项2</MudSelectItem>
</MudSelect>

<!-- 自动完成 -->
<MudAutocomplete T="string"
                 Label="标签搜索"
                 SearchFunc="@SearchTags"
                 Variant="Variant.Outlined"
                 Clearable="true" />
```

### 按钮样式规范
```html
<!-- 主要操作按钮 -->
<MudButton Variant="Variant.Filled"
           Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.Save">
    保存
</MudButton>

<!-- 次要操作按钮 -->
<MudButton Variant="Variant.Outlined"
           Color="Color.Secondary"
           StartIcon="@Icons.Material.Filled.Cancel">
    取消
</MudButton>

<!-- 危险操作按钮 -->
<MudButton Variant="Variant.Filled"
           Color="Color.Error"
           StartIcon="@Icons.Material.Filled.Delete">
    删除
</MudButton>
```

## 布局设计规范

### 页面布局模式

#### 标准页面布局
```html
<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <!-- 页面标题区域 -->
    <MudGrid>
        <MudItem xs="12">
            <MudCard Class="card-base card-header mb-4">
                <!-- 页面头部内容 -->
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- 主要内容区域 -->
    <MudGrid>
        <MudItem xs="12" md="8">
            <!-- 主要内容 -->
        </MudItem>
        <MudItem xs="12" md="4">
            <!-- 侧边栏内容 -->
        </MudItem>
    </MudGrid>
</MudContainer>
```

#### 三栏布局（媒体详情页）
```html
<MudGrid>
    <!-- 左侧海报 -->
    <MudItem xs="12" md="3">
        <MudCard Class="card-base card-sidebar">
            <!-- 海报和基本信息 -->
        </MudCard>
    </MudItem>

    <!-- 中间详情 -->
    <MudItem xs="12" md="6">
        <MudCard Class="card-base card-bordered-primary">
            <!-- 详细信息 -->
        </MudCard>
    </MudItem>

    <!-- 右侧操作 -->
    <MudItem xs="12" md="3">
        <MudCard Class="card-base card-bordered-success">
            <!-- 操作按钮和相关信息 -->
        </MudCard>
    </MudItem>
</MudGrid>
```

### 网格系统使用

#### 响应式网格
```html
<MudGrid>
    <!-- 在不同屏幕尺寸下显示不同列数 -->
    <MudItem xs="12" sm="6" md="4" lg="3">
        <!-- 内容 -->
    </MudItem>
</MudGrid>
```

#### 卡片网格
```html
<MudGrid>
    @foreach (var item in items)
    {
        <MudItem xs="12" sm="6" md="4" lg="3" Class="dashboard-item">
            <MudCard Class="card-base card-hover-scale">
                <!-- 卡片内容 -->
            </MudCard>
        </MudItem>
    }
</MudGrid>
```

## 图标使用规范

### 图标选择原则
- **语义明确** - 图标含义清晰易懂
- **风格统一** - 使用 Material Icons 图标集
- **尺寸适当** - 根据上下文选择合适尺寸
- **颜色协调** - 与主题色彩保持一致

### 常用图标映射
```csharp
// 媒体类型图标
TopCategory.Game => Icons.Material.Filled.VideogameAsset
TopCategory.Audio => Icons.Material.Filled.Headphones
TopCategory.Video => Icons.Material.Filled.SmartDisplay
TopCategory.Picture => Icons.Material.Filled.Image
TopCategory.Text => Icons.Material.Filled.LibraryBooks

// 操作图标
保存 => Icons.Material.Filled.Save
删除 => Icons.Material.Filled.Delete
编辑 => Icons.Material.Filled.Edit
搜索 => Icons.Material.Filled.Search
设置 => Icons.Material.Filled.Settings
```

### 图标使用示例
```html
<!-- 带图标的标题 -->
<div class="d-flex align-center">
    <MudIcon Icon="@Icons.Material.Filled.Dashboard"
             Color="Color.Primary"
             Class="mr-2"/>
    <MudText Typo="Typo.h6">仪表板</MudText>
</div>

<!-- 按钮图标 -->
<MudButton StartIcon="@Icons.Material.Filled.Add"
           Color="Color.Primary">
    添加新项
</MudButton>

<!-- 头像图标 -->
<MudAvatar Color="Color.Primary" Size="Size.Large">
    <MudIcon Icon="@Icons.Material.Filled.Person" Size="Size.Large"/>
</MudAvatar>
```

## 加载状态设计

### 骨架屏设计
```html
<!-- 媒体卡片骨架屏 -->
<MudCard Class="media-card-skeleton rounded-lg ma-2" Elevation="2">
    <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="150px"/>
    <MudCardContent Class="pb-1">
        <MudSkeleton Width="30%" Height="42px"/>
        <MudSkeleton Width="80%"/>
        <MudSkeleton Width="100%"/>
    </MudCardContent>
</MudCard>

<!-- 列表骨架屏 -->
@for (int i = 0; i < 5; i++)
{
    <MudCard Class="mb-2">
        <MudCardContent>
            <div class="d-flex">
                <MudSkeleton SkeletonType="SkeletonType.Circle" Width="40px" Height="40px"/>
                <div class="ml-3 flex-grow-1">
                    <MudSkeleton Width="60%" Height="20px"/>
                    <MudSkeleton Width="40%" Height="16px" Class="mt-1"/>
                </div>
            </div>
        </MudCardContent>
    </MudCard>
}
```

### 进度指示器
```html
<!-- 按钮加载状态 -->
<MudButton Disabled="@_isLoading">
    @if (_isLoading)
    {
        <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true"/>
        <MudText Class="ms-2">处理中</MudText>
    }
    else
    {
        <MudText>提交</MudText>
    }
</MudButton>

<!-- 页面加载状态 -->
@if (_isLoading)
{
    <div class="d-flex justify-center align-center" style="height: 200px;">
        <MudProgressCircular Color="Color.Primary" Indeterminate="true"/>
    </div>
}
```

## 错误状态设计

### 空状态设计
```html
<div class="empty-state">
    <div class="empty-state-icon">
        <MudIcon Icon="@Icons.Material.Filled.SearchOff"
                 Size="Size.Large"
                 Color="Color.Secondary"/>
    </div>
    <MudText Typo="Typo.h6" Color="Color.Secondary" Class="mb-2">
        没有找到相关内容
    </MudText>
    <MudText Typo="Typo.body2" Color="Color.Secondary">
        尝试调整搜索条件或添加新内容
    </MudText>
    <MudButton Variant="Variant.Outlined"
               Color="Color.Primary"
               Class="mt-3"
               StartIcon="@Icons.Material.Filled.Add">
        添加内容
    </MudButton>
</div>
```

### 错误提示设计
```html
<!-- 警告提示 -->
<MudAlert Severity="Severity.Warning" Dense="true" Class="mb-3">
    <div class="d-flex align-center">
        <MudIcon Icon="@Icons.Material.Filled.Warning" Class="mr-2"/>
        <span>请注意：此操作不可撤销</span>
    </div>
</MudAlert>

<!-- 错误提示 -->
<MudAlert Severity="Severity.Error" Dense="true" Class="mb-3">
    <div class="d-flex align-center">
        <MudIcon Icon="@Icons.Material.Filled.Error" Class="mr-2"/>
        <span>操作失败：@errorMessage</span>
    </div>
</MudAlert>
```

## 开发最佳实践

### CSS 类命名规范
- **组件前缀** - 使用组件名作为前缀（如 `.media-card-`）
- **状态修饰符** - 使用状态描述（如 `.is-loading`, `.is-active`）
- **BEM 方法论** - 块-元素-修饰符命名方式

### 响应式开发
```css
/* 移动端优先 */
.component {
    /* 基础样式 */
}

@media (min-width: 768px) {
    .component {
        /* 平板样式 */
    }
}

@media (min-width: 1024px) {
    .component {
        /* 桌面样式 */
    }
}
```

### 性能优化建议
1. **避免深层嵌套** - CSS 选择器不超过 3 层
2. **使用 transform** - 优先使用 transform 和 opacity 做动画
3. **合理使用 will-change** - 提前声明变化属性
4. **减少重排重绘** - 避免频繁修改布局属性

## 代码分离规范

### Razor 文件结构
```
Pages/
├── MediaPage.razor          # 页面结构和样式
├── MediaPage.razor.cs       # 页面逻辑和数据处理
└── MediaPage.razor.css      # 页面特定样式（可选）
```

### 职责分离原则
- **`.razor` 文件** - 负责页面结构、MudBlazor 组件使用、样式类应用
- **`.razor.cs` 文件** - 负责业务逻辑、数据处理、事件处理、API 调用
- **CSS 文件** - 负责样式定义、动画效果、响应式布局

### 示例代码结构

#### MediaPage.razor (页面结构)
```html
@page "/media/{id:int}"
@using NineKgTools.Components.Medias

<PageTitle>@(_media?.Title ?? "媒体详情")</PageTitle>

@if (_isLoading)
{
    <!-- 加载状态 -->
    <MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
        <MudSkeleton Height="400px" Class="rounded-lg"/>
    </MudContainer>
}
else if (_media != null)
{
    <!-- 媒体内容 -->
    <MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4 fade-in">
        <MudGrid>
            <MudItem xs="12" md="3" Class="media-sidebar">
                <MudCard Class="card-base card-sidebar">
                    <!-- 海报区域 -->
                </MudCard>
            </MudItem>
            <MudItem xs="12" md="9">
                <MudCard Class="card-base card-bordered-primary">
                    <!-- 详情区域 -->
                </MudCard>
            </MudItem>
        </MudGrid>
    </MudContainer>
}
```

#### MediaPage.razor.cs (页面逻辑)
```csharp
public partial class MediaPage : ComponentBase
{
    [Parameter] public int Id { get; set; }

    private MediaBase? _media;
    private bool _isLoading = true;
    private bool _saveProcessing = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadMediaAsync();
    }

    private async Task LoadMediaAsync()
    {
        _isLoading = true;
        try
        {
            _media = await MediaService.GetByIdAsync(Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load media {Id}", Id);
            Snackbar.Add("加载媒体失败", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveMediaAsync()
    {
        if (_media == null) return;

        _saveProcessing = true;
        try
        {
            await MediaService.UpdateAsync(_media);
            Snackbar.Add("保存成功", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save media {Id}", Id);
            Snackbar.Add("保存失败", Severity.Error);
        }
        finally
        {
            _saveProcessing = false;
        }
    }
}
```

## 组件开发反模式 Checklist

> **团队审计沉淀**：以下 8 大反模式 + 配套修复模板来自对 `Components/Common`、`Components/Creators`、`Components/Medias`、`Components/FileExplorer` 四个目录、**108 项** 审计问题的系统化归纳——其中 **93%** 的问题都命中了这 8 条模式。写新组件、Code Review、定期审计时请对照自查。

### 1. `<div>` / `<MudPaper>` 作为按钮必须键盘可达

**反模式**：`<MudPaper @onclick="...">` 或 `<div @onclick="...">` 没有键盘支持——鼠标能点，但键盘用户和屏幕阅读器完全进不去（WCAG 2.1.1 A 违规）。

**修复模板**：
```razor
<MudPaper role="button"
          tabindex="0"
          aria-pressed="@(isSelected ? "true" : "false")"
          aria-label="@GetAriaLabel(item, isSelected)"
          @onclick="() => Toggle(itemLocal)"
          @onkeydown="@((KeyboardEventArgs e) => OnKeyDown(e, itemLocal))">
```
```csharp
private void OnKeyDown(KeyboardEventArgs e, TItem item)
{
    if (e.Key == "Enter" || e.Key == " " || e.Key == "Spacebar")
        Toggle(item);
}
```
**注意**：`foreach` 循环里要用局部变量 `var itemLocal = item` 避免 closure 捕获坑。

### 2. 选中判定用 HashSet，禁止 `.Any(x => x.Id == item.Id)`

**反模式**：在 `foreach` 里调用 `selectedList.Any(c => c.Id == item.Id)` —— O(n) × n 次渲染 = **O(n²)**。50 项时每帧 2500 次对比。

**修复模板**：
```csharp
private List<TItem> _selected = new();                // 保留插入顺序用作返回结果
private readonly HashSet<int> _selectedIds = new();    // O(1) 选中判定索引

private bool IsSelected(TItem item) => _selectedIds.Contains(item.Id);

private void Toggle(TItem item)
{
    if (_selectedIds.Remove(item.Id))
        _selected.RemoveAll(x => x.Id == item.Id);
    else
    {
        _selected.Add(item);
        _selectedIds.Add(item.Id);
    }
}
```
参数注入时同步初始化 HashSet；单选模式在 `Add` 前先 `Clear` 两个结构。

### 3. 错误消息不要泄漏 `{ex.Message}`

**反模式**：`Snackbar.Add($"操作失败: {ex.Message}", Severity.Error)` —— 把原始异常消息（可能含内部路径、SQL 片段、堆栈信息）直接吐给用户。既不友好也是信息泄漏。

**修复模板**：
```csharp
catch (Exception ex)
{
    Log.Error(ex, "操作失败 Key={Key} Context={@Context}", key, context);
    Snackbar.Add("操作失败，请稍后重试。", Severity.Error);
}
```
- **用户侧**：固定的友好文案（常量字符串，不插值异常）
- **日志侧**：Serilog 结构化字段（`{PropertyName}` 或 `{@Object}` 序列化）
- **永远不**把 `ex.Message` 拼进用户可见的字符串

### 4. 样式禁止写在 `.razor` 里

**反模式**：
- `.razor` 文件底部写 `<style>...</style>` 块
- 多属性 `Style="cursor: pointer; min-width: 140px; border: 1px solid ...; transition: ..."`

**修复模板**：
- 所有样式放 `wwwroot/css/components/<module>.css`
- 类名带模块前缀 + BEM：`.creator-card` / `.creator-card--selected`
- 在 `App.razor` 注册 `<link>`
- 单属性 inline style（如 `style="flex: 1"`）是**可以接受**的特例，但能用工具类（`flex-grow-1`）就用
- 所有值走 `var(--spacing-*)` / `var(--radius-*)` / `var(--mud-palette-*)` token

**现有目录 → CSS 文件对应**：
| 目录 | CSS 文件 |
| --- | --- |
| `Components/Common/` | `common.css` |
| `Components/Creators/` | `creators.css` |
| `Components/Medias/` | `medias.css` |
| `Components/FileExplorer/` | `file-explorer.css` |

新模块按同样命名新建并在 `App.razor` 注册。

### 5. `Color.Error` / `Color.Warning` 只用于错误和警告

**反模式**：
- 用 `Color.Warning` 给文件夹图标着色（"橙色醒目"）
- 用 `Color.Error` 给 PDF 或 EXE 图标着色（"红色显眼"）
- 用 `Color.Warning` 给评分星标着色

**为什么错**：`Color.Error/Warning` 是 MudBlazor 的**语义状态色**——屏幕阅读器、高对比度模式、主题切换时都会当作"错误/警告"理解，让 UI 看起来永远处于错误态。

**修复模板**：可视区分用非语义色槽 `Primary` / `Secondary` / `Tertiary` / `Info` / `Success`，`Default` 作为中性回退。Error/Warning **只在真正的错误/警告状态下使用**（例如"权限拒绝"提示图标）。

### 6. 多选"确定"按钮的 0 选陷阱

**反模式**：多选 Dialog 的确定按钮固定文案"确定"，但 0 选时点击会返回空列表 = 静默**清空**关联。用户以为没选就"点确定没事"，实际破坏了数据。

**修复模板**：
```csharp
private string GetConfirmLabel()
{
    if (!AllowMultiSelect) return "确定";
    var count = _selected.Count;
    return count == 0 ? "清空并确定" : $"确定（{count} 项）";
}
```
或者在 0 选时 `Disabled="true"`——二选一，**不要**文案和 Disabled 都不做。

### 7. `hover-only` 功能必须有触屏 / 键盘路径

**反模式**：
```css
.card-overlay { opacity: 0; }
.card:hover .card-overlay { opacity: 1; }
```
触屏设备没有 hover，键盘 focus 也不触发 hover——overlay 里的关键按钮（收藏、删除、分类 chip）在触屏/键盘上**完全不可见**。

**修复模板**：补 `@media (hover: none)` 和 `:focus-within` 两条规则：
```css
/* 原规则保留 */
.card-overlay { opacity: 0; transition: opacity 0.2s; }
.card:hover .card-overlay { opacity: 1; }

/* 触屏下常驻显示 */
@media (hover: none) {
    .card-overlay { opacity: 1; }
    /* 顺手禁用触屏的 hover 位移，避免 tap-and-stick 粘滞 */
    .card:hover { transform: translateZ(0); }
}

/* 键盘 focus 也显示 */
.card:focus-within .card-overlay { opacity: 1; }
```

### 8. 小图标按钮的 44×44 触目标（WCAG 2.5.5）

**反模式**：`MudIconButton Size="Size.Small"` 约 32×32px，低于 WCAG 2.5.5 推荐的 44×44。

**修复模板**：用 `::before` 伪元素构造隐形 hit area，视觉尺寸保持不变：
```css
@media (pointer: coarse) {
    .my-small-btn {
        position: relative;
    }
    .my-small-btn::before {
        content: "";
        position: absolute;
        top: 50%;
        left: 50%;
        width: 44px;
        height: 44px;
        transform: translate(-50%, -50%);
    }
}
```
相比直接放大按钮尺寸的好处：
- 视觉不变，工具栏/表单行不会被撑高
- 零布局影响
- 不和 MudBlazor 内部尺寸规则打架

### 9. 其他易踩的小坑

- **`.ToLower()` → `.ToLowerInvariant()`**：文件扩展名、MIME 类型等技术字符串用 Invariant 版本，避免土耳其语 locale 的 `I` / `ı` bug
- **`@code` 块不要写在 `.razor` 里**：逻辑放 `.razor.cs`，razor 只负责结构
- **`[Parameter]` 必填项加 `[EditorRequired]`**：开发时 IDE 会提示
- **`CascadingParameter` 非 nullable 要加 `= null!`**：避免编译警告
- **`_recent` / 任何用户数据禁止用 `static` 字段**：Blazor Server 下 `static` 是**跨用户共享**——会造成隐私泄漏。用实例字段或 `ProtectedLocalStorage`
- **同步文件 I/O 必须包 `await Task.Run(...)`**：Blazor Server 的 SignalR hub 线程不能阻塞，否则**整个 circuit 冻结**
- **`MudChip` / `MudText` 等标签不要硬编码 `font-size: 10px`**：用 `var(--font-size-xs, 0.75rem)` 保证至少 12px，移动端可读
- **XML doc 默认不写**：除非是解释**WHY**（隐藏约束、非显然规则、未来坑），不要写 `// 加载数据` / `/// 切换选中状态` 这种方法名翻译
- **字段初始化默认值省略**：`private bool _x = false` → `private bool _x`；`List<T> _list = new()` 保留（初始化构造是必要的）

### 审计应用顺序

若对新模块做系统化审计/修复，按以下命令顺序可以最大化复用模板：

1. **`/harden`** — Critical + A11y + 表单约束 + 错误处理
2. **`/optimize`** — HashSet、分页、async I/O
3. **`/normalize`** — 样式迁出、Color 语义、razor/razor.cs 分离
4. **`/clarify`** — 用户文案、空态、0 选陷阱
5. **`/adapt`** — 响应式、触屏
6. **`/distill`** — 死代码、XML doc 噪音
7. **`/polish`** — 代码风格收尾

## 组件开发规范

### 可复用组件设计
```html
<!-- Components/Common/StatusBadge.razor -->
<MudChip T="string"
         Size="@Size"
         Color="@GetStatusColor(Status)"
         Variant="Variant.Filled"
         Class="@($"status-badge status-{Status.ToString().ToLower()}")">
    @GetStatusText(Status)
</MudChip>

@code {
    [Parameter] public StatusType Status { get; set; }
    [Parameter] public Size Size { get; set; } = Size.Medium;

    private Color GetStatusColor(StatusType status) => status switch
    {
        StatusType.Active => Color.Success,
        StatusType.Inactive => Color.Secondary,
        StatusType.Error => Color.Error,
        _ => Color.Default
    };

    private string GetStatusText(StatusType status) => status switch
    {
        StatusType.Active => "活跃",
        StatusType.Inactive => "非活跃",
        StatusType.Error => "错误",
        _ => "未知"
    };
}
```

### 组件使用示例
```html
<!-- 在页面中使用 -->
<StatusBadge Status="StatusType.Active" Size="Size.Small" />
```

## 主题定制

### 自定义 MudBlazor 主题
```csharp
// Program.cs 或 Startup.cs
services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});
```

### 自定义颜色主题
```css
:root {
    /* 自定义主色调 */
    --mud-palette-primary: #1976d2;
    --mud-palette-primary-rgb: 25, 118, 210;
    --mud-palette-primary-lighten: #42a5f5;
    --mud-palette-primary-darken: #1565c0;

    /* 自定义成功色 */
    --mud-palette-success: #4caf50;
    --mud-palette-success-rgb: 76, 175, 80;

    /* 自定义警告色 */
    --mud-palette-warning: #ff9800;
    --mud-palette-warning-rgb: 255, 152, 0;
}
```

## 测试和调试

### 样式调试技巧
1. **浏览器开发者工具** - 实时调试 CSS 样式
2. **响应式测试** - 测试不同屏幕尺寸下的表现
3. **性能分析** - 使用 Performance 面板分析动画性能
4. **可访问性测试** - 使用 Lighthouse 检查可访问性

### 常见问题解决
1. **样式不生效** - 检查 CSS 选择器优先级
2. **动画卡顿** - 使用 transform 替代 position 变化
3. **响应式问题** - 检查断点设置和媒体查询
4. **组件样式冲突** - 使用更具体的选择器或 CSS Modules

这个设计系统确保了 NineKgTools 在视觉一致性、用户体验和性能表现方面的高标准，为用户提供现代化、流畅的界面体验。通过遵循这些规范，开发团队可以高效地创建一致、美观、易维护的用户界面。
