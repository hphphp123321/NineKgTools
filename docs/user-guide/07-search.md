# 07. 全局搜索（`/search`）

## 这个页面是干啥的？

`/search` 是 NineKgTools 的**统一搜索入口**——一次查询同时搜 媒体 / 标签 / 创作者 / 社团 四种类型。

![搜索](../assets/screenshots/search.png)

支持两种搜索模式：

- **关键词搜索**（默认）：基于标题 / 描述 / 标签名的全文匹配
- **语义搜索**：基于 OpenAI Embedding 向量的相似度搜索

## 主要操作

### 我想找一个媒体

输入查询词 → 回车或点搜索按钮。结果分四个 chip 类型显示：

- **媒体 (N)**：标题 / 简介 / 标签匹配的 Media 条目
- **标签 (N)**：名称 / 描述匹配的 Tag
- **创作者 (N)**：名字匹配的 Creator
- **社团 (N)**：名字匹配的 Circle

点 chip 切换显示该类型的结果列表。

### 我想用语义搜索

页面顶部有 **AI 语义搜索 开关** → 打开后查询会**先把 query 转成 embedding 向量**再做余弦相似度匹配。

**举例**：

- 关键词搜 "黄昏 海边 独白" → 命中标题/描述里有这些字的作品
- 语义搜 "黄昏 海边 独白" → 命中**主题氛围接近**的作品（即使标题里没这几个字）

两种模式各擅其长：
- 已知具体作品名 / ID → 关键词更快
- 模糊回忆 / 找氛围 → 语义更准

### 我想限定只搜某类（比如只搜媒体）

页面顶部的"类型筛选" chip：勾选你要的类型，其他不查。能省时间（语义搜索每个类型都得跑一次向量比对）。

### 我想从顶部 AppBar 快速搜

每个页面顶部 AppBar 的 **`GlobalSearchBox`** 都能输入查询词 → 回车跳到 `/search?q=你的词`。

支持的快速操作：

- `Esc` 清空
- `Enter` 跳转 `/search?q=...`

> v1.1 计划支持 `/` 快捷键聚焦搜索框（类似 GitHub）。

## 进阶用法

<details>
<summary>URL 查询参数</summary>

```
/search?q=示例                        # 默认关键词搜
/search?q=黄昏海边&semantic=true       # 启用语义搜索
/search?q=ASMR&types=media,tag         # 只搜媒体和标签
```

</details>

<details>
<summary>搜索缓存</summary>

`config.yaml` → `search.enable_search_cache = true`：相同 query 的结果会缓存 5 分钟（`cache_expiration_minutes`）。

这避免短时间内连续按回车导致的重复 OpenAI API 调用（语义搜索每次都要嵌入向量，有费用）。

如果你**改了媒体或标签后立即搜**结果还是旧的——等 5 分钟，或重启 dotnet 清缓存。

</details>

<details>
<summary>语义搜索的相似度阈值</summary>

`config.yaml` → `search.default_min_relevance_score`（默认 0.3）：低于这个相似度的结果不展示。

如果你觉得**搜出来的东西太离题**，调高到 0.5；
如果**没结果**，调低到 0.15 看看模型觉得什么有相关性。

媒体与标签向量化分别有独立的 enable 开关：

```yaml
ai:
  vector:
    enable: true               # 总开关
    media:
      enable: true             # 媒体向量化
    tag:
      enable: true             # 标签向量化
```

详见 [config-reference.md](../reference/config.md)。

</details>

<details>
<summary>关键词高亮</summary>

关键词搜的结果列表里，匹配到的字串会自动**高亮包裹 `<mark>` 标签**（视觉上是黄色背景）。

`config.yaml` → `search.text_search.enable_highlighting` 控制是否启用，`highlight_tag` 控制用啥标签（默认 `<mark>`）。

</details>

## 跟其他页面的关系

```
任何页面 AppBar GlobalSearchBox 输入 → /search?q=...
                                          ↓
                                  搜索结果（4 类 chip）
                                          ├─ 点媒体 → /media/{id}
                                          ├─ 点标签 → /tag/{id}
                                          ├─ 点创作者 → /creators?id=...
                                          └─ 点社团 → /circles?id=...
```

## 常见问题

### Q：语义搜索一直返回 0 个结果

可能性：

1. `ai.vector.media.enable = false` → 媒体向量库为空
2. 媒体库刚加新条目，向量同步任务还没跑（`MediaVectorSync` 每 6 小时跑一次） → 进 `/tasks/scheduled` 手动触发一次
3. `default_min_relevance_score` 太高 → 调到 0.15 试试

### Q：关键词搜不到我刚加的媒体

数据库 / 缓存差异：

1. `enable_search_cache = true` 时，先等 5 分钟或重启
2. 你的查询词跟标题不完全一致 → 试只搜核心几个字

### Q：搜索结果按什么排序

- 关键词模式：按相关性（标题 > 简介 > 标签 命中权重不同）
- 语义模式：按余弦相似度从高到低

均不支持自定义排序（v1.0），v1.1 计划加"按时间 / 评分"次级排序选项。

### Q：每种类型的最大结果数

`config.yaml` → `search.default_max_results_per_type`（默认 20）。

如果一次性想看更多 → 调大；但语义搜索每多 10 条多约 100ms。

### Q：搜索过程中能取消吗

`config.yaml` → `search.search_timeout_seconds`（默认 30）控制单次搜索最长跑多久。超时自动放弃。

UI 上没有"取消按钮"——但你可以重新输入新的查询词，前一次会被丢弃。
