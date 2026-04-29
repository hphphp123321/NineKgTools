<!--
感谢贡献！请填写以下内容。若有不适用的小节，请直接删除而非留空。
-->

## 摘要

<!-- 一两句话说清楚这个 PR 做了什么 -->

## 动机

<!-- 为什么做这个改动？解决什么问题？有没有关联的 Issue？ -->

Closes #<!-- issue 号，无关联可删除此行 -->

## 改动类型

<!-- 勾选适用项 -->

- [ ] 🐛 Bug 修复（不破坏现有功能）
- [ ] ✨ 新功能（不破坏现有功能）
- [ ] 💥 破坏性变更（配置 schema / 路由 / 行为变化）
- [ ] 📚 文档
- [ ] 🔧 构建 / CI / 杂项
- [ ] ♻️ 重构（不改行为）
- [ ] ⚡ 性能优化
- [ ] 🎨 代码风格 / UI 样式

## 实现要点

<!-- 关键文件、设计决策、有争议的取舍 -->

## 测试计划

<!-- 审阅人/维护者如何验证？至少一项必选 -->

- [ ] `dotnet test` 全绿
- [ ] `dotnet format --verify-no-changes` 通过
- [ ] 手动验证路径（具体步骤）：
  1. 
  2. 
- [ ] 新增/修改了自动化测试

## UI 截图

<!-- 仅 UI / 样式改动需要。可删除此小节。 -->

## Checklist

- [ ] 我已阅读 [CONTRIBUTING.md](../blob/main/CONTRIBUTING.md)
- [ ] Commit message 遵循项目约定（`type: 描述`）
- [ ] 已在 [CHANGELOG.md](../blob/main/CHANGELOG.md) 的 `[Unreleased]` 小节追加条目（除纯文档/CI 改动）
- [ ] 若改动了 `Config/config.example.yaml`，已在 `Settings.razor` 同步更新（见 CLAUDE.md 约定）
- [ ] 若改动影响架构或功能，已更新 `CLAUDE.md` 相应章节
