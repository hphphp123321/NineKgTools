# 12. 系统设置（`/settings`）

## 这个页面是干啥的？

`/settings` 是**整个 `config.yaml` 的可视化编辑器**——所有运行时配置（除了 `tags.yaml` 字典）都在 7 个 Tab 里。

![设置](../assets/screenshots/settings.png)

> 字段定义详见 [`docs/config-reference.md`](../reference/config.md)。本页只解释"你常改哪些 + 改了之后怎么生效"。

## 7 个 Tab 速览

| # | Tab | 你大概率会动的字段 |
|---|---|---|
| 1 | **应用设置** | （基本不动）web_host / web_port / 代理 / User-Agent |
| 2 | **日志配置** | log_level（Debug 调试时调）/ log_server（Syslog 才用） |
| 3 | **AI 配置** | open_ai.api_key（必填）/ base_domain（代理） |
| 4 | **文件配置** | minimum_file_size / ignored_patterns / allowed_extensions |
| 5 | **任务配置** | max_concurrent_identification_tasks / 定时任务的启用与参数 |
| 6 | **标签匹配** | enable_fuzzy_matching / similarity_threshold |
| 7 | **搜索配置** | enable_global_search / cache_expiration_minutes |

> ⚠️ "网站识别源" 不在 settings——在 `/website`（详见 [11 识别源](11-website.md)）

## 主要操作

### 我想改 OpenAI API Key

Tab 3 **AI 配置** → `open_ai.api_key`：

- 直接填 sk-xxx
- 或留空，让程序从 `OPENAI_API_KEY` 环境变量读（推荐——避免 yaml 文件含密钥）

测试连接：旁边的"测试 OpenAI 连接"按钮。绿色 OK = 配置就绪。

### 我想配代理（OpenAI 在国内连不通）

Tab 3 → `base_domain`：

```
https://api.openai.com/                    # 官方
https://your-proxy.com/                    # 你的代理（兼容 OpenAI 协议）
https://api.zhizengzeng.com/               # 国内代理示例
```

> 不要填路径，只填域名。OpenAI 协议的路径（`/v1/chat/completions`）由 SDK 自动拼。

### 我想加 / 删监视文件夹

Tab 5 任务配置里 **没有**这个——它在 `Settings.razor` 的"媒体源"专属编辑区（页面顶部子 Tab）。

```
/settings → 媒体源 → watch_folders
  - F:\test                           ← 现有
  + D:\Inbox\本周新增                  ← 新加
```

改完保存 → **重启 dotnet 生效**（FileSystemWatcher 在启动时挂上，运行时改 yaml 不会重挂）。

> Docker 部署：路径写**容器内的**（如 `/app/media`），主机目录靠 volume 挂载。

### 我想改识别并发数

Tab 5 任务配置 → `max_concurrent_identification_tasks`：

- 默认 5-8
- 调小（2-3）：识别源被限流时
- 调大（10-15）：你机器和网络扛得住，想更快批量识别

