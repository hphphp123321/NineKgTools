# 01. 入门：登录后做什么

## 这个页面是干啥的？

`http://localhost:23333/` 是 NineKgTools 的**首页 / 仪表盘**——汇总媒体库统计、最近添加、随机发现的封面墙。第一次启动看到的就是这里（默认 logged-in 跳转）。

![首页](../assets/screenshots/home.png)

## 第一次进来的 5 件事

### 1. 用默认账号登录

```
URL:      http://localhost:23333
账号:     admin
密码:     admin
```

> 想改默认账号？设环境变量 `NT_USER` / `NT_PASSWORD` 后让 dotnet 重启**且数据库为空**（`NINEKG_RESET_DB=true` 一次）。详见 [DEPLOYMENT.md](../operations/deployment.md#环境变量参考)。

### 2. 检查 OpenAI Key 是否就位

进 **Settings 页 → AI 配置 Tab**，看 `api_key`：
- 已填 → 测试连接按钮点一下，绿色 OK 就行
- 留空 → 后台会从 `OPENAI_API_KEY` 环境变量读；同样测试一下

如果测试报错最常见两种：
- **401** → key 错了或被吊销
- **timeout** → `base_domain` 连不通，换代理或确认网络

> AI 不是必须的——禁掉 `ai.use_ai` 也能用 DLsite/Bangumi/Steam 三大识别源。但**向量语义搜索**会失效。

### 3. 看一眼主菜单

左侧导航栏（`MudNavLink`），从上到下：

| 入口 | 路由 | 这是什么 | 详细文档 |
|---|---|---|---|
| 总览 | `/` | 现在这里 | 本文 |
| 本地搜索 | `/search` | 全局搜索（关键词 + 语义） | [07 搜索](07-search.md) |
| 媒体库 | `/media/overview` | 已入库媒体的浏览 | [03 媒体库](03-media-library.md) |
| 收藏夹 | `/favorites` | 把媒体丢到不同的收藏夹分组 | [10 收藏夹](10-favorites.md) |
| 媒体源管理 | `/sources` | 你硬盘上的文件夹与文件清单 | [05 媒体源](05-sources.md) |
| └ 待处理 | `/source/pending` | 待识别 + 待入库双 Tab | [04 待处理](04-pending.md) |
| 任务 | `/tasks` | 后台 / 定时 / 历史 | [08 任务](08-tasks.md) |
| └ 后台/定时/历史 | `/tasks/...` | 三个子页面 | 同上 |
| 站点管理 | `/website` | DLsite/Bangumi/Steam 配置 | [11 识别源](11-website.md) |
| 标签管理 | `/tags` | 标签列表与映射 | [06 标签](06-tags.md) |
| 创作者 | `/creators` | 创作者实体 | [09 创作者 & 社团](09-creators-circles.md) |
| 社团 | `/circles` | 社团实体 | 同上 |
| 设置 | `/settings` | 7 个 tab 系统配置 | [12 设置](12-settings.md) |

### 4. 确认监视文件夹（可选）

如果你想**让 NineKgTools 自动盯着某个目录**（拉了新东西就识别），现在就配：

进 `Settings → 媒体源 Tab` → `watch_folders` 加路径 → 保存。

> Docker 部署：路径要写**容器内的路径**（默认 `/app/media`），主机上的目录靠 `docker-compose.yml` 的 volume 挂载。

之后任何放进 watch_folders 的文件夹会被自动扫描 → 识别 → 入库。详见 [13 工作流 - 流程一](13-workflows.md#流程一监视文件夹全自动)。

### 5. 跑通"第一次识别"

最简单的验证：随便找一个 DLsite RJ 号文件夹（如 `D:\test\RJ01081508`）：

1. 进 `/source/pending` 点"扫描新文件"或者放进 watch_folders
2. 等几秒，自动识别完成（看 `/tasks` 实时进度）
3. 进 `/media/overview` 看到这条媒体出现在媒体库

完整流程见 [02 第一次导入媒体](02-first-import.md)。

## 进阶用法

<details>
<summary>键盘快捷键 / 深链 / URL hack</summary>

- 顶部搜索栏可点击或按 `/` 聚焦（v1.1 计划）
- `/source/pending?tab=unidentified` / `?tab=pending` 直达对应 Tab
- `/media/overview/{category}` 直达分类（`game` / `audio` / `video` / `picture` / `text`）
- `/media/{id}?edit=true` 直接打开媒体详情的编辑模式

</details>

<details>
<summary>页面右上角的几个图标都是干啥的</summary>

- 🔢 **任务 Badge**：当前活动任务数；点击弹出"快速预览"对话框（不跳页）
- 🌙 **主题**：循环切换 暗色 / 紫色 / 浅色
- ⋮ **更多菜单**：主题 / 退出登录

</details>

## 跟其他页面的关系

- 这是**所有用户的入口**——99% 的会话从这里开始
- 首页的"待处理"卡片直达 `/source/pending`
- 首页的"最近添加"封面点击进 `/media/{id}`

## 常见问题

### Q：登录后白屏 / 卡 loading

最常见原因是 **MudBlazor 的 SignalR circuit 没建立**。看浏览器 Console，如果有 `Failed to load resource: _framework/blazor.server.js`：
- Docker 部署：确认 ASPNETCORE_ENVIRONMENT 没设成 Production（影响 StaticWebAssets，但 v1.0 镜像已修）
- 本地：清浏览器缓存重试

### Q：admin/admin 登录失败

数据库被清过但环境变量 `NT_USER` / `NT_PASSWORD` 改了 → 用新密码登录。还不行：清库重建（`NINEKG_RESET_DB=true`）。

### Q：左侧菜单"媒体源管理"和"待处理"看着像两个东西

**确实是两个东西**：
- **媒体源管理 `/sources`**：所有你扫过的源（不管是否识别），看文件清单
- **待处理 `/source/pending`**：仅"还没识别完"或"已识别但没入库"的子集

详见 [05 媒体源](05-sources.md) vs [04 待处理](04-pending.md) 的对比。
