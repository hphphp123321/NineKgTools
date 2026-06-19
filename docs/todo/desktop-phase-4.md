# Desktop Phase 4：打包与分发

> **目标**：让普通用户可以"双击安装包"装上桌面端，无需 dotnet SDK / Rider / 命令行。Win 完整体验，Mac/Linux 提供能跑的二进制即可。
>
> **估时**：1–2 周
>
> **前置条件**：Phase 1–3 全部完成 + 三平台 smoke test 通过
>
> **视觉基线**：Phase 4 主要工程化，UI 极少；仅"首次启动引导"和"自动更新提示"两处需要设计。两者都遵守 [Phase 1 视觉设计基线](desktop-phase-1.md#视觉设计基线先读这一节)。

---

## 0. 实施状态（⚠ 实际采用 Velopack，下方原计划部分作废）

经评审，**放弃 MSIX/DMG/tarball 各自手搓 + 单独自动更新**，改为 **Velopack 统一方案**：一套 `vpk` 流水线在各 OS 产出安装包 + 便携 + 跨平台增量自动更新。**Windows 优先**端到端跑通，Mac/Linux best-effort。下面 §1（MSIX）/§3（DMG）/§4（tarball）/§6（自动更新）/§7（CI）的具体做法**已被 Velopack 取代**，保留仅作历史参考。

**关键决策（已落地）**：
- **packId = `NineKgToolsDesktop`**（无点号，≠ 数据目录 `NineKgTools.Desktop`，否则更新清数据）。
- **OutputType = `WinExe`**（修了"启动弹 console 窗、关窗杀进程"）。
- 不签名（SmartScreen/Gatekeeper 用户放行）、不上 Store。
- 仓库 `hphphp123321/NineKgTools`（无 `-public`）。
- 发布 tag `desktop-v*.*.*`（与 Web `v*` 隔离）。

**进度**：
- [x] 发布基础：csproj RID + `WinExe` + `build-desktop.sh/.ps1` + `build-icons.ps1` + `app-icon.png`
- [x] Portable：`.portable` 标记 → 数据落 exe 同目录
- [x] Velopack 集成：`VelopackApp.Build().Run()` + `UpdateService` + `NINEKG_UPDATE_FEED` 本地调试旁路
- [x] 自动更新 UI：主窗 `FAInfoBar` + Settings「应用」分组 + `UpdateProgressDialog`
- [x] 首次启动引导：`FirstRunWizardDialog`（3 步）+ `FirstRunCompleted` + Settings 重新运行
- [x] CI：`.github/workflows/desktop-release.yml`（OS 矩阵 vpk pack/upload）
- [x] 文档：`docs/user-guide/14-desktop-install.md` + README/deployment/CLAUDE §4.10
- [x] 本地验证：build/publish/`vpk pack`（0.1.0→0.1.1→0.1.2 delta）/安装/更新检查全链路
- [ ] 真·CI 实跑：推 `desktop-v*` tag 触发（待执行）
- [ ] 三平台 smoke test（需对应设备）

**未决项**：EV 证书（暂不买）；MS Store（暂不上）；macOS arm64 自动更新（独立 channel `osx-arm64`，客户端 channel 未接通，best-effort）；AppImage 之外的 Flatpak/Snap（不做）。

---

## 1. Windows MSIX 打包（重点）

MSIX 是 Win10/11 推荐的现代应用包格式，自动支持 Mica + 安装/卸载/更新生命周期。

### 任务

- [ ] **`Package.appxmanifest`**：应用标识 / 显示名 / 图标 / 文件类型关联
  - 标识：`NineKgTools.Desktop`，发行者信息（无证书时用临时 self-signed）
  - Capability：`broadFileSystemAccess`（媒体库可能在任意磁盘位置）
  - 文件关联（可选）：把 `.nkbundle` 之类自定义后缀关联到桌面端
- [ ] **图标资源**（详见设计稿）
- [ ] **`<Target Name="Pack">` MSBuild 任务**：`dotnet publish -c Release -r win-x64 --self-contained` + `makeappx.exe pack`
- [ ] **签名**：开发期用 `New-SelfSignedCertificate` 生成临时证书；正式发布要用 EV Code Signing 证书（成本约 $300/年）
- [ ] **自动更新**：spike 一周决定 Velopack vs MS Store（见未解决问题）

### 设计稿

```
图标资源全套（Visual Studio 风格命名，设计师可一键导出）
NineKgTools.Desktop/
└─ Assets/
   ├─ Square44x44Logo.scale-100.png  (44x44)
   ├─ Square44x44Logo.scale-125.png  (55x55)
   ├─ Square44x44Logo.scale-150.png  (66x66)
   ├─ Square44x44Logo.scale-200.png  (88x88)
   ├─ Square44x44Logo.scale-400.png  (176x176)
   ├─ Square150x150Logo.scale-100.png (150x150)
   ├─ Square150x150Logo.scale-200.png (300x300)
   ├─ Square310x310Logo.scale-100.png (310x310)
   ├─ Wide310x150Logo.scale-100.png  (310x150, 宽磁贴)
   ├─ SplashScreen.scale-100.png     (620x300)
   └─ AppIcon.ico                    (Win 旧式 ICO，含 16/32/48/256 多分辨率)

MSIX 包目录布局（pack 后产出）
NineKgTools.Desktop_1.0.0_x64.msix
└─ (内部结构)
   ├─ AppxManifest.xml
   ├─ Assets/                  ← 上面那一套图标
   ├─ NineKgTools.Desktop.exe
   ├─ NineKgTools.Desktop.dll
   ├─ Config/                  ← config.example.yaml + tags.yaml
   │  ├─ config.example.yaml
   │  └─ tags.yaml
   └─ runtimes/                ← .NET 9 SDK self-contained
      ├─ win-x64/
      └─ ...

Win11 开始菜单展示
┌──────────────────────┐
│ NineKgTools.Desktop  │  ← 应用名
│                      │
│       ◆              │  ← Square150x150Logo 图标
│                      │
└──────────────────────┘
```

### 关键决策

- **包标识** 选 `NineKgTools.Desktop` 而非 `NineKgTools`——明确区别于 Web 后端的镜像名，避免用户混淆
- **不上 MS Store（初期）**：Store 抽成 + 审核慢；GitHub Releases 自托管 MSIX 让用户右键安装即可
- **EV 证书 是否买** 见未解决问题——没证书的话首次启动会被 SmartScreen 拦截，需要用户点"仍要运行"
- **图标设计风格**：圆角矩形 + 简单图形 + 用户系统 accent 色作主色（与 Phase 1 视觉基线一致——纯 Win11 Fluent，不强加品牌色）；建议交给设计师产出 SVG 后批量导出 PNG
- **数据保留策略**：MSIX 卸载默认会清理 `LocalAppData\Packages\<PackageId>\` 目录——但我们的数据存在 `LocalAppData\NineKgTools.Desktop\`（不是包私有目录），卸载**不会**误删用户数据

### 验收

1. 双击 `.msix` 安装，开始菜单出现 NineKgTools 图标，点击启动正常
2. 卸载（设置 → 应用 → NineKgTools.Desktop → 卸载）→ `LocalAppData\NineKgTools.Desktop` 数据**保留**（验证不删用户数据）
3. 重装 + 启动 → 数据库 / 标签 / 媒体全部恢复
4. 没 EV 证书时首次启动 SmartScreen 拦截，文档说明"点'更多信息' → '仍要运行'"
5. Win11 任务栏右键应用图标 → 跳转列表（jump list）显示"打开主窗 / 退出"等托盘菜单同款项

---

## 2. Windows 单文件 portable 版本（备选）

不愿装 MSIX 的用户可以下载单 exe 直接跑。注意 portable 模式下 dataDir 改为 exe 同目录而非 LocalAppData。

### 任务

- [ ] **publish profile**：`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- [ ] **portable 模式检测**：exe 同目录有 `.portable` 标记文件 → `Program.GetPlatformDataDirectory()` 返回 exe 目录而非 LocalAppData
- [ ] **大小优化**：`-p:PublishTrimmed=true` 风险高（Avalonia 反射重）；先**不裁剪**保证稳定，60MB+ 单文件可接受

### 设计稿

```
portable 模式判定流程
┌────────────────────────────────────────────────────────┐
│  Program.GetPlatformDataDirectory()                    │
│   ├─ 检查 exe 同目录是否有 .portable 标记文件          │
│   │   ├─ 有  → 返回 exe 同目录 + "/data"               │
│   │   └─ 无  → 走 LocalAppData / Application Support / │
│   │           XDG_DATA_HOME 标准路径                    │
└────────────────────────────────────────────────────────┘

portable 包目录布局（用户下载后可任意拷贝）
NineKgTools-Portable/
├─ NineKgTools.Desktop.exe   (60MB+ self-contained)
├─ Config/                   (随 exe 发布)
│  ├─ config.example.yaml
│  └─ tags.yaml
├─ .portable                 (空文件，标记开关)
└─ data/                     (首次运行后自动创建)
   ├─ Database/
   ├─ Logs/
   └─ Config/
```

### 关键决策

- **portable 模式 zip 不解压自动跑**——用户必须解压，不能像免安装绿色软件那样从 zip 直接启动（self-contained .NET 限制）
- **`.portable` 文件由用户手动 touch**——不是默认值；防止安装版被误识别成 portable 而把数据写到 Program Files
- **不做 PublishTrimmed**：60MB 单文件在 2026 网络环境完全能接受；trimming 容易导致 Avalonia 运行时反射失败，得不偿失

### 验收

1. 下载单 exe → 双击启动，无需 dotnet SDK
2. exe 同目录 `touch .portable` → 重启 → 数据落到 exe 目录而非 LocalAppData
3. 整个目录拷贝到另一台 Win 电脑 → 数据完整迁移

---

## 3. macOS .app bundle（best-effort）

### 任务

- [ ] **publish**：`dotnet publish -c Release -r osx-x64` 和 `-r osx-arm64` 两套
- [ ] **`.app` 结构组装**（详见设计稿）
- [ ] **DMG 打包**：`create-dmg` CLI 或手写脚本
- [ ] **不做** 签名 / Notarization：用户首次打开会被 Gatekeeper 拦，需要右键 → 打开 → 确认（README 说明）
- [ ] **Universal Binary** （可选）：`lipo` 把 x64 + arm64 合并成单二进制；Phase 4 第一版分别提供两个 dmg，universal 留给 v2

### 设计稿

```
.app bundle 标准结构
NineKgTools.Desktop.app/
└─ Contents/
   ├─ Info.plist                  ← 应用元数据
   ├─ MacOS/
   │  └─ NineKgTools.Desktop      ← 二进制（标记为可执行）
   ├─ Resources/
   │  ├─ AppIcon.icns             ← Mac 多分辨率图标
   │  └─ Config/
   │     ├─ config.example.yaml
   │     └─ tags.yaml
   └─ Frameworks/
      └─ (空 - self-contained .NET 自带)

DMG 用户安装界面（macOS 标准）
┌─────────────────────────────────────┐
│  NineKgTools 1.0.0                  │
│                                     │
│      ◆                  📁          │
│      │                   ↑          │
│      └─────拖拽────→  Applications  │
│                                     │
│  把 NineKgTools 拖到右边即可安装    │
└─────────────────────────────────────┘
```

### 关键决策

- **不做 Notarization**——Apple Developer 账号 $99/年 + 证书 + xcrun notarytool 整套，对单人项目不值；用户首次打开按住 Ctrl 右键 → 打开 → 确认即可
- **README 必须显示这一步**——Mac 用户对未签名应用拦截已经习惯，但要明确告知操作步骤
- **Universal Binary 推后**：x64 + arm64 两个 dmg 分别下载会让用户多一步选择，但 universal 二进制大小翻倍。Phase 4 先两个 dmg，看反馈

### 验收

1. macOS 14+ Intel + Apple Silicon 各 smoke test 一遍：双击 dmg → 拖到 Applications → 启动 → 主窗显示 + 媒体库可读
2. 数据库自动落到 `~/Library/Application Support/NineKgTools.Desktop/`
3. 关窗后 Dock 图标常驻（macOS 默认行为）；菜单栏出现 ◆ 状态图标（[Phase 3 任务 1](desktop-phase-3.md#1-系统托盘windows-优先) 的 Mac 实现）

---

## 4. Linux 单文件二进制（best-effort）

### 任务

- [ ] **publish**：`dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true`
- [ ] **`.desktop` 文件**：让用户拷到 `~/.local/share/applications/` 后能在 GNOME / KDE 应用菜单看到
- [ ] **依赖项 README**：Skia 仍需要系统级 `libfontconfig` / `libX11` 等；Avalonia 文档列出依赖清单，README 写明
- [ ] **不做** AppImage / Flatpak / Snap：投入产出比低，Phase 4 不做

### 设计稿

```
Linux 安装包目录
NineKgTools-1.0.0-linux-x64/
├─ NineKgTools.Desktop          ← 单文件可执行
├─ NineKgTools.Desktop.desktop  ← Linux 启动器配置
├─ icon.png                     ← 256x256
├─ Config/
│  ├─ config.example.yaml
│  └─ tags.yaml
├─ install.sh                   ← 帮用户拷到标准位置
└─ README.md                    ← 依赖说明 + 安装指引

NineKgTools.Desktop.desktop 内容
[Desktop Entry]
Type=Application
Name=NineKgTools.Desktop
Comment=Personal media library manager
Exec=/path/to/NineKgTools.Desktop
Icon=/path/to/icon.png
Terminal=false
Categories=AudioVideo;Audio;Video;
StartupWMClass=NineKgTools.Desktop

install.sh 简单脚本（不强制使用）
#!/bin/bash
# 拷 desktop 文件到 ~/.local/share/applications
# 拷可执行文件到 ~/.local/bin
# chmod +x
# update-desktop-database
```

### 关键决策

- **不做 AppImage / Flatpak**：Avalonia 已经 self-contained 一个 60MB binary，再裹一层 AppImage 没本质收益；Flatpak 的 sandbox 反而妨碍我们读用户家目录的媒体文件
- **install.sh 提供但不强制**：Linux 用户偏好"自己掌控"，强制安装脚本反感；提供一份合规脚本但用户可以选择手动放
- **依赖清单要明确**：缺 libfontconfig 时启动会崩，错误信息晦涩——README 必须列出每个发行版的安装命令

### 验收

1. Ubuntu 24.04 LTS smoke test：`./NineKgTools.Desktop` 启动 → 主窗显示 → 媒体库可读
2. 数据库自动落到 `~/.local/share/NineKgTools.Desktop/`
3. `install.sh` 能正确拷贝 + 桌面应用菜单出现条目（GNOME / KDE 各测一遍）

---

## 5. 首次启动引导（First-Run Experience）

新用户装完桌面端启动后**不能一脸懵**——需要一份引导让他们快速从"装完"到"识别第一个媒体"。

### 任务

- [ ] **首次启动检测**：`dataDir/.first-run` 标记文件不存在 → 走 FirstRun 流程；走完后创建标记
- [ ] **引导窗口**：3 步向导
  - Step 1: 欢迎 + 数据目录确认
  - Step 2: 添加监视文件夹（可跳过）
  - Step 3: 网站配置（说明 Bangumi / Steam 默认配置可用，需要 Bangumi ApiKey 才能用 Bangumi）
- [ ] **完成后跳到 [Phase 1 任务 1](desktop-phase-1.md#1-主窗--navigationview-框架) 的首页**

### 设计稿

```
首次启动 - Step 1: 欢迎
┌────────────────────────────────────────────────┐
│ ◆ NineKgTools                          _ □ ✕  │
├────────────────────────────────────────────────┤
│                                                │
│         (大号图标 120x120)                     │
│                                                │
│         欢迎使用 NineKgTools 桌面端            │
│         你的本地媒体库管理器                   │
│                                                │
│         数据目录:                              │
│         ~/AppData/Local/NineKgTools.Desktop/   │
│         [打开数据目录]                         │
│                                                │
│         我们将在 3 步内帮你启动。              │
│                                                │
│                                                │
│                          [跳过] [下一步 →]     │
└────────────────────────────────────────────────┘

Step 2: 监视文件夹
┌────────────────────────────────────────────────┐
│  Step 2/3 · 添加你的媒体文件夹                 │
│                                                │
│  桌面端会监控这些文件夹的变化，自动识别新增   │
│  的媒体。你随时可以在「媒体源」页面添加更多。 │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │  [+ 添加文件夹]                          │ │
│  │                                          │ │
│  │  当前未添加文件夹                        │ │
│  └──────────────────────────────────────────┘ │
│                                                │
│  ⓘ 不知道选什么？可以先跳过，之后再添加。     │
│                                                │
│              [← 上一步] [跳过] [下一步 →]     │
└────────────────────────────────────────────────┘

Step 3: 网站配置
┌────────────────────────────────────────────────┐
│  Step 3/3 · 识别源                             │
│                                                │
│  桌面端通过这些网站识别媒体元数据：            │
│                                                │
│  🌸 DLsite      ●已启用  无需配置              │
│  🔵 Bangumi    ○未配置   需要 ApiKey [配置]   │
│  🟦 Steam       ●已启用  默认 us 区            │
│                                                │
│  ⓘ Bangumi 需要 ApiKey 才能用，可以先用       │
│     DLsite + Steam 跑起来，之后再配。          │
│                                                │
│              [← 上一步] [完成 ✓]               │
└────────────────────────────────────────────────┘
```

### 关键决策

- **3 步固定，不允许扩展**——超过 3 步用户会跑掉；额外配置都放 [Phase 2 Settings](desktop-phase-2.md#3-设置页settings)
- **每步都可跳过**——不强制配置，让用户先把界面看到，配置可以后置；"跳过"按钮始终在底部最右
- **完成动画**：完成后短暂显示 "✓ 准备就绪"全屏（300ms）后跳到首页——给一个完成感
- **标记文件 `.first-run`**：失败时不创建（用户下次启动重新走引导）；成功后创建（之后再不弹）；Settings 加"重新运行引导"按钮以备需要

### 验收

1. 全新装的桌面端首次启动 → 引导自动出现
2. 走完引导 → 标记文件创建 → 重启不再出现
3. 引导期间随时跳过 → 直接到首页
4. Settings"重新运行引导" → 删标记 → 下次启动重新走

---

## 6. 自动更新

### 任务

- [ ] **方案选型 spike**（第一周）：Velopack vs MS Store；本目录在 spike 完成后用决策更新
- [ ] **更新检查**：每次启动时静默检查新版本（HEAD 到 GitHub Releases API），新版可用时主窗顶部 InfoBar 提示
- [ ] **手动检查更新**：Settings 加"检查更新"按钮
- [ ] **下载 + 重启**：用户点击"立即更新" → 下载到 temp → 退出当前进程 → 安装包接管 → 启动新版

### 设计稿

```
有新版可用时主窗顶部 InfoBar
┌────────────────────────────────────────────────────────┐
│ ⓘ 新版本 1.1.0 可用 (当前 1.0.0)            [稍后][立即更新] │
└────────────────────────────────────────────────────────┘

点击"立即更新"后
┌────────────────────────────────────────┐
│  ◆ 正在更新到 1.1.0                    │
│                                        │
│   ████████░░░░░░░░░░░  43%             │
│                                        │
│   下载中... 12.3 MB / 28.4 MB          │
│                                        │
│   下载完成后将自动重启应用。           │
│                                        │
│                          [取消]        │
└────────────────────────────────────────┘

Settings "应用" 组里的更新区段
┌────────────────────────────────────────────────────┐
│  当前版本: 1.0.0                                   │
│                                                    │
│  ☑ 启动时自动检查更新                             │
│  ☐ 自动下载新版本（不自动安装）                   │
│                                                    │
│  上次检查: 2026-05-05 09:23                       │
│  [立即检查更新]                                    │
└────────────────────────────────────────────────────┘
```

### 关键决策

- **静默检查不阻塞启动**：`Task.Run(async () => CheckUpdateAsync())` 不 await；失败也无所谓（下次启动再试）
- **更新提示用 InfoBar 不用对话框**——不打断用户工作；Severity=`Informational`
- **不做"自动安装"**：用户点击才装；防止后台升级把用户正在做的事情中断
- **Velopack 倾向**：Velopack（前 Squirrel）是 .NET 生态当前最活跃的自动更新方案，与 MSIX 兼容性好；建议倾向 Velopack，Spike 阶段验证

### 验收

1. 启动桌面端 → 静默检查 → 模拟有新版 → 主窗顶部出现 InfoBar
2. 点击"立即更新" → 进度对话框 → 下载完成 → 自动重启 → 新版本号显示
3. Settings "立即检查更新" 工作正常
4. 网络故障时静默失败（日志记录），不弹错误对话框打扰用户

---

## 7. CI/CD 自动构建发布

### 任务

- [ ] **GitHub Actions workflow** `.github/workflows/desktop-release.yml`：
  - 触发：tag 推 `desktop-v*.*.*` 时
  - matrix：win-x64 / osx-x64 / osx-arm64 / linux-x64
  - 步骤：checkout → setup-dotnet 9 → publish → upload artifact → 创建 release 附件
- [ ] **版本号自动注入**：从 git tag 解析后填到 `csproj` 的 `<Version>` 和 `Package.appxmanifest`
- [ ] **changelog 自动生成**：对照上一个 desktop-v 标签的 commit 列表生成
- [ ] **Web 端独立 release pipeline**：不污染现有 docker 镜像 release，桌面端独立 workflow

### 设计稿

```
.github/workflows/desktop-release.yml 流程
┌──────────────────────────────────────┐
│  触发: git push tag desktop-v*.*.*   │
└──────────┬───────────────────────────┘
           │
           ▼
┌──────────────────────────────────────┐
│  矩阵 build                          │
│  ┌────────┬────────┬────────┬──────┐ │
│  │ win-x64│osx-x64 │osx-arm│linux │ │
│  └────────┴────────┴────────┴──────┘ │
└──────────┬───────────────────────────┘
           │
           ▼
┌──────────────────────────────────────┐
│  Each platform:                      │
│   1. dotnet publish                  │
│   2. platform-specific package       │
│      ├─ Win: makeappx → .msix        │
│      │       + portable .exe         │
│      ├─ Mac: hdiutil → .dmg          │
│      └─ Linux: tar.gz                │
│   3. Upload artifact                 │
└──────────┬───────────────────────────┘
           │
           ▼
┌──────────────────────────────────────┐
│  Release：collect artifacts          │
│   1. 创建 Release: desktop-v1.0.0   │
│   2. 上传 4 个 artifact              │
│   3. 自动 changelog 从 commit 生成   │
│   4. Notify (Slack/Discord 可选)     │
└──────────────────────────────────────┘
```

### 关键决策

- **tag 命名**：`desktop-v*.*.*` 不和 Web 端 docker 的 tag 冲突（如果 Web 用 `v*.*.*`）；命名前缀清晰
- **CI 不做 EV 签名**：CI runner 上签名需要把私钥上传到 GitHub Secrets，安全风险高；签名留给本地 release 流程或者 self-hosted runner
- **changelog 自动从 commit 生成**：用 [conventional-changelog](https://github.com/conventional-changelog/conventional-changelog)；项目 commit message 已经倾向规范化，开箱即用

### 验收

1. `git tag desktop-v0.1.0 && git push --tags` → 30 分钟内 GitHub Releases 出现 4 个 artifact（msix / exe / dmg / linux-binary）
2. release notes 自动包含 commit list

---

## 8. 安装文档 + 用户指南

### 任务

- [ ] **`docs/user-guide/14-desktop-install.md`**（新建）：分平台安装步骤 + 截图
- [ ] **`README.md`** 加桌面端入口段落：链接到下载页 + 一句话介绍"桌面端 vs Docker Web 的区别"
- [ ] **`docs/operations/deployment.md`**：澄清桌面端 ≠ Docker，部署方式不同
- [ ] **`docs/user-guide/01-getting-started.md`**：增加"如何选择 Web 端 vs 桌面端"章节

### 设计稿

```
docs/user-guide/14-desktop-install.md 文档结构
─────────────────────────────────────────────────
# 桌面端安装

## 我应该装哪个？
- Docker Web: 你想要服务器场景 / 多人共享 / 远程访问
- 桌面端:    你想要单机使用 / 与系统集成 / 不愿管 Docker

## Windows 安装
### MSIX (推荐)
   1. 下载 NineKgTools.Desktop_*.msix
   2. 双击安装
   3. 首次启动 SmartScreen 拦截 → 点"更多信息" → "仍要运行"
   4. 走过完成首次启动引导

### Portable
   1. 下载 NineKgTools-Portable.zip
   2. 解压到任意目录
   3. (可选) 创建 .portable 文件让数据落到 exe 同目录
   4. 双击 NineKgTools.Desktop.exe

## macOS 安装
   1. 下载对应你 Mac 的 dmg (Intel/Apple Silicon)
   2. 拖到 Applications
   3. 首次打开按住 Ctrl 右键 → "打开" → 确认 (绕过 Gatekeeper)

## Linux 安装
   1. 下载 NineKgTools-*-linux-x64.tar.gz
   2. 解压: tar xzf NineKgTools-*.tar.gz
   3. 装依赖: sudo apt install libfontconfig1 (Ubuntu)
                  pacman -S fontconfig (Arch)
   4. 跑 install.sh 或手动启动 ./NineKgTools.Desktop

## 数据迁移
- 桌面端数据目录与 Docker Web 完全独立
- 想从 Web 端迁移数据？详见 docs/operations/desktop-data-migration.md (待写)

## 常见问题
- "SmartScreen 拦截怎么办?" 见 Win 安装步骤 3
- "Gatekeeper 拦截怎么办?" 见 Mac 安装步骤 3
- "我能同时装 Docker 和桌面端吗?" 可以，二者数据完全独立

```

### 关键决策

- **"如何选择"段落** 必须开门见山——大多数用户在文档第一段就要决定走哪条路
- **截图至少 5 张**：Win 安装、首次启动引导 3 步、主窗截图、首次识别成功——给视觉印象
- **不写"如何贡献"**：那是 [`CONTRIBUTING.md`](../../CONTRIBUTING.md) 的事

### 验收

1. 完全不懂 .NET 的用户照着 14-desktop-install.md 在 Win11 装上并跑通"识别第一个媒体"流程
2. README.md 顶部 5 秒内能看到桌面端下载链接
3. docs/operations/deployment.md 顶部明确区分 Web/Docker 与桌面端

---

## 9. 三平台 Smoke Test Checklist

每次发布前手动跑一遍。

### Checklist

- [ ] **Win11**：MSIX 安装 → 启动 → 加监视文件夹 → 识别 → 主窗 + 详情窗 + 任务页都正常
- [ ] **macOS 14+**（Apple Silicon）：dmg 安装 → 同上
- [ ] **macOS 14+**（Intel）：dmg 安装 → 同上
- [ ] **Ubuntu 24.04 LTS**：单文件运行 → 同上
- [ ] **关窗重开任务续跑**（[Phase 1 任务 6](desktop-phase-1.md#6-hangfire-切换到-sqlite-持久化) Hangfire SQLite 持久化验证）—— 必须每平台验
- [ ] **数据目录隔离**：装一份 Docker Web + 一份桌面端，并行跑几小时无数据冲突
- [ ] **更新流程**：从前一个版本 1.0.0 升级到 1.0.1，配置 + 数据库都保留
- [ ] **首次启动引导**：在干净虚拟机上验证 3 步引导流畅可用
- [ ] **托盘 / 拖拽 / Shell 集成**：仅 Win 完整跑一遍，Mac 验托盘 + 拖拽，Linux 仅验拖拽

### 关键决策

- **每发布一次都跑全表**——这是手动测试，不能省；保证发布质量
- **虚拟机环境**：用 Hyper-V Win11 / Parallels macOS / VirtualBox Ubuntu，每次发布前清回 baseline；避免开发机上的脏环境干扰

### 验收

完成所有 checklist 项目，记录到 [`docs/operations/release-notes.md`](../operations/release-notes.md)（如果不存在则新建）。

---

## 未解决问题

- **EV Code Signing 证书** 是否买？没证书的 Win 用户首次打开会被 SmartScreen 阻拦，体验差。**决策点：Phase 4 第一周拍板**——如果项目不打算赚钱，靠 Smart App Control + 让用户白名单也能用
- **MS Store 上架** vs **GitHub Releases 自托管**：Store 触达广但审核慢、抽成、限制多；GitHub 自由但需要自己解决签名 + 自动更新。建议**先 GitHub Releases 一段时间，等用户量上来再考虑 Store**
- **Auto-update 选型**：Velopack vs MS Store。建议倾向 Velopack，Spike 阶段验证
- **是否做 Linux AppImage**：目前不做，但如果用户呼声大可以加；评估窗口建议 Phase 4 后 6 个月

---

## 完成态总览

桌面端从 Phase 0 到 Phase 4 全部完成后：
- ✅ Win/Mac/Linux 三平台原生体验
- ✅ 与 Web/Docker 数据完全隔离的本地版本
- ✅ Win 完整 Mica + 托盘 + 拖拽 + Shell 集成
- ✅ 自动更新 + 双击安装
- ✅ 与 Web 共享 95% 业务代码（Core 层），UI 各自独立维护
- ✅ 三平台 CI/CD 自动构建 + GitHub Releases 分发

后续路线图（Roadmap v2，超出本目录范围）可以考虑：
- 移动端（Avalonia 11 已支持 iOS / Android，主要是 UI 适配）
- 桌面 ↔ Docker Web 之间的数据同步协议（如果用户呼声大）
- 插件系统（让第三方扩展 IWebsite 识别源）
