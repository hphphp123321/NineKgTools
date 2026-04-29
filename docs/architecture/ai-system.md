# AI 系统架构文档

本文档详细说明 NineKgTools 项目中的 AI 功能和向量数据库系统的架构设计。

## 1. 系统概述

### 1.1 功能列表

NineKgTools 的 AI 系统提供以下核心功能：

| 功能 | 说明 | 核心服务 |
|------|------|----------|
| **文本嵌入** | 将文本转换为1536维向量，用于语义搜索 | VectorEmbeddingService |
| **智能关键词切分** | 从媒体文件名中提取结构化信息 | OpenaiService |
| **文本翻译** | AI驱动的多语言翻译 | OpenaiService |
| **向量相似度搜索** | 基于向量的语义搜索媒体和标签 | VectorService |
| **定时同步** | 自动同步媒体/标签到向量数据库 | MediaVectorSyncTask, TagVectorSyncTask |

### 1.2 技术架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        应用层 (Pages/Components)                 │
│              搜索页面、标签匹配、媒体识别等                        │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                     业务服务层                                   │
│   MediaService      TagService      GlobalSearchService          │
│   (媒体管理)        (标签管理)       (全局搜索)                   │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                        AI 服务层                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │  OpenaiService  │  │VectorEmbedding  │  │  MediaName      │  │
│  │  (API交互)      │  │    Service      │  │  SplitterService│  │
│  │                 │  │  (向量生成)      │  │  (分词服务)     │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      向量存储层                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │  VectorService  │  │  TagVector      │  │  MediaVector    │  │
│  │  (SQLite向量库) │  │  Collection     │  │  Collection     │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      外部服务                                    │
│           OpenAI API (text-embedding-ada-002, gpt-4o-mini)       │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 技术栈

| 组件 | 技术 | 说明 |
|------|------|------|
| SDK | Betalgo.Ranul.OpenAI | OpenAI API 客户端 |
| 向量存储 | Microsoft.SemanticKernel.Connectors.SqliteVec | SQLite向量扩展 |
| 向量抽象 | Microsoft.Extensions.VectorData.Abstractions | 向量数据抽象层 |
| 数值计算 | System.Numerics.Tensors | 向量相似度计算 |
| 缓存 | Microsoft.Extensions.Caching.Memory | 嵌入向量缓存 |

---

## 2. 配置项说明

### 2.1 AIConfig 配置类

**文件位置**: `NineKgTools.Core/Core/Services/Configs/AIConfig.cs`

```yaml
# Config/config.yaml
ai:
  use_ai: true                              # 是否启用AI功能（总开关）
  use_ai_for_keyword_splitting: true        # 是否使用AI进行关键词切分
  enable_vector_storage: true               # 是否启用向量存储

  open_ai:                                  # OpenAI配置
    api_key: ""                             # API Key（为空时读取环境变量OPENAI_API_KEY）
    api_version: "v1"                       # API版本（base domain后的后缀）
    default_model: "gpt-4o-mini"            # 默认模型
    base_domain: "https://api.openai.com"   # API基础域名（支持代理服务）

  vector_db:                                # 向量数据库配置
    provider: "sqlite"                      # 数据库提供者
    connection_string: "Data Source=Database/vectors.db"  # 连接字符串
    dimension: 1536                         # 向量维度
    batch_size: 50                          # 批处理大小
```

### 2.2 配置类定义