⚠️ 这个值**只在 dotnet 启动时读一次**——改完保存不会立即生效，**必须重启**。详见 [`CLAUDE.md` - Hangfire 任务并发](../../CLAUDE.md#hangfire-任务并发)。

### 我想关掉某个定时任务

Tab 5 → `scheduled_tasks` 列表 → 找到对应任务 → `enabled` 开关关闭 → 保存。

或者去 `/tasks/scheduled` 页面用 UI 切（更直观）。

### 我想改识别策略（自动入库 / 阈值）

Tab 5 任务配置或 Tab 7 搜索配置都不是——是**单独的"识别"分组**（在 Settings 页的子 Tab）：

| 字段 | 推荐值 | 说明 |
|---|---|---|
| `auto_add_to_database` | true（自动） / false（人工 review） | 影响 watch_folders 自动入库的行为 |
| `min_similarity` | 0.6-0.8 | 识别相似度门槛 |
| `pending_retention_days` | 30 | 待入库结果保留天数 |
| `skip_cache` | false | 调试时设 true 强制重抓 |
| `strategy` | Auto / Manual / Hybrid / ... | 识别策略 |
| `timeout_seconds` | 30 | 单网站识别超时 |

### 我想调日志级别（debug 时刷屏看细节）

Tab 2 → `log_level`：

- **Verbose**（最详细，刷屏）
- **Debug**（调试时用）
- **Information**（默认，平衡）
- **Warning**（仅看警告）
- **Error**（仅看错误）
- **Fatal**（仅看致命）

调完保存 → **不需要重启**——Serilog 会立即应用新级别。

## 进阶用法

<details>
<summary>所有改动什么时候生效</summary>

| 改动 | 立即生效 | 重启生效 |
|---|---|---|
| OpenAI API key | ✅（下次识别用新 key） | — |
| 网站启用开关 | ✅ | — |
| 网站优先级 | ✅ | — |
| 标签匹配阈值 | ✅（下次识别用） | — |
| 搜索配置 | ✅（下次搜用） | — |
| 日志级别 | ✅ | — |
| **watch_folders** | ❌ | ✅ |
| **max_concurrent_identification_tasks** | ❌ | ✅ |
| **定时任务的 cron 表达式** | ❌ | ✅ |
| **数据库路径** | ❌ | ✅ |
| **web_host / web_port** | ❌ | ✅ |

简单规律：**影响 DI 注册或启动逻辑的字段需要重启；运行时查询的字段立即生效**。

</details>

<details>
<summary>用环境变量覆盖敏感字段</summary>

部分字段支持环境变量优先：

```yaml
ai:
  open_ai:
    api_key:           # 留空，从 OPENAI_API_KEY 读
```

详见 [`README.md` - 配置与 API Key](../../README.md#-配置与-api-key)。

Bangumi key **没有环境变量回退**——只能写在 `config.yaml` 或 `/website` UI 填。

</details>

<details>
<summary>导出 / 导入配置</summary>

v1.0 没有 UI 导出。但 `Config/config.yaml` 本身就是配置——直接复制这个文件到另一台机器替换，参数全过去了。

⚠️ 复制前**清掉 `api_key`**——避免泄密。

</details>

<details>
<summary>NINEKG_RESET_DB 是干啥</summary>

环境变量 `NINEKG_RESET_DB=true` 启动时会**清空数据库**重建。仅用于：

1. 改了 `tags.yaml` 想重灌入字典
2. 改了 ER 模型（加字段 / 改类型）
3. 想从零开始

详见 [`CLAUDE.md` - 数据库初始化](../../CLAUDE.md#数据库初始化programcs)。

⚠️ **会丢全部数据**！默认是 false（保护生产容器）。

</details>

## 跟其他页面的关系

```
/settings                       ← 你在这
   ├─ Tab 媒体源 → watch_folders → 影响 /sources 自动扫描
   ├─ Tab AI → 影响 /search 语义搜索 + 标签向量化
   ├─ Tab 标签匹配 → 影响识别时标签命中行为（/tags 间接受影响）
   ├─ Tab 任务 → 影响 /tasks/scheduled 行为
   └─ Tab 搜索 → 影响 /search 行为

不在这里：
   /website     → 网站识别源配置（独立页面）
   /tags        → 标签字典管理（独立页面）
   /tags/mappings → 标签映射（独立页面）
```

## 常见问题

### Q：保存了配置但没看到效果

看上面"所有改动什么时候生效"折叠区——某些字段需要重启 dotnet。

`docker compose restart web` 或 `taskkill /F /IM dotnet.exe` 后重新启动。

### Q：改坏了某个 Tab 想恢复默认

v1.0 没有"恢复默认"按钮。手动方法：

1. 看 `Config/config.example.yaml` 找该字段的默认值
2. 改回去保存

### Q：所有 Tab 的字段都在 config.yaml 里吗

是的——这个页面就是 `config.yaml` 的 1:1 编辑器。

但 `tags.yaml` 不在这里管（它是字典，不是配置）。

### Q：能用 yaml 直接改吗（不通过 UI）

可以——`Config/config.yaml` 就是源头。改完保存，重启 dotnet。

但 UI 比手编 yaml 更**安全**：
- 强校验类型（数字字段不会输成字符串）
- 下拉枚举（log_level / strategy 不会拼错）
- 联动验证（cron 表达式实时解析）

新手强烈建议走 UI，老用户随意。
