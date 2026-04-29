# FAQ — 常见问题

> 这是按用户高频问题归集的快查表。详细内容在各页面文档里。

## 安装与启动

### Q：admin/admin 登录失败

可能性：
1. **改过密码**：用新密码（或 `NT_USER` / `NT_PASSWORD` 环境变量的值）
2. **数据库被清后重置过**：`NT_USER` / `NT_PASSWORD` 设了新值导致默认 admin 不存在 → 用环境变量里的账号
3. **真的忘了**：清库重建（`NINEKG_RESET_DB=true` 一次启动）回到默认 admin/admin

### Q：浏览器打开 23333 端口白屏

排查顺序：

1. **后端没起来**：看 `dotnet run` / `docker logs ninekg-web` 输出有没有报错
2. **MudBlazor.min.css 404**：Production 模式 + StaticWebAssets 关了。改 `ASPNETCORE_ENVIRONMENT=Development`，详见 [DEPLOYMENT.md](../operations/deployment.md)
3. **SignalR 连不通**：反代没正确转 WebSocket，详见 [DEPLOYMENT.md - 反向代理](../operations/deployment.md#反向代理--https)

### Q：每次启动数据库就清了

`NINEKG_RESET_DB=true` 没改回 false。设为 `false`（或干脆删除该环境变量）。

---

## 配置

### Q：OpenAI 在国内连不通怎么办

`/settings → AI 配置 → base_domain` 改为兼容 OpenAI 协议的代理：

```
https://your-proxy.com/
```

详见 [12 设置](12-settings.md#我想配代理openai-在国内连不通)。

### Q：Bangumi key 在哪申请

[https://next.bgm.tv/demo/access-token](https://next.bgm.tv/demo/access-token)（免费，要 bgm.tv 账号）。

填到 `/website → Bangumi 卡 → 设置齿轮 → api_key`。

### Q：watch_folders 路径要写主机路径还是容器路径

**Docker 部署**：写**容器内的路径**（默认 `/app/media`），主机目录靠 `docker-compose.yml` 的 volume 挂载。

**dotnet run / Windows 便携版**：写本地绝对路径（如 `D:\Media`）。

详见 [DEPLOYMENT.md - 持久化目录](../operations/deployment.md#持久化目录)。

---

## 识别

### Q：DLsite 识别一直失败

1. 看 `/source/pending` 待识别 Tab → 识别诊断 Tab → 看具体哪一站、什么错误
2. 常见 NoMatch：文件名跟 DLsite 标题差太多 → 改文件名加 RJ 号
3. 常见 Exception：DLsite 改版导致 HTML 解析挂 → 等项目更新或开 issue

### Q：识别没结果但相似度还挺高

`config.yaml` → `identification.min_similarity`（默认 0.8）太严。改到 0.6 看看。

### Q：批量识别 100 条，一次只跑 5 条慢

设计上限制（`max_concurrent_identification_tasks` 默认 5）——避免触发识别源限流。

调大：`/settings → 任务 → max_concurrent_identification_tasks` → 10-15 → 重启 dotnet。

详见 [02 第一次导入媒体](02-first-import.md) 与 [08 任务系统](08-tasks.md)。

### Q：识别结果错了想重跑

进 `/source/{id}` → "重新识别"按钮 → 弹识别选项对话框（可临时调网站优先级）。

详见 [13 流程三 重新识别](13-workflows.md#流程三重新识别--替换)。

⚠️ 重新识别会**先删旧 Media**——你的自定义编辑（评分、收藏夹关联）会丢，谨慎使用。

---

## 标签

### Q：识别后多了好多看不懂的标签

DLsite/Bangumi 返回的原标签**没匹配到现有标签也没映射**时会**直接创建独立标签**。

清理方式见 [13 流程四 标签清理](13-workflows.md#流程四标签清理--映射)：

1. `/tags/mappings` 加映射规则
2. 手动合并历史媒体的旧标签到目标标签
3. 删孤儿标签

### Q：tags.yaml 是怎么来的能改吗

来源：从老版本 DLsite 网站爬取整理，含 R18 标签。**完全可以删改替换**——见 [`docs/tags-yaml-reference.md`](../reference/tags-yaml.md)。

⚠️ 改 yaml **只在数据库为空时生效**——已经启动过就要 `NINEKG_RESET_DB=true` 重灌（会丢所有数据）或走 UI 一个个改。

---

## 媒体管理

### Q：媒体库分类卡片显示数字 0 但我有数据

可能你的媒体被识别但 `Category` 是 `Unknown`。进 `/media/overview/全部` 看；筛选 `Category = Unknown` 找到这些媒体；进每个详情页改 `Category`。

### Q：能批量改媒体的标签 / 评分吗

v1.0 多选支持有限：批量删除 ✅、批量加收藏 ✅、批量改标签/评分 ❌（v1.1）。

### Q：删了媒体硬盘文件也删吗

**不会**。所有"删除"只删数据库记录。硬盘文件原封不动。

### Q：媒体图片不显示

可能：

1. `Database/.cache/` 启动时被清了（设计行为）
2. 图片下载失败 → `/media/{id}` 看图片字段是否为空 → `/source/{id}` 重新识别

---

## 搜索

### Q：语义搜索无结果

可能：

1. `ai.vector.media.enable = false` → 媒体向量库为空
2. 媒体刚加新条目，向量同步任务还没跑 → `/tasks/scheduled → MediaVectorSync → 立即触发`
3. `default_min_relevance_score` 太高 → 调到 0.15 试

详见 [07 搜索](07-search.md)。

### Q：搜不到刚加的媒体

`enable_search_cache = true` 时缓存 5 分钟。等 5 分钟或重启 dotnet。

---

## 任务

### Q：任务取消了还在跑

Hangfire 任务取消是协作式的——给信号后任务在下一个检查点才停。某些操作（HTTP 请求中、IO 中）的检查点稀疏，可能延迟几秒到几十秒。耐心等。

### Q：定时任务一直没跑

`/tasks/scheduled` 看：状态是不是禁用 / 下次触发时间是不是合理。

最常见：dotnet 启动后从未跑到 cron 触发时间。点"立即触发"测试。

### Q：执行历史只有 1000 条

是 `Hangfire.MemoryStorage` 内存上限。重启 dotnet 全部清空。要持久化历史 → 等 v1.1 切到 SQLite Hangfire backend。

---

## 部署

### Q：Docker 容器健康检查一直 unhealthy

`/healthz` 端点应该返回 `Healthy`。如果不是：

1. 看 `docker logs ninekg-web` 是否有启动错误
2. 容器内 `curl http://localhost:23333/healthz` 测
3. 镜像版本太旧没有 `/healthz` → `docker compose pull` 拉最新

详见 [DEPLOYMENT.md - 故障排查](../operations/deployment.md#故障排查)。

### Q：HTTPS 怎么配

Docker 内是 HTTP。HTTPS 由前置反代终止（Nginx / Caddy）。详见 [DEPLOYMENT.md - 反向代理](../operations/deployment.md#反向代理--https)。

### Q：升级到新版本 / 数据丢失风险

```bash
docker compose pull
docker compose up -d
```

数据保留（在 `./data` volume）。

主版本升级（v1 → v2）看 [CHANGELOG.md](../../CHANGELOG.md) 的 Breaking Changes 小节，可能要：
1. 备份 `./data`
2. 改 `config.yaml` 适配新 schema
3. 视情况 `NINEKG_RESET_DB=true` 重建

---

## 性能

### Q：批量识别百条以上慢

调大并发：`/settings → tasks.max_concurrent_identification_tasks → 10-15` → 重启。

### Q：内存占用 1GB+

Blazor Server circuit + Hangfire MemoryStorage 累积。重启容器；v1.1 切 SQLite Hangfire 后会改善。

### Q：搜索语义模式慢

向量库太大。关 `ai.vector.media.enable` 走纯关键词搜索。

---

## 杂项

### Q：能不能把 NineKgTools 用于普通家庭照片管理

**完全可以**。但建议：

1. 启动前替换 `Config/tags.yaml` 为自己的字典（默认是 R18 偏向）
2. 或启动后到 `/tags` 删掉不需要的分类

详见 [`tags-yaml-reference.md` - 关于默认标签](../reference/tags-yaml.md#关于默认标签的说明)。

### Q：项目支持中文 OCR / 自动字幕识别吗

不支持。NineKgTools 只管"知道你拥有什么"——不做内容分析、转码、播放。

### Q：能上传媒体到云盘吗

不支持。这是离线工具，不会上传任何媒体内容到第三方（除了 OpenAI Embedding 用到的标题/描述/标签**文本**）。

### Q：OpenAI key 用量监控

OpenAI 控制台看。NineKgTools 会用到的：

- 标签向量化（每个标签 1 次 embedding，上限 ~400 次/初次启动）
- 媒体向量化（每条媒体 1 次 embedding）
- 识别中的 AI fallback（`ai.use_ai_for_keyword_splitting = true` 时）

绝大多数操作是**一次性的**——同一标签 / 媒体不会重复 embed。`MediaVectorSync` 定时任务用 `cleanup_orphans / force_update` 控制是否重新做。

---

## 还没找到答案？

- 看[使用指南目录](README.md)对应章节
- 翻 [GitHub Issues](https://github.com/hphphp123321/NineKgTools/issues)
- 开新 Issue（带 `Logs/` 节选 + 截图诊断 Tab）

> 反复出现的问题会被整理到这份 FAQ。社区贡献的高级 tips / troubleshooting 会迁到 GitHub Wiki（v1.1 启用）。