```csharp
// AIConfig.cs
public class AIConfig
{
    public bool UseAi { get; set; }                    // 是否启用AI
    public bool UseAiForKeywordSplitting { get; set; } // 是否使用AI关键词切分
    public OpenAi OpenAi { get; set; }                 // OpenAI配置
    public bool EnableVectorStorage { get; set; }      // 是否启用向量存储
    public VectorDbConfig VectorDb { get; set; }       // 向量数据库配置
}

// OpenAi.cs
public class OpenAi
{
    public string? ApiKey { get; set; }                // API密钥
    public string ApiVersion { get; set; } = "v1";     // API版本
    public string DefaultModel { get; set; } = "gpt-4o-mini";  // 默认模型
    public string BaseDomain { get; set; } = "https://api.openai.com";  // 基础域名
}

// VectorDbConfig.cs
public class VectorDbConfig
{
    public string Provider { get; set; } = "sqlite";   // 提供者
    public string ConnectionString { get; set; }       // 连接字符串
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";  // 嵌入模型
    public int Dimension { get; set; } = 1536;         // 向量维度
    public int BatchSize { get; set; } = 50;           // 批处理大小
}
```

### 2.3 相关功能开关

项目中其他配置类也包含与AI/向量相关的开关：

#### TagMatchingConfig（标签匹配配置）
```yaml
tag_matching:
  enable_vector_matching: true          # 是否启用向量匹配
  vector_similarity_threshold: 0.05     # 向量相似度阈值（0-1）
  vector_search_top_k: 3                # 向量搜索返回的最大结果数
```

#### SearchConfig（搜索配置）
```yaml
search:
  vector_search:
    enable_for_media: true              # 是否为媒体搜索启用向量搜索
    enable_for_tags: true               # 是否为标签搜索启用向量搜索
    vector_search_weight: 0.6           # 向量搜索在综合结果中的权重
    min_vector_similarity: 0.7          # 最小向量相似度阈值
```

#### MediaConfig（媒体配置）
```yaml
media:
  enable_vector_indexing: true          # 是否启用媒体向量索引
```

### 2.4 配置依赖关系

```
AI功能启用 (use_ai: true)
├── OpenAI配置 (api_key, base_domain, default_model)
│   ├── 测试连接
│   └── 获取模型列表
├── 向量存储 (enable_vector_storage: true)
│   ├── 向量数据库配置 (provider, connection_string, dimension)
│   ├── 媒体向量索引 (media.enable_vector_indexing)
│   ├── 标签向量匹配 (tag_matching.enable_vector_matching)
│   └── 搜索向量功能 (search.vector_search.*)
└── 关键词切分 (use_ai_for_keyword_splitting: true)
```

---

## 3. 核心服务

### 3.1 OpenaiService

**文件位置**: `NineKgTools.Core/Core/Services/AI/OpenaiService.cs`

OpenaiService 是与 OpenAI API 交互的核心服务类。

#### 初始化

```csharp
public OpenaiService(Config config, HttpService httpService)
{
    _config = config;
    _httpService = httpService;
    _ = Init();
}

public async Task Init()
{
    var aiConfig = _config.Ai.OpenAi;
    var apiKey = aiConfig.ApiKey;

    // 如果配置为空，尝试从环境变量获取
    if (string.IsNullOrEmpty(apiKey))
    {
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
    }

    if (apiKey == "")
    {
        Log.Debug("OpenAI API Key 为空, 将无法使用 OpenAI 相关功能");
        return;
    }

    _openAiService = new OpenAIService(
        settings: new OpenAIOptions
        {
            ApiKey = apiKey,
            ApiVersion = aiConfig.ApiVersion,
            BaseDomain = aiConfig.BaseDomain,
            DefaultModelId = aiConfig.DefaultModel,
        },
        httpClient: _httpService.GetDefaultHttpClient()
    );
}
```

#### 核心方法

| 方法 | 功能 | 参数 | 返回值 |
|------|------|------|--------|
| `Chat(string text)` | 基本对话 | 用户输入文本 | 模型回复 |
| `Translate(string text, string targetLanguage)` | 文本翻译 | 原文本、目标语言 | 翻译结果 |
| `Embed(string text)` | 单文本嵌入 | 文本内容 | 1536维向量 |
| `Embed(List<string> texts)` | 批量嵌入 | 文本列表 | 向量列表 |
| `SplitKeywords(string fileName)` | 关键词切分 | 媒体文件名 | MediaKeywords结构 |
| `TestAvailable()` | 测试连接 | 无 | 是否可用 |
| `GetModelList()` | 获取模型列表 | 无 | 可用模型列表 |

