# `tags.yaml` 标签字典参考

> 本文件解释 `Config/tags.yaml` 的格式、加载时机、自定义方式以及它和 Web 标签管理之间的关系。

## TL;DR

- `Config/tags.yaml` 是项目的**初始标签字典**，**仅在数据库为空时自动灌入**
- 内置 **8 个一级分类（TopTag）+ 375 个二级标签（Tag）**，覆盖偏好 / 物品 / 角色 / 制服 / 剧情 / 玩法 / 外貌 / 异常 等维度
- **数据来源**：从老版本 DLsite 网站爬取整理而成（含 R18 标签），仅作为参考字典；与项目无任何官方关系，可任意删改替换
- 一旦应用启动并把标签写入数据库，**之后所有改动都走 Web UI（`/tags`）和数据库**，`tags.yaml` 不再被写回
- 想完全用自己的标签体系？删掉 `tags.yaml` 或留空 `tags: []`，应用启动就是空字典

## 它的角色

NineKgTools 的标签系统由两部分组成：

```
┌──────────────────┐        启动时一次性             ┌────────────────────┐
│  Config/tags.yaml │  ─────  从这里灌入  ────────►  │   SQLite 标签表    │
│   (初始字典/种子) │   仅当数据库为空时              │  TopTags / Tags    │
└──────────────────┘                                 └────────────────────┘
                                                              │
                                                              │ 运行时所有 CRUD
                                                              ▼
                                                     ┌────────────────────┐
                                                     │   Web UI /tags     │
                                                     │  + 标签映射 + 向量 │
                                                     └────────────────────┘
```

- **`tags.yaml`**：开发者预置的"开箱即用"标签库，目的是让新用户启动后直接有可用的中文标签可以选
- **数据库 (SQLite)**：运行时的真实数据源，识别和管理都基于它
- **同步策略**：**单向、一次性**——启动时 DB 空才灌入；之后再改 yaml 不会反映到 DB

加载逻辑见 [`NineKgTools.Core/Core/Services/Tags/TagService.cs`](../../NineKgTools.Core/Core/Services/Tags/TagService.cs) 的 `InitializeTagsDbFromYaml()`：

```csharp
// 如果数据库中不存在标签
if (!await _context.Tags.AnyAsync())
{
    var yamlFilePath = Config.FindConfigFile("tags.yaml");
    var tagTypes = await GetTagsFromYamlAsync(yamlFilePath);
    // ...灌入 TopTags / Tags 表
}
```

## YAML 结构

`tags.yaml` 是一份带 BOM 的 UTF-8 文件，根节点是 `tags:` 数组。每个数组元素是一个一级分类（**TopTag**），下面又包含若干二级标签（**Tag**）。

### 完整 schema

```yaml
tags:                                    # 根节点（数组）
  - id: 1                                # TopTag 的 id（仅 yaml 内部用，DB 是 auto increment）
    name: 偏好/需求                       # TopTag 名称（必填，DB 唯一键）
    tags:                                # 子标签数组（必填，可为空）
      - description: 3D作品是指通过…       # 子标签描述（可选，可空字符串）
        id: 1                            # 子标签 id（同上，仅 yaml 内部用）
        name: 3D作品                      # 子标签名称（必填，DB 唯一键）
      - description: ASMR（自发性…
        id: 2
        name: ASMR
      # ...
  - id: 2
    name: 物品/道具
    tags:
      # ...
```

### 字段含义

| 字段 | 必填 | 类型 | 说明 |
|---|---|---|---|
| `tags[]` | ✅ | array | 顶层数组，每个元素是一个 TopTag |
| `tags[].id` | ❌ | int | yaml 内部 id，DB 不使用（DB 自增）。可省略或填任意整数 |
| `tags[].name` | ✅ | string | 一级分类名，**DB 唯一键**——同名 TopTag 不会被重复添加 |
| `tags[].tags[]` | ✅ | array | 该一级分类下的子标签数组（可空） |
| `tags[].tags[].id` | ❌ | int | 同 TopTag.id，yaml 内部用 |
| `tags[].tags[].name` | ✅ | string | 子标签名，**DB 唯一键** |
| `tags[].tags[].description` | ❌ | string | 子标签描述，会在 Web UI 标签详情页显示，向量化时也作为 embedding 输入 |

### 内置的 8 个一级分类

| # | 名称 | 子标签数（约） | 用途 |
|---|---|---|---|
| 1 | 偏好/需求 | ~50 | 题材、风格倾向（治愈、致郁、ASMR 等） |
| 2 | 物品/道具 | ~30 | 作品中出现的物品（玩具、道具…） |
| 3 | 角色 | ~50 | 角色类型（妹妹、姐姐、御姐…） |
| 4 | 制服/衣着/职业 | ~40 | 服饰与职业（女仆、护士…） |
| 5 | 剧情/系统 | ~50 | 剧情走向 / 玩法机制 |
| 6 | 玩法/H倾向 | ~80 | （R18）玩法标签 |
| 7 | 外貌/身体特征 | ~40 | 体型、发色、特征 |
| 8 | 残酷系/异常系 | ~35 | （R18）极端 / 异常向标签 |

