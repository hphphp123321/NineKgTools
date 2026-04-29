# 11. 识别源配置（`/website`）

## 这个页面是干啥的？

`/website` 是配置**三大识别源**（DLsite / Bangumi / Steam）的中央控制台——决定每种媒体类型先试哪个站、每个站用什么 key、是否启用。

![识别源](../assets/screenshots/website.png)

## 主要操作

### 我想看每个站启用 / 禁用状态

页面顶部"网站列表"区域三张卡片，每卡显示：

- 站点 logo
- 名称（DLsite / Steam / Bangumi）
- "已启用" / "已禁用" chip
- 一句话简介
- 支持的媒体类型（chips：视频 / 音频 / 游戏 / 图片 / 文本）
- 启用开关
- 设置齿轮按钮（点开弹该站的配置对话框）

### 我想配 Bangumi 的 API Key

DLsite 和 Steam **不需要 key**（开放接口或 HTML 爬取）。**只有 Bangumi** 需要：

1. 去 [https://next.bgm.tv/demo/access-token](https://next.bgm.tv/demo/access-token) 申请（免费，要 bgm.tv 账号）
2. 复制 access token
3. 进 `/website` → Bangumi 卡的设置齿轮 → 粘贴到 `api_key` 字段
4. 保存

> 没填 key Bangumi 识别会全部 401 失败。在 `/source/pending` 待识别 Tab 看识别诊断会发现 Bangumi 全跳过。

### 我想调整某种媒体类型的识别源优先级

页面下半部分 **"网站优先级设置"**：

每种媒体类型一行：

```
🎬 视频:    [DLsite] [Bangumi]
🎵 音频:    [DLsite]
🎮 游戏:    [DLsite] [Steam] [Bangumi]
🖼️ 图片:    [DLsite] [Bangumi]
📝 文本:    [Bangumi]
❓ 未知:    [DLsite] [Bangumi] [Steam]
```

每个 chip **可以拖拽改顺序**——左边的优先试。

右下角还有个 **"可拖动的网站"** 提示区，列出所有可用站点；拖到某行加进去。

> 拖拽后**点页面顶部"保存配置"才生效**。不点保存关掉页面就丢。

### 我想新增一个识别源

v1.0 没有 UI 加新识别源——是**代码集成**：实现 `IWebsite` 接口 + 注册 DI。详见 [`docs/architecture/media-identification-flow.md`](../architecture/media-identification-flow.md) 与项目根的 [`CLAUDE.md` § 识别网站](../../CLAUDE.md#识别网站iwebsite-实现)。

加完代码后这个页面会自动出现新站的卡片。

### 我想看某个站的配置细节

点站点卡的设置齿轮 → 弹该站专属对话框：

| 站点 | 主要字段 |
|---|---|
| DLsite | `enable`、`use_selenium_for_rating`（是否用 Selenium 抓评分） |
| Bangumi | `enable`、`api_key` |
| Steam | `enable`、`language`（schinese/english/japanese）、`country_code`（**禁用 cn**，部分游戏对 CN 区屏蔽，推荐 us） |

## 进阶用法

<details>
<summary>DLsite 的 use_selenium_for_rating</summary>

DLsite 的评分**不在初始 HTML 里**——是 Vue 异步加载的。如果你需要带评分的识别结果，必须开 Selenium：

```yaml
website:
  dlsite:
    use_selenium_for_rating: true
```

但开了之后 Selenium 会在每次识别时启动 Chrome → 慢 + 有时报错。

**Docker 部署默认不带 Chromium**（镜像考量）→ Selenium 在容器里启不起来。所以容器部署**不要开**这个开关。详见 [README - 关于 Selenium](../../README.md)。

</details>

<details>
<summary>Steam country_code 的坑</summary>

部分日厂游戏 Steam 页对**CN 区屏蔽**——`country_code: cn` 会让 Steam Storefront 接口返回 `success: false`。

**推荐 `country_code: us`**——日厂游戏对美区基本都开放。

副作用：价格/区域信息会显示美元——但识别只关心标题/简介/标签，不关心价格，所以无所谓。

</details>

<details>
<summary>Bangumi API 限流</summary>

Bangumi 公开 API 没有官方限流文档，但密集请求容易被临时封 IP。批量识别 100+ 条媒体时建议：

1. `tasks.max_concurrent_identification_tasks` 调小到 3-5（默认 5-8）
2. 中间穿插非 Bangumi 优先的媒体类型分散负载

</details>

<details>
<summary>识别源在哪起作用</summary>

`WebsiteService.GetMediaInfoAsync` 按当前媒体类型对应的 priority 列表逐个试：

```
[DLsite] → 试，相似度 0.85 ≥ 0.8（min_similarity）→ 选中，结束
                ↓ 失败/不匹配
[Bangumi] → 试，无结果 → 继续
                ↓
[Steam] → 试，相似度 0.75 < 0.8 → 跳过，继续
                ↓
全部尝试完 → 进 /source/pending 待识别 Tab
```

详细诊断快照在每条识别任务的"识别诊断"Tab（[08 任务](08-tasks.md)）。

</details>

## 跟其他页面的关系

```
/website                       ← 你在这
   ├─ 站点卡设置齿轮 → 改 api_key / 启用开关 → 保存
   ├─ 优先级 chip 拖拽 → 保存（影响全局识别行为）
   └─ /sources / /source/pending 的所有识别走这里的配置

/settings → 识别 Tab     ← 跟 /website 不重叠：那里是 min_similarity / strategy / auto_add 等"识别策略"配置
```

## 常见问题

### Q：刚改了优先级，识别还是按旧顺序

没保存？看页面顶部右上角 **"保存配置"** 按钮——必须点它才生效。

### Q：DLsite 一直识别失败

看 `/source/pending` 待识别 Tab → 识别诊断 → 看具体哪一站、什么错误：

- **NoMatch**：搜不到。可能是文件名跟 DLsite 上的标题差太多。试改文件名加 RJ 号 → 重新识别
- **Skipped**：被规则跳过（比如分类不支持）
- **Exception**：网络 / DLsite 改版导致解析挂。看 `Logs/`

### Q：禁用 DLsite 后识别全失败

如果禁用了主力识别源，剩下的（Bangumi 没 key / Steam 仅游戏类）覆盖面会很窄。

要么补 Bangumi key，要么不要禁用 DLsite。

### Q：能用国内站点（百度网盘 / 哔哩哔哩漫画 / etc）作为识别源吗

v1.0 没有内置，但任何能解析出标题/简介/标签/封面的页面都能加——实现 `IWebsite` 即可。详见 [`CLAUDE.md` - 新增识别源的标准步骤](../../CLAUDE.md#新增识别源的标准步骤)。

> ⚠️ 接入识别源前请确保**遵守目标站点的 robots.txt 和服务条款**——不要写出 DDoS 级别的爬取，不要绕过登录墙抓付费内容。