#### SplitKeywords 详解

该方法使用AI从媒体文件名中提取结构化信息：

```csharp
public async Task<MediaKeywords?> SplitKeywords(string fileName, CancellationToken cancellationToken = default)
```

**输入示例**:
```
[社团名] RJ12345 作品标题 ver1.0.zip
```

**输出结构**:
```csharp
public class MediaKeywords
{
    public string? ProductCode { get; set; }      // "RJ12345"
    public string? CircleName { get; set; }       // "社团名"
    public string? CleanedTitle { get; set; }     // "作品标题"
    public string? PrimaryKeyword { get; set; }   // 主关键词
    public List<string>? SecondaryKeywords { get; set; }  // 副关键词
    public Language DetectedLanguage { get; set; } // 检测到的语言
    public string? Version { get; set; }          // "ver1.0"
    public string? Date { get; set; }             // 发布日期
}
```

### 3.2 VectorService

**文件位置**: `NineKgTools.Core/Core/Services/Vectors/VectorService.cs`

VectorService 是 SQLite 向量数据库的核心服务，使用 `SqliteVec` 扩展实现向量存储和检索。

#### 架构设计

VectorService 采用部分类（partial class）设计，分为三个文件：
- `VectorService.cs` - 基类，初始化和通用方法
- `VectorService.Tags.cs` - 标签向量操作
- `VectorService.Media.cs` - 媒体向量操作

#### 初始化

```csharp
public VectorService(VectorDbConfig config)
{
    _config = config;

    // 确保数据库文件路径是绝对路径
    var connectionString = EnsureAbsolutePath(_config.ConnectionString);

    // 确保数据库目录存在
    EnsureDatabaseDirectoryExists(connectionString);

    // 创建 SQLite 向量存储
    _vectorStore = new SqliteVectorStore(connectionString);
}

public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    // 初始化标签集合
    _tagCollection = _vectorStore.GetCollection<string, TagVector>("Tags");
    await _tagCollection.EnsureCollectionExistsAsync(cancellationToken);

    // 初始化媒体集合
    _mediaCollection = _vectorStore.GetCollection<string, MediaVector>("Media");
    await _mediaCollection.EnsureCollectionExistsAsync(cancellationToken);

    _isInitialized = true;
}
```

#### 集合操作

**Tags 集合方法**（VectorService.Tags.cs）:
| 方法 | 功能 |
|------|------|
| `AddTagVectorAsync(TagVector)` | 添加标签向量 |
| `UpdateTagVectorAsync(TagVector)` | 更新标签向量 |
| `DeleteTagVectorAsync(string id)` | 删除标签向量 |
| `ExistsTagAsync(string id)` | 检查标签是否存在 |
| `SearchTagsAsync(embedding, topK, threshold)` | 搜索相似标签 |
| `AddBatchTagVectorsAsync(List<TagVector>)` | 批量添加 |

**Media 集合方法**（VectorService.Media.cs）:
| 方法 | 功能 |
|------|------|
| `AddMediaVectorAsync(MediaVector)` | 添加媒体向量 |
| `UpdateMediaVectorAsync(MediaVector)` | 更新媒体向量 |
| `DeleteMediaVectorAsync(string id)` | 删除媒体向量 |
| `GetMediaVectorByMediaIdAsync(int mediaId)` | 按媒体ID获取向量 |
| `SearchMediaAsync(embedding, topK, threshold)` | 搜索相似媒体 |
| `AddBatchMediaVectorsAsync(List<MediaVector>)` | 批量添加 |

### 3.3 VectorEmbeddingService

**文件位置**: `NineKgTools.Core/Core/Services/Vectors/VectorEmbeddingService.cs`

