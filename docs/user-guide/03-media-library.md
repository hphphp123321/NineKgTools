# 03. 媒体库（`/media/overview`）

## 这个页面是干啥的？

媒体库是 NineKgTools 的**核心展示页**——你所有已入库的媒体都在这里浏览、筛选、排序、跳到详情。

![媒体库](../assets/screenshots/overview.png)

页面顶部是 6 个"分类卡片"，分别显示当前每类有多少条；点击卡片就把列表过滤到这一类。

## 主要操作

### 我想浏览全部媒体 / 切换分类

- 默认进来是"全部"分类，所有已入库媒体一锅端
- 点顶部的 **🎵 音频 / 🎬 视频 / 🎮 游戏 / 🖼️ 图片 / 📝 文本** 任一卡片 → 立即切到对应分类
- 切换有 150ms 淡出动画 → 内容挂载后淡入，不会闪屏

URL 直达：

```
/media/overview              # 全部
/media/overview/audio        # 音频
/media/overview/video
/media/overview/game
/media/overview/picture
/media/overview/text
```

### 我想筛选：按标签 / 收藏夹 / 评分 / 日期

页面右上角的 **筛选** 按钮 → 弹出 `MediaFilterDialog`：

- **标签**：多选下拉，AND 关系（必须全部满足）
- **收藏夹**：多选下拉，OR 关系（在任一收藏夹里）
- **评分范围**：下界 + 上界
- **创建日期范围**：起 + 止
- **顶级分类**：默认跟当前分类一致，可以改

点"应用"立即生效；筛选条件持久到 URL 查询参数（可分享 / 收藏链接）。

### 我想排序

筛选栏旁有"排序"下拉：
- 创建时间（默认，新→旧）
- 评分（高→低 / 低→高）
- 标题（A-Z / Z-A）
- 发布日期

### 我想换个视图风格

**网格视图**（默认）：4-6 列封面卡片，悬停显示标题
**列表视图**：单列长条，每行：封面缩略图 + 标题 + 评分 + 标签 chips

切换在筛选栏右侧的图标按钮。

### 我想新建一个媒体（识别源搜不到的冷门资源）

页面右上角 **新建媒体** 按钮（绿色 PostAdd 图标）：

1. 弹出 `MediaKindPickerDialog` 双卡片：**文件夹** vs **单文件**
2. 选完弹出 `FileExplorer` 让你选路径
3. 选完弹 `ManualAddMediaDialog`：
   - 必填：标题 + 顶级分类
   - 选填手风琴：具体分类、简介、评分
4. 点"添加"
5. 跳转到新创建的 `/media/{id}`：
   - 只填了必填两项 → 进编辑态（`?edit=true`），让你继续填详情
   - 展开手风琴填了任一选填 → 直接进只读态

详细见 [13 工作流 - 流程二](13-workflows.md#流程二批量手动添加)。

### 我想点进单条媒体看详情

点封面 → 跳转 `/media/{id}`。详情页能干：

- 看完整字段（标题、简介、标签、创作者、评分、发布日期）
- 看封面 / 截图（多张）
- 编辑（点右上 ✏️）
- 跳转到关联的"媒体源"（`/source/{sourceId}`）
- 加入 / 移除收藏夹
- 删除整条媒体（含 source 关联 + 图片 + 向量）

> v1.0 没有专门的"详情页文档"，因为详情页主要是字段展示——所有字段定义见 [`docs/config-reference.md`](../reference/config.md) 或 `MediaBase.cs`。

### 我想批量删除 / 加收藏

列表行的左侧 checkbox 多选 → 顶部出现批量操作工具栏：
- **批量加收藏**
- **批量删除**（弹 `NineKgConfirmDialog DestructiveBatch`，必须看一眼数量再确认）

## 进阶用法

<details>
<summary>URL 查询参数深链</summary>

```
/media/overview/game?tags=1,5,9&minRating=4&sort=rating-desc
```

支持的参数：
- `tags=1,2,3` 逗号分隔标签 ID
- `favorites=2` 收藏夹 ID
- `minRating=3` / `maxRating=5`
- `from=2025-01-01` / `to=2025-12-31`
- `sort=created-desc | rating-desc | title-asc | ...`

参数会**与页面筛选 UI 双向同步**——改 UI URL 自动更新，反之亦然。

</details>

<details>
<summary>按页大小（每页几条）</summary>

页面底部分页器旁边有"每页 X 条"下拉。选择会保存到 localStorage（`mediaShownView.pageSize` 之类的 key），下次进来记住。

</details>

<details>
<summary>键盘快捷键</summary>

v1.0 没全局快捷键，但筛选对话框内：
- `Esc` 关闭
- `Enter` 应用筛选

详情页内：
- `Esc` 返回列表

更多键盘可达性是 v1.1 工作（参考 [`frontend-design-guide.md`](../development/frontend-design.md) "组件开发反模式 Checklist"）。

</details>

## 跟其他页面的关系

```
/                          ← 首页（"最近添加"卡片直接跳进 /media/{id}）
   ↓
/media/overview            ← 当前页
   ├─ 点封面 → /media/{id}（详情）
   │           ├─ "媒体源"链接 → /source/{id}（详见 05）
   │           ├─ 加收藏 → 关联 /favorites
   │           └─ 标签 chip 点击 → /tag/{id}
   │
   └─ "新建媒体"按钮 → ManualAddMediaDialog → /media/{newId}?edit=true
```

## 常见问题

### Q：分类卡片显示数字 0 但我明明添加过媒体

可能数据库里的 Media 记录被识别但**没正确归到某个 TopCategory**。检查 `/media/overview/全部` 看看有没有；有的话点进去看 `Category` 字段是不是 `Unknown`。

### Q：筛选条件清不掉

筛选对话框里有"重置"按钮；或者直接改 URL 把 query 参数删掉。

### Q：媒体的封面没显示

- 看 `Database/.cache/` 是否被清了（启动时会清，是 [设计](../../CLAUDE.md#数据库初始化programcs)）
- 或者识别时图片下载失败 → 进 `/media/{id}` 看图片字段是否为空 → 进 `/source/{id}` 重新识别一次（详见 [流程三](13-workflows.md#流程三重新识别--替换)）

### Q：能不能多选媒体批量改标签

v1.0 没批量编辑，只有批量删除/加收藏。批量改标签是 v1.1 计划。

### Q：媒体库太大滚动卡

v1.0 用的是分页（默认每页 20-40 条），不是虚拟列表。如果你有几万条媒体，每页加大到 60-80 也仍流畅。真的卡了切到列表视图（封面缩略图小，渲染轻）。
