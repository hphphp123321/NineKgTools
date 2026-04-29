# 05. 媒体源管理（`/sources` & `/source/{id}`）

## 这个页面是干啥的？

**媒体源（MediaSource）** 是 NineKgTools 里的"原始文件 / 文件夹的元数据记录"——每条记录指向硬盘上的一个真实路径。它跟 **媒体（Media）** 不是一回事：

```
/sources         所有源（不论识别状态）—— 你硬盘有啥
/source/pending  仅"还没事"的子集     —— 待识别 / 待入库
/source/{id}     单个源的详情        —— 文件树 + 重新识别
/media/overview  已识别 + 已入库的源   —— 完整媒体库
```

> 一个 MediaSource **可以**没对应 Media（识别失败 / 没入库）。一个 Media **必须**关联一个 MediaSource。

## `/sources` —— 源浏览器

这页面是**项目自带的文件管理器**——左侧目录树，右侧文件清单。但它**只显示磁盘上真实的文件夹和文件**，不是数据库记录。

### 主要操作

#### 我想浏览整块硬盘看哪些可以加进媒体库

左侧导航树展示**项目能访问到的所有磁盘**（Windows: C:\ D:\ E:\...；Linux: 容器内 `/app/media` 等挂载点）。

逐级展开 → 右侧出现该目录下的子文件夹和文件。