负责生成文本的向量嵌入，并提供缓存和相似度计算功能。

#### 核心功能

```csharp
public class VectorEmbeddingService
{
    private readonly OpenaiService _openaiService;
    private readonly IMemoryCache _cache;
    private readonly VectorDbConfig _config;

    /// <summary>
    /// 生成文本的向量嵌入（带缓存）
    /// </summary>
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"embedding:{GetHash(text)}";

        // 检查缓存
        if (_cache.TryGetValue<float[]>(cacheKey, out var cached))
        {
            return new ReadOnlyMemory<float>(cached);
        }

        // 调用 OpenAI API 生成嵌入
        var embedding = await _openaiService.Embed(text, cancellationToken);
        var floatArray = embedding.Select(d => (float)d).ToArray();

        // 缓存结果（24小时滑动过期）
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(24),
            Size = 1
        };
        _cache.Set(cacheKey, floatArray, cacheOptions);

        return new ReadOnlyMemory<float>(floatArray);
    }

    /// <summary>
    /// 计算两个向量的余弦相似度
    /// </summary>
    public double CalculateSimilarity(
        ReadOnlyMemory<float> vector1,
        ReadOnlyMemory<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            return 0;
        }

        // 使用 System.Numerics.Tensors 计算余弦相似度
        return TensorPrimitives.CosineSimilarity(vector1.Span, vector2.Span);
    }
}
```

#### 缓存机制

- **缓存键**: 使用 SHA256 哈希值作为缓存键
- **过期策略**: 24小时滑动过期
- **批量支持**: `GenerateBatchEmbeddingsAsync` 方法支持批量生成，自动跳过已缓存的文本

### 3.4 MediaNameSplitterService

**文件位置**: `NineKgTools.Core/Core/Services/Websites/MediaNameSplitterService.cs`

整合 AI 和传统分词方法的媒体名称分词服务。

#### 分词策略

```
优先级从高到低：
1. AI关键词切分（如果启用）
2. 传统分词降级（如果AI失败或未启用）
   - Jieba（中文分词）
   - MeCab（日文分词）
   - 正则表达式分割
```

---

## 4. 向量数据库架构

### 4.1 SQLite 向量存储

项目使用 `Microsoft.SemanticKernel.Connectors.SqliteVec` 实现向量存储，这是基于 SQLite 的向量扩展。

**特点**:
- 轻量级，无需额外部署
- 支持文件存储和内存存储
- 与 Microsoft Semantic Kernel 生态集成

**数据库文件位置**: `Database/vectors.db`

### 4.2 向量模型

#### BaseVector（基类）

**文件位置**: `NineKgTools.Core/Core/Models/Vectors/BaseVector.cs`

```csharp
public abstract class BaseVector
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;           // 记录唯一标识

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;         // 原始文本内容

    [VectorStoreVector(Dimensions: 1536)]
    public ReadOnlyMemory<float>? Embedding { get; set; }    // 向量嵌入

    [VectorStoreData]
    public string RecordType { get; set; } = string.Empty;   // 记录类型
}
```

#### TagVector（标签向量）

**文件位置**: `NineKgTools.Core/Core/Models/Vectors/TagVector.cs`

```csharp
public class TagVector : BaseVector
{
    public int TagId { get; set; }           // 标签ID
    public string TagName { get; set; }       // 标签名称
    public string? Description { get; set; }  // 标签描述
    public string? TopTagName { get; set; }   // 顶级标签名
    public int? TopTagId { get; set; }        // 顶级标签ID
}
```

#### MediaVector（媒体向量）

**文件位置**: `NineKgTools.Core/Core/Models/Vectors/MediaVector.cs`

