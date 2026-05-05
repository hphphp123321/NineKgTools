# Desktop Phase 3：桌面端独占功能

> **目标**：实现 Web 端做不到的体验差异化点——系统托盘、原生文件拖拽、资源管理器右键集成、多窗口。**这是用户选择"独立桌面体验"的兑现 Phase**。
>
> **估时**：2–3 周
>
> **平台范围**：Windows 完整实现 + Mac/Linux best-effort。Mica / 托盘 / Shell 集成在非 Windows 上 graceful 降级。
>
> **前置条件**：Phase 1 + Phase 2 已经达成功能等价。
>
> **视觉基线**：所有交互元素遵守 [Phase 1 视觉设计基线](desktop-phase-1.md#视觉设计基线先读这一节)；Phase 3 主要是 OS 集成层，UI 元素少但**交互流**是设计重点。

---

## 1. 系统托盘（Windows 优先）

让用户可以"关窗不关进程"——主窗 X 后程序仍在托盘运行，文件夹监控继续工作，识别任务继续跑。

### 任务

- [ ] **包**：`NineKgTools.Desktop.csproj` 加托盘控件（包选型见未解决问题）
- [ ] **`Services/TrayService.cs`**（Win / Mac / Linux 各一份 partial 实现）
- [ ] **托盘菜单项**（详见设计稿）
- [ ] **关窗行为**：`MainWindow.Closing` 事件拦截，默认改为"最小化到托盘"；只有托盘菜单"退出"或全局快捷键退出时才真停
- [ ] **Settings 项**：[Phase 2 任务 3 Settings](desktop-phase-2.md#3-设置页settings) 的"外观"组加"关闭主窗时" 单选：[最小化到托盘 / 退出应用]，默认前者
- [ ] **任务完成 Toast 通知**：用 Windows Toast（`Microsoft.Toolkit.Uwp.Notifications` 或跨平台抽象包）
- [ ] **托盘图标动态化**：识别任务运行时图标加 dot badge（小红点），全部完成消失

### 设计稿

```
托盘图标 + ContextMenu 展开
                                            ┌─────────────────────────────┐
                                            │ ◆ NineKgTools  (运行中: 5)  │  ← 头部状态栏
                                            ├─────────────────────────────┤
                                            │  📋 打开主窗                │
                                            │  ────────────────           │
                                            │  ⏸  暂停全部识别            │  ← toggle 项
                                            │  ⏸  暂停文件夹监控          │
                                            │  ────────────────           │
                                            │  📊 5 个任务运行中  ›       │  ← 子菜单显示前 5 个任务名
                                            │  ⚠ 3 个任务失败    ›       │
                                            │  ────────────────           │
                                            │  ⚙  设置...                 │
                                            │  ?  关于                    │
                                            │  ────────────────           │
                                            │  ✕  退出                    │  ← 真退出
                                            └─────────────────────────────┘
                                                                    [系统托盘 →]

托盘图标的 4 个状态：
- 默认 (静止)         : ◆ (单色)
- 运行中 (有 ≥1 任务) : ◆· (右下角蓝色小点)
- 全部失败            : ◆! (右下角红色感叹号)
- 暂停状态            : ⏸ (替换为暂停图标)

任务完成 Toast 通知（Win11 右下角）
┌─────────────────────────────────────────────┐
│ ◆ NineKgTools                            ✕ │
│                                             │
│  ✓ 识别完成: 视频名_完整版                  │
│  DLsite · RJ01081508 · 评分 0.95           │
│                                             │
│  [查看详情]  [入库]                         │  ← 可点击 action
└─────────────────────────────────────────────┘
```

### 关键决策

- **托盘菜单头部** 显示当前实时状态——避免每次右键都要先打开主窗才能知道有几个任务在跑
- **子菜单展示任务列表**：把"5 个任务运行中" hover 出子菜单显示具体任务名（最多 5 个），点击单个任务直接打开 BackgroundTasksPage 并选中
- **图标动态化**——红色感叹号是用户最关心的"出问题了"信号，不需要打开主窗就能感知
- **Toast 通知** 限速：相同任务类型 30s 内只弹一次（避免批量识别 100 个文件刷屏）；失败任务 always 弹（用户一定要知道）
- **关窗 vs 退出语义**：关闭主窗 ≠ 退出应用——这是 Win 用户的预期（OneDrive、Steam、Discord 都这样）；用户首次关窗时 InfoBar 提示一次"应用仍在托盘运行"，记住后不再提醒
- **Mac**：macOS 默认就是"关窗不退应用"，菜单栏图标走 NSStatusBar；Avalonia 的 TrayIcon 在 Mac 上自动用菜单栏。**实现成本几乎为零**——Mac 反而是 Phase 3 最便宜的平台

### 验收

1. 关主窗 → 进程仍在 + 托盘有图标 + 任务管理器看得到进程
2. 托盘"暂停识别" → 队列里的任务停止；恢复后继续
3. watch_folder 里放文件 → 识别成功后桌面 Toast 通知 + 含可点击的"查看详情/入库"动作
4. 识别任务运行时图标右下角有蓝色 dot；全部完成后 dot 消失
5. 托盘菜单"退出" → 进程清理（MonitorService 停 + Hangfire 落库 + Log flush）后真退

---

## 2. 原生文件拖拽接收

把文件夹/文件直接从资源管理器拖到主窗，触发"加入监视文件夹"或"识别此文件"。

### 任务

- [ ] **主窗 DragDrop 接入**：`DragDrop.SetAllowDrop(this, true)` + 处理 `DragOver` / `Drop` 事件
- [ ] **可视反馈**：拖拽进入主窗时显示半透明 overlay + 提示
- [ ] **路径解析**：从 `DragEventArgs.Data.GetFiles()` 拿到 `IStorageItem` 列表
- [ ] **分类决策**（详见设计稿）
- [ ] **从浏览器拖图片**：暂不支持——Phase 3 不做（远端 URL 需要先下载，复杂度高，与媒体库主流程不符）

### 设计稿

```
拖拽进入主窗时的 Overlay（覆盖整个内容区，不含侧栏和标题栏）
┌────────────────────────────────────────────────────────────┐
│ ◆ NineKgTools                            [Search]   _ □ ✕ │
├────────┬───────────────────────────────────────────────────┤
│  ◇首页 │  ╔═════════════════════════════════════════╗      │
│  ▶媒体 │  ║                                          ║      │
│  ●待处│  ║                                          ║      │
│  ▦媒源 │  ║       📥 (大号 80x80 图标)               ║      │
│  ⚙任务 │  ║                                          ║      │
│  ＃标签│  ║       放下文件以识别                     ║      │
│  ...   │  ║       或文件夹以加入监视                ║      │
│        │  ║                                          ║      │
│  ⚙设置 │  ╚═════════════════════════════════════════╝      │
│        │      ← Backdrop alpha 0.7 + Acrylic blur          │
└────────┴───────────────────────────────────────────────────┘

放下后的分类决策对话框（拖了一个文件夹时）：
┌──────────────────────────────────────────────┐
│  📁 你拖入了一个文件夹                        │
│                                              │
│  C:\Media\NewBatch\                          │
│  包含 23 个文件                              │
│                                              │
│  ┌──────────────┐  ┌──────────────┐         │
│  │ 🔁           │  │ ⚡            │         │
│  │ 加入监视     │  │ 一次性识别    │         │
│  │ 长期跟踪     │  │ 跑完即结束    │         │
│  └──────────────┘  └──────────────┘         │
│                                              │
│                          [取消]              │
└──────────────────────────────────────────────┘
```

### 关键决策

- **Overlay 出现时机**：`DragEnter`（拖进窗口范围）触发，`DragLeave` / `Drop` 消失；停留 200ms 内的快速划过不显示（防误触）
- **多文件 vs 单文件 vs 文件夹** 三种情况分别处理，不混用统一对话框：
  - 单文件 → 直接走 [`ManualAddMediaHelper.OpenByPathAsync`](desktop-phase-1.md#7-共享对话框体系基础设施)（无对话框，最快路径）
  - 单文件夹 → 弹"加入监视 / 一次性识别"双卡片选择（设计稿上的对话框）
  - 多个项 → 弹 `NineKgConfirmDialog Affirmative` 确认 "识别这 N 个项目?"
- **错误处理**：路径不存在 / 权限拒绝 → InfoBar Severity=Error 显示 5s，**不**用对话框（不打断流程）

### 验收

1. 从 Win11 资源管理器拖 1 个文件夹到主窗 → 弹双卡片对话框 → 选"加入监视" → SourcesPage 出现该文件夹
2. 拖 5 个零散视频文件 → `NineKgConfirmDialog` 确认"识别这 5 个文件" → 全部进入识别队列
3. 拖拽过程中主窗有 Overlay 视觉反馈（200ms 后才显示，避免误触）
4. 拖拽出错走 InfoBar 不崩

---

## 3. Windows Shell 右键集成（仅 Win）

在资源管理器里右键文件 / 文件夹 → "用 NineKgTools 识别"。

### 任务

- [ ] **方案**：Win11 Shell Verb，注册表方式
  - `HKEY_CURRENT_USER\Software\Classes\*\shell\NineKgTools识别\command`
  - command 指向 `NineKgTools.Desktop.exe --identify "%1"`
- [ ] **`Program.cs` 命令行参数处理**：检测 `--identify <path>` 参数，启动后直接走 ManualAddMediaHelper 跳过主窗显示
- [ ] **单实例机制**：`Mutex` 实现单实例；新进程把 `--identify <path>` 通过 named pipe 转发给现有进程后退出（依赖任务 5 的 IPC）
- [ ] **注册 / 卸载**：[Phase 2 Settings](desktop-phase-2.md#3-设置页settings) "外观"组加"集成 Windows 资源管理器" toggle；勾上写注册表，取消删；本 Phase 不做安装包级别的注册（移到 [Phase 4](desktop-phase-4.md)）
- [ ] **降级**：Mac 上做"Open with NineKgTools" Service（macOS 自带 Services 菜单），Linux 跳过

### 设计稿

```
Win11 资源管理器右键菜单 (Win11 紧凑菜单 + "显示更多选项" 兼容)
┌──────────────────────────────────────────┐
│  📁 video.mp4                            │
│                                          │
│  打开                                    │
│  在新窗口中打开                          │
│  ────                                    │
│  📋 复制                                 │
│  ✂️ 剪切                                 │
│  ────                                    │
│  ◆ 用 NineKgTools 识别  [新增]          │  ← Shell verb
│  ────                                    │
│  📤 发送到 ›                            │
│  💡 显示更多选项                        │
└──────────────────────────────────────────┘

Settings "集成 Windows 资源管理器" 区段
┌────────────────────────────────────────────────────────┐
│  外观                                                  │
│  ────                                                  │
│  ☑ 集成 Windows 资源管理器                            │
│     启用后，资源管理器右键文件可直接调用识别。         │
│     可能弹 UAC 申请权限（仅首次注册时需要）。          │
│                                                        │
│     状态: ✓ 已注册                                     │
│     [测试: 打开测试文件]    [手动重置注册]            │
└────────────────────────────────────────────────────────┘
```

### 关键决策

- **HKCU 而非 HKLM**：写入 `HKEY_CURRENT_USER`——单用户注册不需要 UAC 提权，最佳路径；HKLM 全局注册留给 [Phase 4](desktop-phase-4.md) 安装包
- **图标**：注册表 `Icon` 值指向 exe 资源——右键菜单旁边能显示 ◆ 项目图标
- **单实例 + IPC 转发**：双击桌面图标时启动主窗；右键菜单触发时（exe 已在跑）转发命令；右键菜单触发时（exe 没跑）启动 + 处理命令
- **Win10 兼容**：Win11 默认隐藏旧式右键项（"显示更多选项"才能看到），所以建议用户右键 → 显示更多选项 → 找到 "用 NineKgTools 识别"。这是 Win11 OS 行为，不是我们的问题
- **测试按钮**：注册成功后用户能立刻用 Settings 里"测试: 打开测试文件"验证；"手动重置注册"用于注册失败时清理 + 重试

### 验收

1. Settings 勾 "集成 Windows 资源管理器" → 资源管理器右键单个视频文件（Win11 需"显示更多选项"）→ 看到"用 NineKgTools 识别"
2. 点击该项 → 已有桌面端进程：弹出 ManualAddMediaDialog；没进程：先启动再弹
3. Settings 取消勾选 → 右键菜单消失
4. 双击桌面图标 + 右键集成同时触发 → 用户最多看到一个主窗（单实例）

---

## 4. 多窗口管理增强

[Phase 1 任务 3](desktop-phase-1.md#3-媒体详情页独立窗口体验) 已经实现"媒体详情每开一个窗"。本任务做更系统的多窗口体验。

### 任务

- [ ] **`Services/WindowManager.cs`**：统一管理所有非主窗
  - `OpenMediaDetail(mediaId)` → 检查是否已有该 media 的窗口在开 → 有则 Activate，无则新建
  - `CloseAll()` → 主窗关闭时一并关
- [ ] **任务进度独立窗口**：长任务支持"分离到独立窗口"按钮，独立窗显示进度 + 日志，主窗可继续浏览
- [ ] **窗口位置记忆**：每类窗口（详情 / 任务）记录上次 size + position 到 `localAppData/.../window-state.json`
- [ ] **置顶选项**：每个窗右上角"📌 置顶"按钮，勾上 `Topmost=True`
- [ ] **快捷键**：
  - `Ctrl+W` 关当前窗
  - `Ctrl+Tab` 在所有打开窗口间切换
  - `Ctrl+1..9` 主窗里跳到对应 NavigationView 项

### 设计稿

```
任务进度独立窗口（从 BackgroundTasksPage 拽出来）
┌────────────────────────────────────────────────┐
│ ◆ 批量识别 142/200          📌 _ □ ✕         │
├────────────────────────────────────────────────┤
│                                                │
│   ████████████████████░░░░  71%                │
│                                                │
│   已用时 4m 23s · 预计还需 1m 47s              │
│                                                │
│  ────────────────────────────────────────      │
│   实时日志                                      │
│   ────                                         │
│   16:32:11  ✓ video_140.mp4 → DLsite           │
│   16:32:14  ✓ video_141.mp4 → Bangumi          │
│   16:32:17  ⚠ video_142.mp4  全部网站未命中    │
│   16:32:19  ▶ video_143.mp4  正在识别...       │
│                                                │
│  ────────────────────────────────────────      │
│                          [暂停] [取消任务]     │
└────────────────────────────────────────────────┘
   宽 480 高 420 (窗口可调，position memory)

WindowManager 状态可视化（不是 UI，是工程模型）
┌──────────────────────────────────────────┐
│  WindowManager                            │
│   ┌─ MainWindow (1)                      │
│   ├─ MediaDetailWindow (mediaId=42)      │
│   ├─ MediaDetailWindow (mediaId=58)      │
│   └─ TaskProgressWindow (taskId=xxx)     │
└──────────────────────────────────────────┘

OpenMediaDetail(42) 二次调用 → Activate 已有窗口而非新建
```

### 关键决策

- **避免重复打开同一 media**——OpenMediaDetail 二次调用时 `Activate()` 现有窗口（带闪烁动画）；很多用户会双击两次同一个卡片
- **任务进度独立窗** 触发条件：BackgroundTasksPage 任务行右上角 "↗ 分离" 按钮；分离后主窗任务行替换为"已分离 ↗"占位
- **窗口位置记忆** key 选择：详情窗用 `media:{id}` 各自独立；任务窗用 `task:{type}` 同类型共享一个位置
- **快捷键全局可用**：所有非主窗也能 `Ctrl+W` 关自己；主窗 `Ctrl+W` 不响应（避免误关主窗）
- **关闭主窗 → 关闭全部子窗**：用 `WindowManager.CloseAll()` 在 `MainWindow.Closing` 里调用；走"最小化到托盘"路径时不关子窗

### 验收

1. 同时打开 5 个媒体详情 → Alt+Tab 列表正确显示 6 个窗口（主窗 + 5 详情）
2. 关主窗（设置=退出应用模式）→ 5 个详情窗一并关
3. 关一个详情 → 重启应用 → 打开同一 media → 窗口位置和大小恢复
4. 触发 100 文件批量识别 → "分离到独立窗口" → 主窗可正常浏览，独立窗显示实时进度
5. 重复点击同一 media 卡片不会开两个详情窗——已有窗口被 Activate 到前台

---

## 5. 跨进程 IPC（命令行集成 + 后续扩展空间）

任务 3 的右键集成需要"已有进程时转发命令"。本任务把这套抽出作为基础设施。

### 任务

- [ ] **`Services/IpcService.cs`**：基于 `NamedPipeServerStream` / `NamedPipeClientStream`（Win），Unix Domain Socket（Mac/Linux）
- [ ] **协议**：JSON-Lines，每行一个命令 `{"cmd":"identify","path":"..."}`
- [ ] **支持的命令**：
  - `identify <path>` → 触发 ManualAddMediaHelper
  - `show-main` → 把主窗 Activate 到前台
  - `quit` → 优雅退出
- [ ] **健壮性**：服务端崩溃时客户端不阻塞（超时 + 兜底启动新进程）

### 设计稿

无 UI——纯工程接口。但流程图：

```
用户右键 video.mp4 → "用 NineKgTools 识别"
                     │
                     ▼
       NineKgTools.Desktop.exe --identify "C:\video.mp4"
                     │
                     ▼
            ┌────────┴────────┐
            ▼                 ▼
      Mutex 已存在？     Mutex 不存在？
      (已有进程)         (无进程)
            │                 │
            ▼                 ▼
   连 NamedPipe 转发    启动新进程，处理命令
            │                 │
            ▼                 ▼
     现有进程的 IpcService    新进程的 IpcService
     收到 identify cmd        启动同时处理 identify
            │                 │
            └────────┬────────┘
                     ▼
            打开 ManualAddMediaDialog
```

### 关键决策

- **Pipe 名称**：`NineKgTools.Desktop.IPC.{username}`——多用户系统不冲突
- **超时 2s**：客户端连接超时则放弃转发，自己启动；防止现有进程死锁导致命令丢失
- **协议简单 JSON-Lines**——不引 gRPC / protobuf，本 Phase 命令就 3 个，YAGNI
- **不做远程 IPC**：只支持本机 IPC，远程 RPC 留给后续如果真做"Web 端 ↔ Desktop 端 sync"再说

### 验收

1. 命令行 `NineKgTools.Desktop.exe --identify "C:\foo.mp4"` 在已有进程时秒级响应
2. 命令行 `NineKgTools.Desktop.exe --quit` 优雅终止现有进程

---

## 6. 平台特定 polish（Mac / Linux best-effort）

仅 Win 完整做的功能在其他平台 graceful 降级。

### 任务

- [ ] **Mac**：
  - 关窗不退出进程是 macOS 默认行为，用 Avalonia 内置 `TrayIcon` 自动落到 NSStatusBar
  - Mica 不可用 → `TransparencyLevelHint="Acrylic,Blur"` fallback；FluentAvalonia 自动降级
  - Shell 集成：用 `NSServicesMenuItem` 添加 "Open with NineKgTools" Service（Phase 3 不做，留 v2）
- [ ] **Linux**：
  - 托盘：`libappindicator` 跨发行版兼容性差，**Phase 3 不做**——关窗即退出
  - Mica 不可用 → 纯色背景（FluentAvalonia 自动降级）
  - Shell 集成跳过（不同发行版不同 file manager，复杂度高）
- [ ] **`RuntimeInformation.IsOSPlatform(...)`** 在所有"平台特定"代码处用，确保非目标平台不会运行时报错

### 设计稿

无 UI——降级是"见不到"的——但需要明确各平台**实际可用功能**对照表：

| 功能 | Win11 | Win10 | macOS 14+ | Ubuntu 24.04 |
|---|---|---|---|---|
| Mica 主窗背景 | ✓ | ✗ (Acrylic 降级) | ✗ (纯色) | ✗ (纯色) |
| 系统托盘 | ✓ | ✓ | ✓ (NSStatusBar) | ✗ (关窗即退) |
| 关窗最小化到托盘 | ✓ | ✓ | ✓ (默认行为) | ✗ |
| Toast 通知 | ✓ | ✓ | ✓ (NSUserNotification) | △ (libnotify 大致可用) |
| 右键 Shell 集成 | ✓ | ✓ | ✗ (Phase 3 不做) | ✗ |
| 文件拖拽 | ✓ | ✓ | ✓ | ✓ |
| 多窗口 | ✓ | ✓ | ✓ | ✓ |

### 关键决策

- **Mac 比预期便宜**：Avalonia 在 Mac 上的 TrayIcon 直接落到 NSStatusBar，关窗不退是 macOS 默认；Mac 上托盘 / 关窗行为几乎免费——但右键 Shell 集成需要 sandbox + Service 注册，复杂度高，**Phase 3 跳过**
- **Linux 直接放弃托盘**：libappindicator 在 Ubuntu 24.04 + GNOME 上需要 extensions，KDE 上有但 API 不同；ROI 太低
- **不要写"if platform != windows then throw"**：所有平台特定调用要 try/catch + log，让用户在不支持的平台上"功能缺失但不崩"

### 验收

- Mac：`dotnet run` 启动主窗、能浏览媒体库 + 跑识别任务（核心功能）；关窗自动到 NSStatusBar；Mica 不显示但 UI 正常
- Linux：同上；关窗即退出；纯色背景

---

## 未解决问题

- **托盘控件包选型**：`H.NotifyIcon` 还是 Avalonia 内置 `TrayIcon`？前者功能强但 Avalonia 11 兼容性需验证，后者跨平台但菜单/通知能力弱。**Phase 3 第一周做技术选型 spike**，决定后再写代码。建议尝试顺序：Avalonia 内置 `TrayIcon`（如能满足需求是最简）→ H.NotifyIcon → 自实现
- **macOS 签名 + Notarization**：右键集成在 Mac 上做 Service 需要 sandbox；如果 [Phase 4](desktop-phase-4.md) 不打算花钱买 Apple 开发者账号，Mac 端 polish 只能 best-effort
- **Win11 紧凑右键菜单**：用户需要"显示更多选项"才能看到我们的 verb，有更新的 IExplorerCommand API 可以让 verb 直接进紧凑菜单——成本是写一个 packaged COM extension。Phase 3 不做，留观察用户反馈

## 下一步

进入 [Phase 4](desktop-phase-4.md)：打包与分发。
