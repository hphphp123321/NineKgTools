# 02. 第一次导入媒体

## 这个页面是干啥的？

这章不对应单一页面——而是**把"硬盘里的文件夹 → NineKgTools 的媒体库条目"** 这个最基本动作的三种方式讲清楚。

新用户看完这章就该会判断：**自己的场景该走哪条路径**。

| 你的场景 | 走这条路径 | 详细见 |
|---|---|---|
| 有几百几千个文件夹，想一次性整理 | **A：监视文件夹自动** | [13 工作流-流程一](13-workflows.md#流程一监视文件夹全自动) |
| 拿到 20-30 个新增文件夹，想立即识别 | **B：批量加入识别队列** | 本文 §B |
| 单个文件夹试水 / 调试识别效果 | **C：单个手动识别** | 本文 §C |
| 识别源搜不到的冷门资源 | **D：完全手动添加** | [13 工作流-流程二](13-workflows.md#流程二批量手动添加) |

## A：监视文件夹自动入库

最省事——配一次，往后都自动跑。

**简短**：`/settings → 媒体源 Tab → watch_folders` 加上你的目录路径，保存。之后扔进去的文件夹会被自动识别 + 自动入库。

**完整步骤**见 [13 工作流-流程一](13-workflows.md#流程一监视文件夹全自动)。

⚠️ Docker 部署：路径写**容器内的**（如 `/app/media`），主机目录靠 `docker-compose.yml` 的 volume 挂载。

## B：批量加入识别队列（最常用）

适合一次有几十个文件夹要处理的场景。

### 步骤

#### 1. 把文件夹放到一个临时目录

```
D:\Inbox\本周新增\
  ├── RJ01081508\
  ├── 同人作品 002\
  ├── ...
```

#### 2. 进 `/sources` 浏览到该目录

`/sources` 页面是"硬盘文件浏览器"风格——左侧导航树，右侧每行一个子文件夹/文件。

导航到 `D:\Inbox\本周新增`，右侧会列出 30 个子文件夹。

#### 3. 多选 → 批量加入识别队列

每行 checkbox 多选 → 顶部出现批量工具栏 → 点 **"批量加入识别队列"**：

- 自动提交到 Hangfire 的 `identification` 队列
- 创建一个**父任务**（Batch）+ 30 个子任务（Single）
- 受 `tasks.max_concurrent_identification_tasks` 限制并发（默认 5-8）

#### 4. 看进度

跳到 `/tasks` 看实时进度。父任务展开后看每个子任务的状态：进行中 / 成功 / 失败。

#### 5. 处理识别失败的

进度跑完后：
- **识别成功的**：直接进 `/media/overview`（如果 `auto_add_to_database = true`）
- **识别失败的**：留在 `/source/pending` 待识别 Tab，点行操作的"识别诊断"看为啥失败
- **识别成功但低于阈值**：跳到待入库 Tab 让你手动确认

详见 [04 待处理](04-pending.md)。

## C：单个手动识别（试水 / 调试）

想看看识别结果对不对、试不同的网站优先级、挑一个文件夹做"显微镜"诊断。

### 步骤

#### 1. 进 `/sources` 找到目标

跟 §B 一样导航到该文件夹。

#### 2. 点行操作的"尝试识别"

弹出 `IdentificationOptionsDialog`：

- **网站优先级**：临时调整这次识别要先试哪个站（不影响全局配置 `/website` 的设置）
- **跳过缓存**：默认 false；调试某个识别异常时设 true 强制重抓
- **最低相似度**：默认从 `config.yaml` 的 `identification.min_similarity` 读；这次识别可临时调

点"开始识别"。

#### 3. 看进度对话框

实时显示：当前在试哪个站、找到几个候选、匹配分数。

#### 4. 候选确认

跑完弹 `MediaInfoDialog` 让你预览所有候选 + 选中最佳的那条 → 点"添加到数据库"。

不满意所有候选 → 点"取消" → 这条进 `/source/pending` 待识别 Tab。

### 这种方式特别适合

- **测试新加的识别源**：单个跑看 ID 解析、字段映射有没问题
- **诊断"为啥这个识别失败"**：每一步进度都看得到（关键词切分、网站尝试、Top 5 候选打分）
- **小心翼翼地确认**：每一条都人工 review 后再入库，不放心 auto_add_to_database

## D：完全手动添加（识别源搜不到）

详见 [13 工作流-流程二](13-workflows.md#流程二批量手动添加)。

简单说：选路径 → 弹 `ManualAddMediaDialog` → 填标题 + 顶级分类（必填）+ 可选填具体分类/简介/评分 → 跳到 `/media/{id}` 继续完善。

## 三条路径的对比

```
┌─────────────────────────┬──────┬──────┬──────┐
│                         │  A   │  B   │  C   │
├─────────────────────────┼──────┼──────┼──────┤
│ 速度（识别 30 个）       │ 快   │ 快   │ 慢   │
│ 自动化程度              │ 100% │ 80%  │ 0%   │
│ 透明度（看每步）         │ 低   │ 中   │ 高   │
│ 失败后人工成本          │ 低   │ 中   │ 高   │
│ 适合常规业务            │ ✅   │ ✅   │ ❌   │
│ 适合调试 / 验证         │ ❌   │ ⚠   │ ✅   │
└─────────────────────────┴──────┴──────┴──────┘
```

## 进阶用法

<details>
<summary>识别队列优先级</summary>

`Hangfire` 的优先级队列：`critical / high / default / low / background / identification`。

**identification 队列由专用 BackgroundJobServer 独占处理**，并发数在 `config.yaml` → `tasks.max_concurrent_identification_tasks` 控制（默认 5）。

主服务器（识别外的全部任务）的 worker 数 = `Environment.ProcessorCount * 2`，比如 8 核 = 16 worker。

详见 [`CLAUDE.md` - Hangfire 任务并发](../../CLAUDE.md#hangfire-任务并发)。

</details>

<details>
<summary>识别缓存</summary>

`config.yaml` → `cache.expiration_minutes`（默认 30 分钟）：相同关键词的网站查询结果会被缓存。这样 30 个文件夹里有 5 个文件夹包含同样的主关键词时，只查一次站点。

如果你**怀疑某条记录的识别基于过期数据**，单个识别时勾"跳过缓存"强制重抓。

</details>

## 跟其他页面的关系

```
方式 A → /settings → 自动跑 → /media/overview
方式 B → /sources → 多选 → /tasks → /media/overview（成功）/ /source/pending（失败）
方式 C → /sources → 单个 → 进度对话框 → /media/{id}（确认）/ /source/pending（取消）
方式 D → /sources 或 /media/overview "新建媒体" → /media/{id}?edit=true
```

## 常见问题

### Q：放进 watch_folders 的文件夹没被自动识别

排查顺序：

1. `/sources` 看这个文件夹是否被扫到（应该出现在列表里）
2. `/tasks/background` 看 FolderMonitorTask 是否在跑（应该有一条对应你 watch_folders 的记录）
3. `Logs/` 看启动日志有没有 `StartProcessConfiguredFolders` 报错
4. 文件夹路径里有特殊字符 / 中文括号 / 全角空格？尝试改名

### Q：批量加入识别队列后任务一直 pending 不跑

`/tasks` 看是否有 30 个子任务都卡在"队列中"——可能 `max_concurrent_identification_tasks` 设得太低（如设为 1）→ 串行跑会很慢。改 `config.yaml` → 重启。

### Q：单个识别后弹的 MediaInfoDialog 我看不懂

详见 [03 媒体库](03-media-library.md) 末尾——MediaInfoDialog 的字段就是 Media 的字段。

### Q：方式 C 跟方式 B 的"识别"区别

方式 B 走 Hangfire 队列（异步、并发受限、失败可重试），方式 C 走前端发起的同步识别（实时进度对话框、单条单条来）。**结果是一样的**，区别是 UX。