```csharp
public class MediaVector : BaseVector
{
    public int MediaId { get; set; }              // 媒体ID
    public string MediaTitle { get; set; }        // 媒体标题
    public string MediaType { get; set; }         // 媒体类型 (Audio/Video/Game/Picture/Text)
    public string? CategoryName { get; set; }     // 分类名称
    public int? CategoryId { get; set; }          // 分类ID
    public string? CircleName { get; set; }       // 社团名称
    public int? CircleId { get; set; }            // 社团ID
    public string? Summary { get; set; }          // 简介
    public string? ReleaseDateString { get; set; } // 发布日期
    public double? Rating { get; set; }           // 评分
    public string? TagsJson { get; set; }         // 标签JSON
    public string? AliasesJson { get; set; }      // 别名JSON
}
```

### 4.3 ID 生成规则

向量记录的ID格式：`{entity_type}_{entity_id}`

- 标签向量: `tag_{tagId}` 例如 `tag_123`
- 媒体向量: `media_{mediaId}` 例如 `media_456`

### 4.4 向量文本构建策略

#### 媒体向量文本

**位置**: `MediaService.BuildMediaTextForVector()`

```csharp
public string BuildMediaTextForVector(MediaBase media)
{
    var parts = new List<string> { media.Title };

    // 添加别名
    if (media.AliasTitles.Any())
        parts.AddRange(media.AliasTitles);

    // 添加简介
    if (!string.IsNullOrWhiteSpace(media.Summary))
        parts.Add(media.Summary);

    // 添加分类
    if (media.Category != null)
        parts.Add($"分类:{media.Category.Name}");

    // 添加社团
    if (media.Circle != null)
        parts.Add($"社团:{media.Circle.Name}");

    // 添加标签
    if (media.Tags.Any())
        parts.Add($"标签:{string.Join(",", media.Tags.Select(t => t.Name))}");

    return string.Join(" ", parts);
}
```

#### 标签向量文本

**位置**: `TagService.BuildTagText()`

```csharp
private string BuildTagText(Tag tag)
{
    var parts = new List<string> { tag.Name };

    // 添加描述
    if (!string.IsNullOrWhiteSpace(tag.Description))
        parts.Add(tag.Description);

    // 添加顶级标签
    if (tag.TopTag != null)
        parts.Add($"类别:{tag.TopTag.Name}");

    return string.Join(" ", parts);
}
```

---

## 5. 定时同步任务

### 5.1 MediaVectorSyncTask

**文件位置**: `NineKgTools.Core/Core/Services/Tasks/ScheduledTasks/MediaVectorSyncTask.cs`

定期同步媒体数据到向量数据库。

#### 配置

```yaml
tasks:
  scheduled_tasks:
    - name: MediaVectorSync
      type: MediaVectorSync
      cron: "0 0 */6 * *"        # 每6小时执行一次
      description: 同步媒体数据库和向量数据库中的媒体数据
      parameters:
        batch_size: 100           # 批处理大小
        force_update: false       # 是否强制更新已有向量
        cleanup_orphans: false    # 是否清理孤立向量
```

#### 执行流程

```
1. 检查向量存储是否启用
2. 获取所有媒体数据（可按类型过滤）
3. 分批处理（默认100条/批）
4. 对每个媒体：
   - 检查向量是否存在
   - 不存在 → 新增
   - 存在且 force_update=true → 更新
5. 可选：清理孤立向量（媒体已删除但向量仍存在）
6. 记录统计信息（新增/更新/删除计数）
```

### 5.2 TagVectorSyncTask

**文件位置**: `NineKgTools.Core/Core/Services/Tasks/ScheduledTasks/TagVectorSyncTask.cs`

定期同步标签数据到向量数据库。

#### 配置

```yaml
tasks:
  scheduled_tasks:
    - name: VectorSync
      type: VectorSync
      cron: "0 0 */6 * *"
      description: 同步媒体数据库和向量数据库中的标签数据
      parameters:
        batch_size: 100
        force_update: false
        cleanup_orphans: false
```

#### 执行流程