> Docker 部署里**只能看容器内的路径**——主机的其他目录靠 volume 挂载暴露。详见 [DEPLOYMENT.md - 持久化目录](../operations/deployment.md#持久化目录)。

#### 我想对单个文件夹做识别

每行右侧两个按钮：

- **尝试识别** ▶️：弹 `IdentificationOptionsDialog` → 跑识别 → 弹 `MediaInfoDialog` 确认入库（详见 [02-C 单个手动识别](02-first-import.md#c单个手动识别试水--调试)）
- **手动添加** ➕：跳过识别，直接走 `ManualAddMediaDialog`（详见 [13 工作流-流程二](13-workflows.md#流程二批量手动添加)）

#### 我想批量识别

多选 checkbox → 顶部"批量加入识别队列"按钮 → 提交 30 个识别任务到 Hangfire（详见 [02-B 批量加入识别队列](02-first-import.md#b批量加入识别队列最常用)）。

#### 我想看哪些已经识别过了

每行有**状态 chip**：
- 🟢 **已识别 + 已入库**：可点跳到 `/media/{id}`
- 🟡 **已识别 + 待入库**：可点跳到 `/source/pending?tab=pending`
- ⚪ **未识别**：还没动过

如果你只想看"未识别"的源，用筛选栏过滤。

#### 我想查看 / 编辑某条源的详情

点行的源名称 → 跳到 `/source/{id}`。

## `/source/{id}` —— 单源详情页

### 主要操作

#### 我想看这个文件夹里到底有啥文件

页面顶部有一个 `MudTreeView` **完整展示目录树**——所有子文件夹与文件层级化排列。

根据**媒体源的类型**（视频/音频/游戏/...），相关文件会**自动高亮**：

- 视频源：`.mp4` / `.mkv` / `.avi` 标 ⭐
- 音频源：`.mp3` / `.flac` / `.wav` 标 ⭐
- 游戏源：`.exe` / `.bat` 标 ⭐

不相关的文件灰显但仍可见（方便检查附带的资源）。

#### 我想改这个源的类型

页面顶部"媒体源类型"下拉 → 改"视频" → "音频"。

> 改类型会影响：
> 1. 重新识别时使用的网站优先级（音频默认 DLsite，视频默认 DLsite + Bangumi）
> 2. 文件高亮规则
> 3. 详情页布局微调

#### 我想设置入口文件（双击 exe 直接启动）

游戏源、视频源等场景下，"入口文件"是双击直接启动的那个：

- 游戏源：`launcher.exe`
- 视频源：第 1 集 / 唯一的 mp4

操作：在文件树里**右键某个文件 → "设为入口文件"** → 保存。

之后页面顶部 **"启动" 按钮**会用这个入口文件直接打开（本地访问场景下）。

> 远程访问（不是 127.0.0.1 / 局域网网段）会自动隐藏"启动"按钮——浏览器在你机器上，启动按钮调用的是**服务器**的 `Process.Start`，远程意义不大。

#### 我想重新识别

页面右上角的 **重新识别** 按钮 → 复用 [02-C 单个手动识别](02-first-import.md#c单个手动识别试水--调试) 的流程。

**重要**：如果这条源**已经入库**了（`InDatabase = true`），重新识别成功后 **会先删旧 Media + 关联图片/向量**，然后用新结果建新 Media。MediaSource 自身保留并复用。

旧 Media 没了——意味着收藏夹关联、自定义标签、自定义评分 **都会丢**。所以重新识别前如果你做了大量自定义编辑，请先备份。

#### 我想看关联媒体的封面 / 标题

如果这条源已经识别且入库，**详情页底部**会显示关联 Media 的封面 + 标题 + 简介——你能直接跳到 `/media/{id}` 看完整字段。

## 进阶用法

<details>
<summary>"打开文件位置"按钮如何工作</summary>

本地访问（`localhost` / `127.0.0.1`）：调用服务端的操作系统 API（`explorer.exe path` / `nautilus path` / `open path`）打开文件管理器。

远程访问：自动**隐藏**——服务端打开文件管理器对你毫无意义。

判断逻辑：`HttpContext.Connection.RemoteIpAddress` 不是 loopback 就视为远程。

</details>

<details>
<summary>媒体源的"扫描新文件"</summary>

每条源都有"扫描新文件"按钮，重新读硬盘看是否有新加的文件——会把新文件加到这条源的关联文件清单里（不是创建新源）。

适合"原源是个文件夹，里面新增了几个补丁包 / 后续语音包"的场景。

</details>

<details>
<summary>watch_folders 自动扫描的机制</summary>

应用启动时，`FilesService.StartProcessConfiguredFolders` 对每个 watch_folders 路径：

1. 提交 `FolderMonitorTask`（low 队列）→ 挂上 `FileSystemWatcher` 实时监听
2. 调 `IdentifyBatchMedia` 扫一遍现有内容（一次性）

监控与批量识别**已解耦**——空文件夹、部分识别失败都不会阻断监控。

详见 [`CLAUDE.md` - 监视文件夹](../../CLAUDE.md#监视文件夹watch_folders)。

</details>

## 跟其他页面的关系

```
/sources                       ← 你在这（浏览器）
   ├─ 行点击源名称 → /source/{id}
   │     ├─ 改类型保存
   │     ├─ 重新识别 → 进度对话框 → /media/{newId}（确认）
   │     └─ "媒体"链接 → /media/{id}（关联 Media）
   ├─ 行"尝试识别" → 进度对话框 → /tasks 队列
   └─ 行"手动添加" → ManualAddMediaDialog → /media/{newId}?edit=true

/source/pending           ← 仅 (Identified=false 或 InDatabase=false) 的子集（详见 04）
```

## 常见问题

### Q：`/sources` 跟 `/source/pending` 的关系

`/sources` = 全部 MediaSource 的浏览器（按硬盘路径组织）
`/source/pending` = MediaSource 的"还没完事"子集（按状态组织）

两个页面都能多选批量识别，但**默认场景**：

- 想找新加的源批量识别 → `/sources`
- 想清理"识别失败堆积"的队列 → `/source/pending`

### Q：删除源会删硬盘文件吗

**不会**。所有"删除"都只删数据库记录。硬盘文件原封不动。

### Q：源被识别后改了类型，要重新识别吗

是的。改类型只改源记录，**不重新识别**。如果你新选的类型对应不同的识别源（比如从"音频"改"游戏"，DLsite 接口不同），需要点"重新识别"按钮跑一遍。

### Q：删了源对应的 Media 还在吗

**还在**——因为 Media 跟 MediaSource 是 1:1 关联但生命周期独立。删 Source 不会级联删 Media（设计上避免误删高价值的 Media 元数据）。

实际操作时如果你想"完全干掉这条":
1. 进 `/media/{id}` 删 Media
2. 进 `/source/{sourceId}` 删 Source

或者反过来——但记得两边都做。
