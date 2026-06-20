# 14 桌面端安装

NineKgTools 有两种形态：浏览器访问的 **Web / Docker 服务端**，和本机原生的 **桌面端**（Avalonia，Win/Mac/Linux）。本页讲桌面端怎么装。

> 桌面端与 Web/Docker **数据完全独立**——它有自己的数据目录，不共享数据库。两者可以并存。

---

## 我该装哪个？

| 选 | 如果你… |
|---|---|
| **Docker / Web** | 想跑在服务器 / NAS，多设备共享，远程访问 |
| **桌面端** | 单机自用、想要系统集成（托盘 / 右键识别 / 拖拽 / 开机自启）、不想碰 Docker |

二者数据独立；你也可以两个都装，互不干扰。

---

## Windows

### 方式 A：安装版（推荐）

1. 到 [Releases](https://github.com/hphphp123321/NineKgTools/releases) 下载最新 `desktop-v*` 的 **`NineKgToolsDesktop-win-Setup.exe`**。
2. 双击安装（装到 `%LocalAppData%\NineKgToolsDesktop`，自动创建开始菜单 / 桌面快捷方式，无需管理员权限）。
3. **首次启动会被 SmartScreen 拦截**（应用未签名）：点「更多信息」→「仍要运行」即可。这是一次性的。
4. 走完 3 步首次启动引导（数据目录 / 监视文件夹 / 识别源）。

安装版支持**自动更新**：启动时静默检查，有新版会在主窗顶部提示，点「立即更新」即可（也可在 设置 → 应用 → 立即检查更新）。

### 方式 B：便携版（portable）

不想安装、想随身带数据的：

1. 下载 `NineKgToolsDesktop-win-Portable.zip`，解压到任意目录。
2. 双击 `NineKgTools.Desktop.exe`。
3. **想让数据随目录走？** 在 exe 同级**新建一个空文件 `.portable`**——有它则数据落到 exe 同目录的 `data/`（而非 `%LocalAppData%`），整个目录拷到别的机器即可带着数据走。不建则数据仍走 `%LocalAppData%`。
4. 便携版**不走自动更新**（要更新就下载新 zip 覆盖）。

---

## macOS（best-effort）

按你的芯片选包（Apple Silicon = `osx-arm64`，Intel = `osx`）：

- **安装版**：下载 `NineKgToolsDesktop-osx-arm64-Setup.pkg`（或 Intel 的 `NineKgToolsDesktop-osx-Setup.pkg`）→ **双击 `.pkg`** 按提示安装。
- **便携版**：下载 `NineKgToolsDesktop-osx-arm64-Portable.zip`（或 `NineKgToolsDesktop-osx-Portable.zip`）→ 解压后直接运行。
- **首次打开被 Gatekeeper 拦截**（未签名/未公证）：按住 Control 右键点应用图标 →「打开」→ 确认。一次性。

> macOS 为 best-effort 支持：Intel（osx）自动更新可用，**Apple Silicon（osx-arm64）的自动更新本期未接通**（手动下载新版即可）。

---

## Linux（best-effort）

1. 下载 `NineKgToolsDesktop.AppImage`。
2. 加可执行权限后运行：`chmod +x NineKgToolsDesktop.AppImage && ./NineKgToolsDesktop.AppImage`。
3. **依赖**：Skia 需要系统字体库，缺了会启动崩溃。装一下：
   - Ubuntu/Debian：`sudo apt install libfontconfig1`
   - Arch：`sudo pacman -S fontconfig`

---

## 数据目录在哪

| 平台 | 路径 |
|---|---|
| Windows | `%LocalAppData%\NineKgTools.Desktop\` |
| macOS | `~/Library/Application Support/NineKgTools.Desktop/` |
| Linux | `$XDG_DATA_HOME/NineKgTools.Desktop/`（默认 `~/.local/share/...`） |
| 便携版 | exe 同目录 `data/`（需 `.portable` 标记） |

里面有 `Database/`、`Logs/`、`Config/`、`.cache/`。**卸载安装版不会删这个目录**（它不在包私有目录里），重装即恢复数据。

---

## 常见问题

- **「SmartScreen 拦截怎么办？」** Win 安装方式 A 第 3 步：更多信息 → 仍要运行。
- **「Gatekeeper 拦截怎么办？」** macOS 第 3 步：Control 右键 → 打开。
- **「启动弹了个黑窗口？」** 旧版本（0.1.1 及更早）有此问题，更新到新版即修复。
- **「能同时装 Docker 和桌面端吗？」** 能，数据完全独立。
- **「自动更新不工作 / 显示『当前不是安装版』？」** 开发运行 / 便携版不走自动更新，只有安装版（Setup.exe）才行。