```
1. 检查向量存储是否启用
2. 获取所有标签
3. 分批处理
4. 调用 TagService.SyncTagVectorAsync() 进行同步
5. 记录统计信息
```

---

## 6. 搜索整合

### 6.1 全局搜索服务

**文件位置**: `NineKgTools.Core/Core/Services/Search/GlobalSearchService.cs`

GlobalSearchService 整合了文本搜索和向量搜索，提供统一的搜索接口。

#### 搜索流程

```
1. 检查搜索缓存
2. 并行执行多种搜索：
   ├── 媒体搜索（MediaSearchStrategy）
   │   ├── 文本搜索（标题、别名匹配）
   │   └── 向量搜索（语义相似度）
   ├── 标签搜索（TagSearchStrategy）
   │   ├── 文本搜索（标签名匹配）
   │   └── 向量搜索（语义相似度）
   └── 社团/创作者搜索
3. 合并和排序结果
4. 缓存搜索结果
5. 返回
```

### 6.2 MediaSearchStrategy

**文件位置**: `NineKgTools.Core/Core/Services/Search/Strategies/MediaSearchStrategy.cs`

#### 向量搜索流程

```csharp
private async Task<List<SearchResultItem<MediaBase>>> PerformVectorSearchAsync(
    string query,
    GlobalSearchOptions options,
    CancellationToken cancellationToken)
{
    // 1. 生成查询向量
    var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(query);

    // 2. 向量相似度搜索
    var vectorResults = await _vectorService!.SearchMediaAsync(
        queryEmbedding,
        topK: options.MaxResultsPerType * 2,
        threshold: _searchConfig.VectorSearch.MinVectorSimilarity
    );

    // 3. 提取媒体ID
    var mediaIds = vectorResults
        .Where(r => r.Record != null)
        .Select(r => r.Record!.MediaId)
        .Distinct()
        .ToList();

    // 4. 批量加载媒体实体
    var mediaDict = await context.Medias
        .Include(m => m.Category)
        .Include(m => m.Tags)
        .Include(m => m.Circle)
        .Where(m => mediaIds.Contains(m.Id))
        .ToDictionaryAsync(m => m.Id);

    // 5. 构建搜索结果
    foreach (var vectorResult in vectorResults)
    {
        if (mediaDict.TryGetValue(vectorResult.Record.MediaId, out var media))
        {
            results.Add(new SearchResultItem<MediaBase>
            {
                Entity = media,
                RelevanceScore = vectorResult.Score,
                MatchType = SearchMatchType.Vector,
                MatchDetails = $"向量相似度: {vectorResult.Score:F3}"
            });
        }
    }

    return results;
}
```

### 6.3 搜索结果融合

向量搜索和文本搜索的结果会根据配置的权重进行融合：

```yaml
search:
  vector_search:
    vector_search_weight: 0.6    # 向量搜索权重60%
    # 文本搜索权重 = 1 - 0.6 = 40%
```

---

## 7. 使用指南

### 7.1 启用AI功能

1. **配置 OpenAI API Key**

   方式一：在配置文件中设置
   ```yaml
   ai:
     use_ai: true
     open_ai:
       api_key: "sk-your-api-key"
       base_domain: "https://api.openai.com"  # 或代理服务地址
   ```

   方式二：使用环境变量
   ```bash
   export OPENAI_API_KEY="sk-your-api-key"
   ```

2. **启用向量存储**
   ```yaml
   ai:
     enable_vector_storage: true
     vector_db:
       provider: "sqlite"
       connection_string: "Data Source=Database/vectors.db"
       dimension: 1536
   ```

3. **运行同步任务**

   首次启用后，需要手动或等待定时任务将现有数据同步到向量数据库。

### 7.2 前端配置界面

在设置页面（`/settings`）的"AI配置"标签页中可以进行可视化配置：

- **启用/禁用AI功能**: `use_ai` 开关
- **OpenAI配置**:
  - API Key 输入框
  - Base Domain 配置
  - 默认模型选择（支持自动获取可用模型列表）
  - API版本配置
