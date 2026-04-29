# 08. 任务系统（`/tasks` 系列）

## 这个页面是干啥的？

任务系统是 NineKgTools 的"后台引擎"——所有耗时操作（识别、向量同步、缓存清理、监视文件夹）都是 Hangfire **任务**，能看进度、能取消、能查历史。

![任务](../assets/screenshots/tasks.png)

任务相关有 4 个页面：

| 路由 | 用途 |
|---|---|
| `/tasks` | 总览仪表盘（统计 + 运行中 + 父子任务树） |
| `/tasks/background` | 后台任务（FolderMonitor 等长期跑的） |
| `/tasks/scheduled` | 定时任务（CacheCleanup 等 cron 触发的） |
| `/tasks/history` | 执行历史（已完成 / 失败的任务） |

## 主要操作

### 我想看现在后台在忙啥

进 `/tasks`：

页面顶部 4 个统计卡片：
- **总执行数**
- **成功率**
- **后台任务数**（运行中的长期任务）
- **定时任务数**（已配置的 cron 任务）

下方 **"运行中的任务"** 区域——所有正在跑的任务**实时刷新**（每 2 秒一次）：

- 任务名（如"批量识别: D:\Inbox"、"识别: RJ01081508"）
- 进度条
- 状态 chip（进行中 / 成功 / 失败 / 取消中）
- 已运行时长
- 进度文本（如 "12 / 30 已完成"）

支持**父子任务展开**：批量识别是父任务，30 条子任务以树形展开/折叠。

### 我想取消一个跑了很久的任务

任务行右侧有 ⋮ 菜单 → "取消"。

弹 `NineKgConfirmDialog Info` 确认。

**对父任务取消**会**同时取消所有子任务**——一键停掉整批。

> 已经完成的子任务不会回滚——只是停止后续未开始的。已 successful 入库的媒体留在数据库。

### 我想看某条任务的详细情况（识别诊断）

点任务行 → 跳转或弹出 `TaskDetailsDialog`：

**Tab 1: 进度**（只有运行中任务有）
- 实时进度条
- 子任务列表
- 取消按钮

**Tab 2: 执行日志**
- 详细的任务执行步骤日志（来自 `progressReporter.DebugAsync` 等调用）

**Tab 3: 识别诊断** ⭐（仅识别类任务）
- **关键词解析**：ProductCode / CircleName / PrimaryKeyword / SecondaryKeywords / CleanedTitle / DetectedLanguage
- **每次网站尝试**：
  - 网站名 + 状态（Success / NoMatch / Skipped / Exception / CacheHit）
  - 来源（搜索 / ID 直查 / 缓存）
  - 用时
  - 跳过/异常原因
- **Top 5 候选**：
  - 网站特定 ID
  - 标题
  - 相关性得分
  - 命中的查询关键词
  - 最终被采用那条 ✓ 高亮

**这是最有用的调试工具**——识别失败先看这里。

### 我想看后台任务（监视文件夹）

进 `/tasks/background`：

每个 `FolderMonitorTask` 一张卡片：

- 任务名（带路径，如"监视: F:\test"）
- 监控路径
- 运行时长
- 处理统计（监视到几个新增 / 已识别几个）
- 状态指示器（绿色脉冲 = 运行中）
- "查看详情" / "停止任务"按钮

如果你**临时不想监视某个目录**：点"停止任务"。但这只是当前会话，重启 dotnet 后会从 `config.yaml` 的 watch_folders 重建。永久禁用要去 `/settings → 媒体源 Tab` 删 watch_folders 条目。

### 我想看定时任务在何时触发

进 `/tasks/scheduled`：

5 个内置定时任务：

| 名称 | 默认 Cron | 干啥 |
|---|---|---|
| CacheCleanup | `0 0 * * *`（每日 0 点） | 清未使用的图片缓存 |
| MediaCleanup | `0 0 4 * *`（每月 4 号 0 点） | 清无效媒体记录 |
| TagVectorSync | `0 0 */6 * *`（每 6 小时） | 标签向量同步 |
| MediaVectorSync | `0 6 */6 * *`（每 6 小时偏 6 分钟） | 媒体向量同步 |
| PendingIdentificationCleanup | `0 3 * * *`（每日 3 点） | 清过期 Pending 识别结果（30 天） |

每个任务卡有：
- 状态（启用/禁用）
- 下次触发时间
- 上次执行时间
- "立即触发"按钮（不等 cron）
- "禁用"开关

> 改 cron 表达式或参数要改 `config.yaml` → `tasks.scheduled_tasks`，重启 dotnet 生效。这页面只能开关 / 立即触发，不能改 cron。

### 我想看历史失败的任务

进 `/tasks/history`：

完整执行历史列表，支持筛选：
- 状态（成功 / 失败 / 取消）
- 类型（识别 / 批量识别 / 监控 / 缓存清理 / ...）
- 时间范围

