# CSS 架构文档

本项目采用现代化的CSS组织架构，遵循模块化、可维护性和可扩展性原则。

## 📁 文件结构

```
wwwroot/css/
├── variables.css          # CSS变量系统 - 设计令牌
├── utilities.css          # 工具类 - 原子化CSS
├── global.css            # 全局基础样式
├── components/           # 组件级样式
│   ├── cards.css        # 卡片组件样式
│   ├── animations.css   # 动画效果样式
│   └── forms.css        # 表单组件样式
├── pages/               # 页面级样式
│   └── media.css        # 媒体页面专用样式
└── README.md           # 本文档
```

## 🎨 设计系统

### CSS变量系统 (variables.css)
定义了项目中使用的所有设计令牌：

- **间距系统**: `--spacing-xs` 到 `--spacing-4xl`
- **圆角系统**: `--radius-xs` 到 `--radius-full`
- **阴影系统**: `--shadow-xs` 到 `--shadow-2xl`
- **发光效果**: `--glow-primary`, `--glow-success` 等
- **过渡动画**: `--transition-fast` 到 `--transition-slower`
- **字体系统**: 字号、字重、行高
- **颜色系统**: 基于MudBlazor主题的颜色变量

### 工具类系统 (utilities.css)
提供原子化CSS类，可直接在HTML中使用：

```html
<!-- 间距 -->
<div class="p-lg m-md">内容</div>

<!-- 阴影 -->
<div class="shadow-md hover-lift">卡片</div>

<!-- 字体 -->
<span class="text-lg font-semibold">标题</span>

<!-- 布局 -->
<div class="flex items-center justify-between">布局</div>
```

## 🧩 组件样式

### 卡片组件 (components/cards.css)
统一的卡片设计系统：

```html
<!-- 基础卡片 -->
<div class="card-base">内容</div>

<!-- 带边框的卡片 -->
<div class="card-base card-bordered-primary">主色边框</div>

<!-- 渐变背景卡片 -->
<div class="card-base card-gradient-info">信息卡片</div>

<!-- 媒体专用卡片 -->
<div class="card-base card-media-main">媒体主卡片</div>
```

#### 可用的卡片变体：
- `card-base` - 基础卡片样式
- `card-bordered-{color}` - 左边框卡片（primary, secondary, success, warning, info, error, tertiary）
- `card-gradient-{color}` - 渐变背景卡片
- `card-media-{type}` - 媒体页面专用卡片
- `card-creator-{type}` - 创建者页面专用卡片
- `card-hover-{effect}` - 悬停效果（lift, scale, glow-{color}）

### 动画组件 (components/animations.css)
统一的动画效果系统：

```html
<!-- 进场动画 -->
<div class="animate-fade-in">淡入动画</div>
<div class="animate-scale-in">缩放进入</div>
<div class="animate-slide-in-left">从左滑入</div>

<!-- 悬停效果 -->
<div class="hover-lift">悬停上升</div>
<div class="hover-scale">悬停放大</div>
<div class="hover-zoom">图片缩放</div>

<!-- 特殊动画 -->
<div class="animate-pulse">脉动效果</div>
<div class="animate-bounce">弹跳效果</div>
<div class="animate-spin">旋转效果</div>
```

### 表单组件 (components/forms.css)
表单相关的样式：

```html
<!-- 容器样式 -->
<div class="container-description">描述容器</div>
<div class="alias-section">别名部分</div>

<!-- 标签样式 -->
<div class="tag-info-section">标签信息区域</div>
<div class="related-media-section">相关媒体区域</div>
```

### 媒体网格 (components/media-grid.css)
统一的媒体卡片网格布局样式：

```html
<!-- 媒体网格容器 -->
<div class="media-card-grid">媒体卡片网格</div>

<!-- 空状态 -->
<div class="media-card-grid-empty">暂无媒体</div>
```

### 媒体列表 (components/media-list.css)
统一的媒体列表布局样式：

```html
<!-- 媒体列表容器 -->
<div class="media-list">媒体列表</div>

<!-- 简化列表项 -->
<div class="media-list-item">简化列表项</div>

<!-- 完整列表项 -->
<div class="media-list-item-full">完整列表项</div>

<!-- 空状态 -->
<div class="media-list-empty">暂无媒体</div>
```

## 📄 页面样式

### 媒体页面 (pages/media.css)
专门用于媒体详情页面的样式，包含：
- 媒体标题动画效果
- 标签容器交互效果
- 海报图片悬停效果
- 创建者和社团链接样式
- 平台图标样式
- 描述内容排版

## 🚀 使用指南

### 1. 优先级原则
按以下优先级使用样式：

1. **工具类** - 优先使用原子化CSS类
2. **组件类** - 使用预定义的组件样式
3. **自定义样式** - 只在必要时添加页面级样式

### 2. 命名规范
- 使用BEM命名法的变体
- 组件样式：`component-variant-modifier`
- 工具类：描述性名称，如 `shadow-lg`, `text-center`
- 页面样式：`page-specific-element`

### 3. 响应式设计
所有组件都包含响应式设计：

```css
/* 移动端优先 */
.card-base {
    padding: var(--spacing-md);
}

@media (max-width: 768px) {
    .card-base {
        padding: var(--spacing-sm);
    }
}
```

### 4. 暗色模式支持
通过MudBlazor的CSS变量自动支持暗色模式，无需额外配置。

## 🔧 维护指南

### 添加新样式
1. **工具类**: 添加到 `utilities.css`
2. **组件样式**: 创建或扩展 `components/` 下的文件
3. **页面特定样式**: 在 `pages/` 下创建对应文件
4. **设计令牌**: 添加到 `variables.css`

### 迁移现有样式
1. 识别重复的样式模式
2. 提取到适当的组件文件
3. 更新HTML使用新的CSS类
4. 删除内联样式

### 性能优化
- 使用CSS变量减少重复值
- 合理组织文件结构，按需加载
- 利用工具类减少CSS文件大小

## 📚 参考资源

- [CSS自定义属性](https://developer.mozilla.org/zh-CN/docs/Web/CSS/Using_CSS_custom_properties)
- [原子化CSS设计](https://tailwindcss.com/docs/utility-first)
- [BEM命名方法论](http://getbem.com/)
- [MudBlazor主题系统](https://mudblazor.com/customization/default-theme)

---

这个CSS架构旨在提供一个可维护、可扩展且性能优异的样式系统。遵循这些规范将确保代码的一致性和团队协作的效率。 