- **向量存储设置**:
  - 启用向量存储开关
  - 嵌入模型配置
  - 批处理大小
- **测试功能**:
  - "测试AI连接"按钮：验证API配置是否正确
  - "刷新AI模型"按钮：动态获取可用的AI模型列表

### 7.3 服务注册

**文件位置**: `NineKgTools.Core/Core/Services/ServiceCollectionExtensions.cs`

```csharp
// 向量存储服务（条件注册）
services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<Config>();
    if (config.Ai?.EnableVectorStorage == true && config.Ai?.VectorDb != null)
    {
        var vectorDb = new VectorService(config.Ai.VectorDb);
        return vectorDb;
    }
    return null;
});

// 向量嵌入服务
services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<Config>();
    if (config.Ai?.EnableVectorStorage == true && config.Ai?.VectorDb != null)
    {
        var openaiService = provider.GetRequiredService<OpenaiService>();
        var cache = provider.GetRequiredService<IMemoryCache>();
        return new VectorEmbeddingService(openaiService, cache, config.Ai.VectorDb);
    }
    return null;
});

// OpenAI服务（无条件注册）
services.AddScoped<OpenaiService>();

// 媒体名称分词服务
services.AddScoped<MediaNameSplitterService>();
```

---

## 8. 关键特性总结

| 特性 | 实现 |
|------|------|
| **向量数据库** | SQLite + SqliteVec |
| **嵌入模型** | OpenAI text-embedding-ada-002 |
| **向量维度** | 1536维 |
| **相似度计算** | 余弦相似度（Cosine Similarity） |
| **缓存机制** | 24小时滑动过期内存缓存 |
| **同步机制** | Hangfire定时任务 |
| **搜索策略** | 文本搜索 + 向量搜索混合 |
| **集合数量** | 2个（Tags, Media） |
| **ID生成规则** | `{entity_type}_{entity_id}` |
| **批处理** | 默认50条/批 |
| **容错机制** | API调用失败自动降级 |

---

## 9. 文件索引

### 配置文件
- `Config/config.yaml` - 主配置文件
- `NineKgTools.Core/Core/Services/Configs/AIConfig.cs` - AI配置类

### AI服务
- `NineKgTools.Core/Core/Services/AI/OpenaiService.cs` - OpenAI API服务
- `NineKgTools.Core/Core/Services/Vectors/VectorService.cs` - 向量数据库服务
- `NineKgTools.Core/Core/Services/Vectors/VectorService.Tags.cs` - 标签向量操作
- `NineKgTools.Core/Core/Services/Vectors/VectorService.Media.cs` - 媒体向量操作
- `NineKgTools.Core/Core/Services/Vectors/VectorEmbeddingService.cs` - 向量嵌入服务
- `NineKgTools.Core/Core/Services/Websites/MediaNameSplitterService.cs` - 分词服务

### 向量模型
- `NineKgTools.Core/Core/Models/Vectors/BaseVector.cs` - 向量基类
- `NineKgTools.Core/Core/Models/Vectors/MediaVector.cs` - 媒体向量
- `NineKgTools.Core/Core/Models/Vectors/TagVector.cs` - 标签向量

### 定时任务
- `NineKgTools.Core/Core/Services/Tasks/ScheduledTasks/MediaVectorSyncTask.cs` - 媒体同步任务
- `NineKgTools.Core/Core/Services/Tasks/ScheduledTasks/TagVectorSyncTask.cs` - 标签同步任务

### 搜索服务
- `NineKgTools.Core/Core/Services/Search/GlobalSearchService.cs` - 全局搜索服务
- `NineKgTools.Core/Core/Services/Search/Strategies/MediaSearchStrategy.cs` - 媒体搜索策略
- `NineKgTools.Core/Core/Services/Search/Strategies/TagSearchStrategy.cs` - 标签搜索策略