每行点详情按钮 → 弹 `TaskHistoryDetailsDialog`，结构跟运行中任务的 details 类似但**有完整的识别诊断快照**（运行结束后冻结）。

> 识别诊断**仅在内存**，重启 dotnet 会丢、上限 1000 条。要长期保留改 `Hangfire.MemoryStorage` 为 `SqliteStorage`（v1.1 计划）。

## 进阶用法

<details>
<summary>识别诊断快照</summary>

每条识别类任务（`SingleSourceIdentificationTask` / `BatchSourceIdentificationTask` 子任务）执行期间，框架自动收集诊断信息。

收集机制：`IdentificationDiagnosticsContext`（`AsyncLocal<IdentificationDiagnostics?>`），各识别源在关键节点调 `RecordKeywords` / `BeginAttempt` / `RecordCandidates` / `MarkChosen` / `EndAttempt`。

详见 [架构文档 - 识别诊断系统](../../CLAUDE.md#识别诊断系统)。

</details>

<details>
<summary>Hangfire Dashboard（开发用）</summary>

`/hangfire` 是 Hangfire 自带的 Dashboard（内置）：

- Recurring Jobs：所有定时任务
- Servers：两个 BackgroundJobServer（主 + identification 专用）
- Jobs：底层 job 视图（Enqueued / Processing / Succeeded / Failed）

跟 `/tasks` 的区别：
- `/tasks` 是**面向用户**的视图（业务术语 + 中文）
- `/hangfire` 是**面向开发者**的（Hangfire 原生 + 英文 + 详细技术信息）

排查深层任务问题时去 `/hangfire`。

</details>

<details>
<summary>识别队列的并发控制</summary>

`identification` 队列由**专用 BackgroundJobServer 独占**——`max_concurrent_identification_tasks`（默认 5）就是它的 worker count。

主服务器（处理其他所有队列）的 worker count = `Environment.ProcessorCount * 2`，不会拉 identification 任务。

这就是为啥识别批量提交 30 条，一次只跑 5 条——不是慢，是设计上限制并发避免触发识别源限流。

详见 [`CLAUDE.md` - Hangfire 任务并发](../../CLAUDE.md#hangfire-任务并发)。

</details>

<details>
<summary>导航栏的活动任务 Badge</summary>

每个页面顶部 AppBar 有个 **🔢 任务 Badge**——显示当前活动任务数。

点 Badge 弹"快速查看"对话框，列出运行中任务概览（不跳页）。再点"查看详情"才跳 `/tasks`。

Badge 数字每 3 秒刷新一次。

</details>

## 跟其他页面的关系

```
/tasks                            ← 总入口
   ├─ 卡片"后台任务" → /tasks/background
   ├─ 卡片"定时任务" → /tasks/scheduled
   ├─ 底部"执行历史" → /tasks/history
   ├─ 父任务展开 → 子任务树
   └─ 行点击 → TaskDetailsDialog（含识别诊断 Tab）

任何页面 AppBar 任务 Badge → 快速查看对话框 → 查看详情 → /tasks
```

## 常见问题

### Q：批量识别提交了 100 条，一次只跑 5 条，慢死

设计上限制——见上面"识别队列的并发控制"折叠。

如果你的识别源能扛更高并发，改 `config.yaml` → `tasks.max_concurrent_identification_tasks` 到 10-20，重启 dotnet。

### Q：取消任务后还在跑

Hangfire 任务取消是**协作式**——给任务发取消信号，任务在下一个 `cancellationToken.ThrowIfCancellationRequested()` 检查点才停。

某些操作（HTTP 请求中、文件 IO 中）的检查点稀疏，可能延迟几秒到几十秒才真停。耐心等 30 秒。

### Q：识别诊断 Tab 没出现

诊断仅在**识别类任务**上：`SingleSourceIdentificationTask` / `BatchSourceIdentificationTask` 子任务。

其他类型（CacheCleanup、FolderMonitorTask、TagVectorSync 等）没有识别动作，所以没诊断。

### Q：定时任务一直没跑

`/tasks/scheduled` 看：
- 状态是不是"已禁用"
- 下次触发时间是不是合理
- Hangfire Dashboard `/hangfire` 看 Recurring Jobs 真实状态

最常见原因：**dotnet 启动后从未跑到 cron 触发时间**——比如 CacheCleanup 是 0 点 0 分，你下午启动看当然还没跑过。点"立即触发"测试。

### Q：执行历史只看到 1000 条

是的，`Hangfire.MemoryStorage` 内存上限。重启 dotnet 全部清空。

要持久化历史 → 改 Hangfire backend 为 `SqliteStorage`（v1.1 计划）。临时需求：从 `Logs/` 文件日志找历史记录。