## 自定义指南

### 场景 1：完全用自己的字典（推荐用于非 R18 用途）

```bash
# 1. 备份原文件（可选）
cp Config/tags.yaml Config/tags.yaml.bak

# 2. 写一份最小起步字典
cat > Config/tags.yaml <<'YAML'
tags:
  - id: 1
    name: 类型
    tags:
      - id: 1
        name: 动作
        description: 强调动作场面与战斗的作品
      - id: 2
        name: 剧情
        description: 以叙事为核心的作品
  - id: 2
    name: 风格
    tags:
      - id: 3
        name: 治愈
        description: 让人放松、温暖的作品
YAML

# 3. 重置数据库让新字典生效（NINEKG_RESET_DB=true 详见 CLAUDE.md）
NINEKG_RESET_DB=true dotnet run --project NineKgTools.Web
```

### 场景 2：在内置字典基础上增删

启动一次后所有标签已经进 DB。**`tags.yaml` 不再权威**——后续操作都通过 Web UI：

- **新增**：访问 `/tags` 页面 → 选一级分类 → 添加子标签
- **删除 / 改名**：在 `/tag/{id}` 详情页直接编辑
- **批量映射**：`/tags/mappings` 页面管理"识别源标签 → 你的标签"映射规则

### 场景 3：完全清空（不要任何预置标签）

```yaml
tags: []
```

应用启动后将一个标签都没有，全靠你自己手动添加或识别过程中自动创建。

## 关于默认标签的说明

**默认的 `tags.yaml` 里的来源是从 **DLsite 老版本网站爬取整理**的字典。

如果你打算**把 NineKgTools 用于一般媒体管理**（家庭照片、动画收藏、独立游戏库等），强烈建议：

1. 启动前替换 `tags.yaml` 为自己的字典（场景 1）
2. 或启动后到 `/tags` 页面把不需要的分类删掉

**项目本身不涉黄、不分发任何资源**——`tags.yaml` 只是一份"什么标签会出现"的字典文件，不包含任何媒体内容或链接。

## 与向量搜索的关系

如果 `config.yaml` 里启用了向量搜索（`ai.vector.tag.enable: true`），应用会在标签首次入库时调用 OpenAI Embedding API 把每个标签的 `name + description` 向量化并存入 `Database/vectors.db`。

- 这意味着**标签描述写得详细**，识别时的"模糊标签匹配"会更准
- 修改 yaml 里的 description **不会**自动重算向量；可通过手动触发 `/tasks/scheduled` 里的 `TagVectorSync` 任务（或等定时任务每 6 小时跑一次）

完整向量管线见 [`docs/architecture/ai-system.md`](../architecture/ai-system.md)。

## 与识别流程的关系

媒体识别（DLsite / Bangumi / Steam）抓到的原始 tag 字符串，通过下面的策略匹配到你的标签库：

1. **精确匹配**：`name == 抓到的字符串`
2. **包含匹配**：`name` 是抓到字符串的子串，或反之
3. **规范化匹配**：去空格 / 大小写 / 全半角后比较
4. **模糊匹配**：编辑距离 ≥ 阈值
5. **向量匹配**：上面都不命中时，用 embedding 余弦相似度找最近标签

匹配阈值在 `config.yaml` → `tag_matching` 各开关下；映射规则可在 `/tags/mappings` 维护"硬规则"覆盖上述自动逻辑。

## 编辑提示

- **保留 BOM**：现有 `tags.yaml` 是 UTF-8 with BOM。多数 IDE 自动识别，但用 `cat` / `head` 看会有 `﻿` 字符。手写新文件时不带 BOM 也能 parse
- **缩进**：YAML 严格 2 空格缩进，**不要用 Tab**
- **id 字段**：可以省略，但 YamlDotNet 解析时会容忍冗余字段
- **description 多行**：用 yaml 的 `>` 或 `|` 多行字符串语法都行
- **校验**：`dotnet run --project NineKgTools.Web` 启动看 `[INFO] InitializeTagsDbFromYaml: 标签初始化完毕` 即成功，否则日志会打 yaml 解析错误

## 参考

- 加载实现：[`TagService.cs:331`](../../NineKgTools.Core/Core/Services/Tags/TagService.cs)
- 模型定义：[`NineKgTools.Core/Core/Models/Tags/Tag.cs`](../../NineKgTools.Core/Core/Models/Tags/Tag.cs)
- 向量化：[`TagService.cs:385`](../../NineKgTools.Core/Core/Services/Tags/TagService.cs) `InitializeTagVectorsAsync`
- 标签匹配策略：[`docs/search-system-guide.md`](../development/search-system.md)
- 配置参考：[`docs/config-reference.md`](config.md)
