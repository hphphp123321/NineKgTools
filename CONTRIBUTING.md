# 贡献指南

感谢你有兴趣为 **NineKgTools** 做贡献！无论是修 bug、加功能、完善文档，还是提出想法，都非常欢迎。本文档帮你快速上手。

## 行为准则

参与本项目请遵守 [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)。

## 在开始之前

- 小改动（typo、文档、明显 bug）：直接开 PR
- 中大型改动（新功能、架构调整、破坏性变更）：**先开 Issue 讨论**，避免白工

## 本地开发环境

### 先决条件

- **.NET 9.0 SDK**（版本由 `global.json` 钉住为 9.0.100+）
- **Git**
- **Chrome / Chromium**（识别源用到 Selenium）
- 可选：Rider / Visual Studio / VS Code

### 首次启动

```bash
git clone https://github.com/hphphp123321/NineKgTools.git
cd NineKgTools

# 配置
cp Config/config.example.yaml Config/config.yaml
# 编辑 Config/config.yaml 填入 OpenAI / Bangumi key（或用环境变量）
$env:OPENAI_API_KEY = "sk-..."  # PowerShell

# 启动
dotnet run --project NineKgTools.Web
```

浏览器访问 `http://localhost:23333`。

> 更详细的开发细节（数据库迁移、Hangfire Dashboard、热重载、常见坑）请看 [`docs/development/README.md`](docs/development/README.md)。

### 运行测试

```bash
dotnet test
```

## 提交代码

### 分支命名

- `feature/<简短描述>` — 新功能
- `fix/<简短描述>` — bug 修复
- `refactor/<简短描述>` — 重构（不改行为）
- `docs/<简短描述>` — 文档
- `chore/<简短描述>` — 杂项（构建、CI、依赖升级）

### Commit Message 规范

采用约定式提交前缀 + 中文描述（参考现有 commit 历史）：

```
<type>: <简短中文描述>

[可选正文：背景、动机、实现思路]

[可选脚注：关联 issue 等]
```

常用 `<type>`：`feat` / `fix` / `refactor` / `docs` / `chore` / `test` / `perf` / `style`

**范例**：

```
refactor: 重构缓存配置并移除媒体配置

将 CacheConfig 抽出独立文件，CacheCleanupTask 参数化 max_age_days。
```

### Pull Request 流程

1. Fork 仓库，在你 fork 的仓库下基于 `main` 创建分支
2. 做改动，本地跑 `dotnet format` + `dotnet test` 全绿
3. 推到你 fork 的仓库，开 PR 到本仓库 `main`
4. 填 PR 模板里的各项（CI 会自动运行）
5. 根据 review 意见迭代
6. 维护者 squash-merge 进 `main`

### 代码风格

- 缩进/换行由根目录 [`.editorconfig`](.editorconfig) 统一管控，IDE 会自动应用
- 提交前跑 `dotnet format` —— CI 会用 `dotnet format --verify-no-changes` 把关
- C# 约定：文件作用域 namespace、启用 nullable、类型明显处用 `var`
- UI 约定见 [`CLAUDE.md`](CLAUDE.md)（`.razor` / `.razor.cs` 分离、CSS 不内联、MudBlazor 优先）

## 版本号约定（SemVer）

本项目遵循 [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html)：

| 变更类型 | 触发位 | 示例 |
|---|---|---|
| MAJOR | `config.yaml` schema 破坏、路由破坏、最低 .NET 版本变化 | 2.0.0 |
| MINOR | 新功能（新识别源、新媒体类型、新页面） | 1.1.0 |
| PATCH | bug 修复、性能改进、无破坏性调整 | 1.0.1 |

每个 PR 合入时在 [`CHANGELOG.md`](CHANGELOG.md) 的 `[Unreleased]` 小节追加对应条目，**不要**在 PR 里手动改版本号。

## 项目文档索引

- 架构与功能：[`docs/README.md`](docs/README.md)
- 开发指南：[`docs/development/README.md`](docs/development/README.md)
- 部署指南：[`docs/operations/deployment.md`](docs/operations/deployment.md)
- 前端设计：[`docs/development/frontend-design.md`](docs/development/frontend-design.md)
- 配置参考：[`docs/reference/config.md`](docs/reference/config.md)

## 分支保护（维护者参考）

`main` 分支在 GitHub Settings 中启用：

- Require status checks：`ci` / `format` / `codeql` 全绿
- 禁止 force-push
- 可选：最少 1 次 approval（单人维护时可关闭）

## 许可

本项目采用 [MIT License](LICENSE)。提交 PR 即视为同意以 MIT 协议贡献你的代码。

## 致谢

感谢所有贡献者！✨
