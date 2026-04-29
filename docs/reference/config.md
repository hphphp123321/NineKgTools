# NineKgTools 配置参考文档

本文档详细介绍 NineKgTools 项目中所有配置项的作用、类型和默认值。

## 目录

- [概述](#概述)
- [配置组总览](#配置组总览)
- [app - 应用配置](#app---应用配置)
- [database - 数据库配置](#database---数据库配置)
- [log - 日志配置](#log---日志配置)
- [media - 媒体配置](#media---媒体配置)
- [ai - AI配置](#ai---ai配置)
- [source - 媒体源配置](#source---媒体源配置)
- [website - 网站配置](#website---网站配置)
- [files - 文件过滤配置](#files---文件过滤配置)
- [tasks - 任务配置](#tasks---任务配置)
- [tag_matching - 标签匹配配置](#tag_matching---标签匹配配置)
- [search - 搜索配置](#search---搜索配置)
- [identification - 识别配置](#identification---识别配置)
- [完整配置示例](#完整配置示例)

---

## 概述

### 配置文件位置

主配置文件位于项目根目录下的 `Config/config.yaml`。

### 配置加载机制

配置文件使用 YAML 格式，通过 `YamlDotNet` 库进行解析。系统按以下顺序查找配置文件：

1. 当前工作目录下的 `Config/config.yaml`
2. 应用程序基础目录下的 `Config/config.yaml`
3. 向上查找到解决方案根目录（最多5级）

配置类定义在 `NineKgTools.Core/Core/Services/Configs/` 目录下，主入口类为 `Config.cs`。

### 配置修改

可以通过以下方式修改配置：

- 直接编辑 `Config/config.yaml` 文件
- 在 Web 界面的设置页面 (`/settings`) 进行可视化配置

---

## 配置组总览

| 配置组 | 配置类 | 说明 |
|--------|--------|------|
| `app` | `AppConfig` | 应用基础配置（主机、端口、代理、UA）|
| `database` | `DatabaseConfig` | 数据库路径配置 |
| `log` | `LogConfig` | 日志输出配置 |
| `cache` | `CacheConfig` | 缓存根目录与识别缓存过期时间（启动前通过 yaml 固定，不在前端 Settings 页暴露） |
| `ai` | `AIConfig` | AI 和向量功能配置 |
| `source` | `SourceConfig` | 媒体源文件夹配置 |
| `website` | `WebsiteConfig` | 网站识别优先级配置 |
| `files` | `FilesConfig` | 文件过滤规则配置 |
| `tasks` | `TaskConfig` | 后台任务配置 |
| `tag_matching` | `TagMatchingConfig` | 标签匹配配置 |
| `search` | `SearchConfig` | 搜索功能配置 |
| `identification` | `IdentificationConfig` | 媒体识别配置 |

---

## app - 应用配置

应用程序的基础配置，包括 Web 服务器设置、代理配置等。

### web_host

- **类型**: `string`
- **默认值**: 无（必须配置）
- **说明**: Web 服务器监听的主机地址
- **使用位置**: `Program.cs` 配置 Kestrel 服务器
- **示例**:
  ```yaml
  web_host: 0.0.0.0  # 监听所有网络接口
  # 或
  web_host: "::"     # IPv6 监听所有接口
  # 或
  web_host: 127.0.0.1  # 仅本地访问
  ```

### web_port

- **类型**: `int`
- **默认值**: `23333`
- **说明**: Web 服务器监听的端口号
- **使用位置**: `Program.cs` 配置 Kestrel 服务器
- **示例**:
  ```yaml
  web_port: 23333
  ```

### proxy

代理服务器配置，用于网络请求（如访问 DLsite、Bangumi 等网站）。

#### proxy.proxy_addr

- **类型**: `string?` (可选)
- **默认值**: 空
- **说明**: 代理服务器地址，以 `http://` 或 `https://` 开头
- **使用位置**: `HttpClient` 配置
- **示例**:
  ```yaml
  proxy:
    proxy_addr: http://127.0.0.1:7890
  ```

#### proxy.proxy_user

- **类型**: `string?` (可选)
- **默认值**: 空
- **说明**: 代理服务器认证用户名
- **示例**:
  ```yaml
  proxy:
    proxy_user: username
  ```

#### proxy.proxy_password

- **类型**: `string?` (可选)
- **默认值**: 空
- **说明**: 代理服务器认证密码
- **示例**:
  ```yaml
  proxy:
    proxy_password: password123
  ```

### user_agent

- **类型**: `string?` (可选)
- **默认值**: 无
- **说明**: HTTP 请求使用的浏览器 User-Agent 标识
- **使用位置**: `HttpClient` 请求头配置，网站爬取服务
- **示例**:
  ```yaml
  user_agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36
  ```

---

## database - 数据库配置

SQLite 数据库文件路径配置。

### path

- **类型**: `string`
- **默认值**: `Database/database.db`
- **说明**: 主数据库文件保存路径（存储媒体、标签等数据）
- **使用位置**: `MediaDbContext` Entity Framework Core 数据库上下文
- **示例**:
  ```yaml
  path: Database/database.db
  ```

### hangfire_path

- **类型**: `string`
- **默认值**: `Database/hangfire.db`
- **说明**: Hangfire 后台任务调度数据库文件路径
- **使用位置**: Hangfire 任务调度服务
- **示例**:
  ```yaml
  hangfire_path: Database/hangfire.db
  ```

---

## log - 日志配置

Serilog 日志框架配置。

### log_types

- **类型**: `IEnumerable<LogType>`
- **可选值**: `Console`, `File`, `Server`
- **默认值**: 无（必须配置）
- **说明**: 日志输出类型，可同时配置多个
- **使用位置**: `Program.cs` Serilog 配置
- **示例**:
  ```yaml
  log_types:
    - Console  # 输出到控制台
    - File     # 输出到文件
  ```

### log_path

- **类型**: `string`
- **默认值**: 无
- **说明**: 日志文件保存目录路径（当 log_types 包含 File 时使用）
- **使用位置**: Serilog 文件 Sink 配置
- **示例**:
  ```yaml
  log_path: Logs/
  ```

### log_server

- **类型**: `string`
- **默认值**: 无
- **说明**: 日志服务器地址（当 log_types 包含 Server 时使用），如群晖 Syslog 服务器
- **使用位置**: Serilog 远程日志 Sink
- **示例**:
  ```yaml
  log_server: 192.168.1.100:514
  ```

### log_level

- **类型**: `LogEventLevel` (Serilog 枚举)
- **可选值**: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`
- **默认值**: 无
- **说明**: 日志记录的最低级别
- **使用位置**: Serilog 最小级别配置
- **示例**:
  ```yaml
  log_level: Debug
  ```

### log_template

- **类型**: `string`
- **默认值**: 无
- **说明**: 日志输出格式模板
- **使用位置**: Serilog 输出模板配置
- **示例**:
  ```yaml
  log_template: '{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Method}: {Message:lj} - {SourceFile}:{LineNumber}{NewLine}{Exception}'
  ```

---

## media - 媒体配置

媒体处理相关配置。

### cache_path

- **类型**: `string`
- **默认值**: 无
- **说明**: 媒体信息缓存路径，包括图片缓存等
- **使用位置**: 图片缓存服务、媒体缓存服务
- **示例**:
  ```yaml
  cache_path: .cache/
  ```

### cache_expiration_minutes

- **类型**: `int`
- **默认值**: `30`
- **说明**: 缓存过期时间（分钟）
- **使用位置**: 缓存服务
- **示例**:
  ```yaml
  cache_expiration_minutes: 30
  ```

---

## ai - AI配置

AI 功能和向量数据库配置。

### use_ai

- **类型**: `bool`
- **默认值**: 无
- **说明**: 是否启用 AI 功能
- **使用位置**: AI 相关服务的总开关
- **示例**:
  ```yaml
  use_ai: true
  ```

### use_ai_for_keyword_splitting

- **类型**: `bool`
- **默认值**: 无
- **说明**: 是否使用 AI 进行关键词切分
- **使用位置**: 搜索服务、关键词提取
- **示例**:
  ```yaml
  use_ai_for_keyword_splitting: true
  ```

### open_ai

OpenAI API 配置。

#### open_ai.api_key

- **类型**: `string?` (可选)
- **默认值**: 无
- **说明**: OpenAI API Key，如果为空会从环境变量 `OPENAI_API_KEY` 获取
- **使用位置**: OpenAI 服务配置
- **示例**:
  ```yaml
  open_ai:
    api_key: sk-xxxxxxxxxxxxxxxxxxxxxxxx
  ```

#### open_ai.api_version

- **类型**: `string`
- **默认值**: `v1`
- **说明**: OpenAI API 版本，即 base domain 后面的路径后缀
- **使用位置**: OpenAI API 请求 URL 构建
- **示例**:
  ```yaml
  open_ai:
    api_version: v1
  ```

#### open_ai.default_model

- **类型**: `string`
- **默认值**: `gpt-4o-mini`
- **说明**: 默认使用的 AI 模型
- **使用位置**: OpenAI API 请求
- **示例**:
  ```yaml
  open_ai:
    default_model: gpt-4o-mini
  ```

#### open_ai.base_domain

- **类型**: `string`
- **默认值**: `https://api.openai.com`
- **说明**: OpenAI API 基础域名，注意不能有域名后面的路径后缀
- **使用位置**: OpenAI API 请求 URL 构建
- **示例**:
  ```yaml
  open_ai:
    base_domain: https://api.openai.com
    # 或使用第三方代理
    base_domain: https://api.example.com/
  ```

### vector

向量功能配置（统一管理所有向量相关设置）。

#### vector.enable

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 向量功能总开关
- **使用位置**: 向量服务初始化
- **示例**:
  ```yaml
  vector:
    enable: true
  ```

#### vector.media

媒体向量配置。

##### vector.media.enable

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用媒体向量（包括索引和搜索）
- **使用位置**: 媒体向量服务
- **示例**:
  ```yaml
  vector:
    media:
      enable: true
  ```

##### vector.media.min_similarity

- **类型**: `double`
- **默认值**: `0.7`
- **说明**: 媒体向量搜索最小相似度阈值
- **取值范围**: 0-1
- **使用位置**: 媒体向量搜索服务
- **示例**:
  ```yaml
  vector:
    media:
      min_similarity: 0.7
  ```

#### vector.tag

标签向量配置。

##### vector.tag.enable

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用标签向量（包括匹配和搜索）
- **使用位置**: 标签向量服务
- **示例**:
  ```yaml
  vector:
    tag:
      enable: true
  ```

##### vector.tag.similarity_threshold

- **类型**: `double`
- **默认值**: `0.05`
- **说明**: 标签匹配相似度阈值
- **取值范围**: 0-1
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  vector:
    tag:
      similarity_threshold: 0.05
  ```

##### vector.tag.search_top_k

- **类型**: `int`
- **默认值**: `3`
- **说明**: 向量搜索返回的最大结果数
- **使用位置**: 标签向量搜索服务
- **示例**:
  ```yaml
  vector:
    tag:
      search_top_k: 3
  ```

#### vector.search

向量搜索配置。

##### vector.search.weight

- **类型**: `double`
- **默认值**: `0.6`
- **说明**: 向量搜索在综合搜索结果中的权重
- **取值范围**: 0-1
- **使用位置**: 混合搜索服务
- **示例**:
  ```yaml
  vector:
    search:
      weight: 0.6
  ```

#### vector.db

向量数据库配置。

##### vector.db.provider

- **类型**: `string`
- **默认值**: `sqlite`
- **说明**: 向量数据库提供者
- **使用位置**: 向量数据库服务初始化
- **示例**:
  ```yaml
  vector:
    db:
      provider: sqlite
  ```

##### vector.db.connection_string

- **类型**: `string`
- **默认值**: `Data Source=Database/vectors.db`
- **说明**: 向量数据库连接字符串
- **使用位置**: 向量数据库连接
- **示例**:
  ```yaml
  vector:
    db:
      connection_string: Data Source=Database/vectors.db
  ```

##### vector.db.dimension

- **类型**: `int`
- **默认值**: `1536`
- **说明**: 向量维度（需与嵌入模型输出维度匹配）
- **使用位置**: 向量数据库表创建
- **示例**:
  ```yaml
  vector:
    db:
      dimension: 1536
  ```

##### vector.db.batch_size

- **类型**: `int`
- **默认值**: `100`
- **说明**: 批量处理大小
- **使用位置**: 向量同步任务
- **示例**:
  ```yaml
  vector:
    db:
      batch_size: 100
  ```

---

## source - 媒体源配置

媒体源文件夹监控配置。

### watch_folders

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 监控的文件夹路径列表，系统会自动扫描这些文件夹中的媒体文件
- **使用位置**: 文件夹监控服务、媒体源扫描服务
- **示例**:
  ```yaml
  watch_folders:
    - F:\Videos
    - E:\Games
    - D:\Music
  ```

---

## website - 网站配置

网站识别优先级和各网站的具体配置。

### priority

不同媒体类型的网站识别优先级配置。

#### priority.audio

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 音频类型媒体的网站识别顺序
- **示例**:
  ```yaml
  priority:
    audio:
      - DLsite
  ```

#### priority.video

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 视频类型媒体的网站识别顺序
- **示例**:
  ```yaml
  priority:
    video:
      - DLsite
      - Bangumi
  ```

#### priority.game

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 游戏类型媒体的网站识别顺序
- **示例**:
  ```yaml
  priority:
    game:
      - DLsite
      - Bangumi
  ```

#### priority.text

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 文本类型媒体的网站识别顺序
- **示例**:
  ```yaml
  priority:
    text:
      - Bangumi
  ```

#### priority.picture

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 图片类型媒体的网站识别顺序
- **示例**:
  ```yaml
  priority:
    picture:
      - DLsite
      - Bangumi
  ```

#### priority.unknown

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 未知类型媒体的默认识别顺序
- **示例**:
  ```yaml
  priority:
    unknown:
      - DLsite
      - Bangumi
  ```

### dlsite

DLsite 网站配置。

#### dlsite.enable

- **类型**: `bool`
- **默认值**: 无
- **说明**: 是否启用 DLsite 网站识别
- **使用位置**: DLsite 服务
- **示例**:
  ```yaml
  dlsite:
    enable: true
  ```

#### dlsite.use_selenium_for_rating

- **类型**: `bool`
- **默认值**: 无
- **说明**: 是否使用 Selenium 获取评分（用于获取需要 JavaScript 渲染的评分数据）
- **使用位置**: DLsite 服务
- **示例**:
  ```yaml
  dlsite:
    use_selenium_for_rating: false
  ```

### bangumi

Bangumi 网站配置。

#### bangumi.enable

- **类型**: `bool`
- **默认值**: 无
- **说明**: 是否启用 Bangumi 网站识别
- **使用位置**: Bangumi 服务
- **示例**:
  ```yaml
  bangumi:
    enable: true
  ```

#### bangumi.api_key

- **类型**: `string`
- **默认值**: 空字符串
- **说明**: Bangumi API Key，从 https://next.bgm.tv/demo/access-token 申请
- **使用位置**: Bangumi API 请求
- **示例**:
  ```yaml
  bangumi:
    api_key: your_api_key_here
  ```

---

## files - 文件过滤配置

文件过滤规则配置，用于扫描媒体源时过滤不需要的文件。

### minimum_file_size

- **类型**: `long`
- **默认值**: `1024` (1KB)
- **说明**: 最小文件大小（字节），小于此大小的文件将被忽略
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  minimum_file_size: 0  # 不限制最小大小
  ```

### ignored_files

- **类型**: `List<string>`
- **默认值**: `["Thumbs.db", ".DS_Store", "desktop.ini", ".gitkeep", ".gitignore"]`
- **说明**: 忽略的文件名列表（精确匹配，不区分大小写）
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  ignored_files:
    - Thumbs.db
    - .DS_Store
    - desktop.ini
    - .gitkeep
    - .gitignore
  ```

### ignored_patterns

- **类型**: `List<string>`
- **默认值**: `[".*", "~*", "*.tmp", "*.temp", "*.cache", "*.log", "*.bak", "*.swp"]`
- **说明**: 忽略的文件名模式，支持简单通配符（`*` 匹配任意字符）
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  ignored_patterns:
    - .*        # 所有以点开头的文件
    - '*.tmp'   # 所有.tmp扩展名的文件
    - '*.temp'  # 所有.temp扩展名的文件
    - '*.cache' # 所有.cache扩展名的文件
    - '*.log'   # 所有.log扩展名的文件
    - '*.bak'   # 所有.bak扩展名的文件
    - '*.swp'   # vim临时文件
    - '*.swo'   # vim临时文件
  ```

### skip_hidden_files

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否跳过隐藏文件
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  skip_hidden_files: true
  ```

### skip_system_files

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否跳过系统文件
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  skip_system_files: true
  ```

### allowed_extensions

- **类型**: `List<string>`
- **默认值**: 空列表
- **说明**: 允许的文件扩展名列表，为空表示允许所有扩展名
- **使用位置**: 文件扫描服务
- **示例**:
  ```yaml
  allowed_extensions: []  # 允许所有扩展名
  # 或限制特定扩展名
  allowed_extensions:
    - .mp4
    - .mkv
    - .avi
  ```

---

## tasks - 任务配置

后台任务和定时任务配置。

### scheduled_tasks

- **类型**: `List<ScheduledTaskConfig>`
- **默认值**: 空列表
- **说明**: 定时任务配置列表
- **使用位置**: 定时任务调度服务

每个定时任务包含以下字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | `string` | 任务名称 |
| `type` | `string` | 任务类型（对应 `[ScheduledTask]` 特性的 Key）|
| `cron` | `string` | Cron 表达式 |
| `enabled` | `bool` | 是否启用 |
| `description` | `string?` | 任务描述 |
| `parameters` | `Dictionary<string, object>?` | 任务参数 |
| `timeout_override` | `int?` | 覆盖默认超时时间（分钟）|
| `priority` | `string?` | 任务优先级 |

**示例**:
```yaml
scheduled_tasks:
  - name: CacheCleanup
    type: CacheCleanup
    cron: 0 0 * * *  # 每天午夜执行
    enabled: true
    description: 清理未使用的图片缓存、更新图片哈希值并清理过期文件
    parameters:
      max_age_days: 0
  - name: TagVectorSync
    type: TagVectorSync
    cron: 0 0 */6 * *  # 每6小时执行
    enabled: true
    description: 同步标签数据到向量数据库
    parameters:
      batch_size: 100
      force_update: false
      cleanup_orphans: false
```

**可用的任务类型**:

| type | 说明 |
|------|------|
| `CacheCleanup` | 缓存清理任务 |
| `MediaCleanup` | 媒体清理任务 |
| `TagVectorSync` | 标签向量同步任务 |
| `MediaVectorSync` | 媒体向量同步任务 |

### retry_count

- **类型**: `int`
- **默认值**: `3`
- **说明**: 任务失败重试次数
- **使用位置**: 任务执行服务
- **示例**:
  ```yaml
  retry_count: 3
  ```

### max_concurrent_identification_tasks

- **类型**: `int`
- **默认值**: `5`
- **说明**: 最大并发识别任务数
- **使用位置**: 识别任务队列服务
- **示例**:
  ```yaml
  max_concurrent_identification_tasks: 5
  ```

### cache_cleanup

缓存清理配置。

#### cache_cleanup.max_age_days

- **类型**: `int`
- **默认值**: `0`
- **说明**: 缓存文件最大保留天数，0 表示不按时间清理
- **示例**:
  ```yaml
  cache_cleanup:
    max_age_days: 0
  ```

#### cache_cleanup.cleanup_image_cache

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否清理过期图片缓存
- **示例**:
  ```yaml
  cache_cleanup:
    cleanup_image_cache: true
  ```

#### cache_cleanup.cleanup_temp_files

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否清理临时文件
- **示例**:
  ```yaml
  cache_cleanup:
    cleanup_temp_files: true
  ```

---

## tag_matching - 标签匹配配置

标签匹配算法配置。

### enable_fuzzy_matching

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用模糊匹配
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  enable_fuzzy_matching: true
  ```

### similarity_threshold

- **类型**: `double`
- **默认值**: `0.7`
- **说明**: 相似度匹配阈值
- **取值范围**: 0-1
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  similarity_threshold: 0.7
  ```

### enable_contains_matching

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用包含匹配
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  enable_contains_matching: true
  ```

### enable_normalized_matching

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用规范化匹配
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  enable_normalized_matching: true
  ```

### max_match_results

- **类型**: `int`
- **默认值**: `5`
- **说明**: 返回的最大匹配结果数
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  max_match_results: 5
  ```

### log_match_details

- **类型**: `bool`
- **默认值**: `false`
- **说明**: 是否记录匹配详情（用于调试）
- **使用位置**: 标签匹配服务
- **示例**:
  ```yaml
  log_match_details: false
  ```

---

## search - 搜索配置

全局搜索功能配置。

### enable_global_search

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用全局搜索功能
- **使用位置**: 搜索服务
- **示例**:
  ```yaml
  enable_global_search: true
  ```

### enable_search_cache

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用搜索缓存以提高性能
- **使用位置**: 搜索服务
- **示例**:
  ```yaml
  enable_search_cache: true
  ```

### cache_expiration_minutes

- **类型**: `int`
- **默认值**: `5`
- **说明**: 搜索缓存过期时间（分钟）
- **使用位置**: 搜索缓存服务
- **示例**:
  ```yaml
  cache_expiration_minutes: 5
  ```

### max_concurrent_searches

- **类型**: `int`
- **默认值**: `10`
- **说明**: 最大并发搜索数量限制
- **使用位置**: 搜索服务
- **示例**:
  ```yaml
  max_concurrent_searches: 10
  ```

### default_max_results_per_type

- **类型**: `int`
- **默认值**: `20`
- **说明**: 每种类型默认最大结果数
- **使用位置**: 搜索服务
- **示例**:
  ```yaml
  default_max_results_per_type: 20
  ```

### default_min_relevance_score

- **类型**: `double`
- **默认值**: `0.3`
- **说明**: 默认最小相关性分数阈值，低于此分数的搜索结果将被过滤掉
- **取值范围**: 0-1
- **使用位置**: 搜索服务、各搜索策略
- **示例**:
  ```yaml
  default_min_relevance_score: 0.3
  ```

#### 阈值使用逻辑详解

**1. 配置覆盖机制**

在 `GlobalSearchService.SearchAsync` 中，如果搜索选项的 `MinRelevanceScore` 是默认值 0.3，会使用配置文件中的 `default_min_relevance_score` 值覆盖：

```csharp
// GlobalSearchService.cs:99-102
if (Math.Abs(options.MinRelevanceScore - 0.3) < 0.001)
{
    options.MinRelevanceScore = _searchConfig.DefaultMinRelevanceScore;
}
```

**2. 各搜索策略中的过滤位置**

| 搜索策略 | 过滤时机 | 代码位置 |
|----------|----------|----------|
| MediaSearchStrategy | 完成文本+向量搜索、应用过滤器后 | 第91行 |
| TagSearchStrategy | 合并智能匹配和文本搜索结果后 | 第117行 |
| CircleSearchStrategy | 计算匹配分数后立即判断 | 第70行 |
| CreatorSearchStrategy | 计算匹配分数后立即判断 | 第69行 |

**3. 相关性分数来源**

不同匹配类型产生的分数范围：

| 匹配类型 | 分数范围 | 说明 |
|----------|----------|------|
| 精确匹配 (Exact) | 1.0 | 名称完全相同 |
| 别名匹配 (Alias) | 0.95 | 别名完全匹配 |
| 开头匹配 | 0.8-0.9 | 查询词在目标开头 |
| 包含匹配 (Contains) | 0.5-0.7 | 目标包含查询词 |
| 模糊匹配 (Fuzzy) | 0-0.7 | 基于编辑距离计算 |
| 向量匹配 (Vector) | 0-1 | AI语义相似度 |
| 描述匹配 (Description) | 较低 | 在描述字段中匹配，权重0.6-0.7 |

**4. 字段权重系统**

`RelevanceScorer` 根据匹配字段应用不同权重：

```csharp
字段权重:
- title/name: 1.0 (最高)
- aliastitle/aliasname: 0.9
- summary: 0.7
- description: 0.6
- 其他字段: 0.5
```

**5. 混合搜索分数组合**

当同时启用文本搜索和向量搜索时，使用 `RelevanceScorer.CombineScores()` 组合分数：

```csharp
// 组合公式：textScore * (1 - vectorWeight) + vectorScore * vectorWeight
// 默认 vectorWeight = 0.6 (由 ai.vector.search.weight 配置)
```

**6. 调优建议**

| 场景 | 推荐阈值 | 说明 |
|------|----------|------|
| 精确搜索 | 0.7-0.9 | 只返回高度相关的结果 |
| 一般搜索 | 0.3-0.5 | 平衡精确度和召回率（默认） |
| 宽泛搜索 | 0.1-0.3 | 返回更多可能相关的结果 |
| 探索性搜索 | 0-0.1 | 几乎返回所有有匹配的结果 |

### search_timeout_seconds

- **类型**: `int`
- **默认值**: `30`
- **说明**: 搜索超时时间（秒）
- **使用位置**: 搜索服务
- **示例**:
  ```yaml
  search_timeout_seconds: 30
  ```

### text_search

文本搜索相关配置。

#### text_search.enable_highlighting

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 是否启用搜索结果高亮显示
- **使用位置**: 搜索结果渲染
- **示例**:
  ```yaml
  text_search:
    enable_highlighting: true
  ```

#### text_search.highlight_tag

- **类型**: `string`
- **默认值**: `<mark>`
- **说明**: 搜索结果高亮显示使用的 HTML 标签
- **使用位置**: 搜索结果渲染
- **示例**:
  ```yaml
  text_search:
    highlight_tag: <mark>
  ```

---

## identification - 识别配置

媒体识别默认配置。

### skip_cache

- **类型**: `bool`
- **默认值**: `false`
- **说明**: 是否跳过缓存，强制重新识别
- **使用位置**: 媒体识别服务
- **示例**:
  ```yaml
  skip_cache: false
  ```

### max_retries

- **类型**: `int`
- **默认值**: `3`
- **说明**: 识别失败时的最大重试次数
- **使用位置**: 媒体识别服务
- **示例**:
  ```yaml
  max_retries: 3
  ```

### timeout_seconds

- **类型**: `int`
- **默认值**: `30`
- **说明**: 单个网站的识别超时时间（秒）
- **使用位置**: 媒体识别服务
- **示例**:
  ```yaml
  timeout_seconds: 30
  ```

### min_similarity

- **类型**: `double`
- **默认值**: 无
- **说明**: 最小相似度阈值，低于此值的搜索结果将被忽略
- **取值范围**: 0-1
- **使用位置**: 网站识别搜索服务（DLsite、Bangumi）
- **示例**:
  ```yaml
  min_similarity: 0.6
  ```

### strategy

- **类型**: `IdentificationStrategy` (枚举)
- **可选值**: `Auto`, `Manual`, `Hybrid`, `ForceRefresh`, `CacheOnly`, `Quick`
- **默认值**: `Auto`
- **说明**: 识别策略
- **使用位置**: 媒体识别服务
- **策略说明**:
  - `Auto`: 自动选择最佳策略
  - `Manual`: 仅手动识别
  - `Hybrid`: 混合策略
  - `ForceRefresh`: 强制刷新，忽略缓存
  - `CacheOnly`: 仅使用缓存
  - `Quick`: 快速识别
- **示例**:
  ```yaml
  strategy: Auto
  ```

### website_priority_override

- **类型**: `List<string>?` (可选)
- **默认值**: 空
- **说明**: 覆盖默认的网站优先级列表
- **使用位置**: 媒体识别服务
- **示例**:
  ```yaml
  website_priority_override:
    - Bangumi
    - DLsite
  ```

### auto_add_to_database

- **类型**: `bool`
- **默认值**: `true`
- **说明**: 识别完成后是否自动添加到数据库
- **使用位置**: 媒体识别服务
- **示例**:
  ```yaml
  auto_add_to_database: true
  ```

---

## 完整配置示例

以下是一个完整的 `config.yaml` 配置文件示例：

```yaml
# Web应用设置
app:
  # 主机地址, 例如: ::
  web_host: 0.0.0.0
  # 端口
  web_port: 23333
  # 代理设置
  proxy:
    # 代理地址，以http://或https://开头
    proxy_addr:
    # 代理用户名
    proxy_user:
    # 代理密码
    proxy_password:
  # 浏览器User-Agent
  user_agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0

# 数据库配置
database:
  # 数据库文件保存路径
  path: Database/database.db
  # Hangfire数据库文件保存路径
  hangfire_path: Database/hangfire.db

# 日志配置
log:
  # 日志输出类型, 可选值: Console, File, Server
  log_types:
    - Console
    - File
  # 日志文件路径
  log_path: Logs/
  # 日志服务器地址, 例如群晖默认为'群晖ip:514'
  log_server: 192.168.1.100:514
  # 日志级别, 可选值: Verbose, Debug, Information, Warning, Error, Fatal
  log_level: Debug
  # 日志模板
  log_template: '{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u4}] {Method}: {Message:lj} - {SourceFile}:{LineNumber}{NewLine}{Exception}'

# 媒体相关配置
media:
  # 媒体信息缓存路径
  cache_path: .cache/
  # 缓存过期时间（分钟）
  cache_expiration_minutes: 30

# AI相关配置
ai:
  # 是否使用AI
  use_ai: true
  # 是否使用AI进行关键词切分
  use_ai_for_keyword_splitting: true
  # OpenAI配置
  open_ai:
    # OpenAI API Key
    api_key: sk-your-api-key-here
    # OpenAI API版本
    api_version: v1
    # 默认模型
    default_model: gpt-4o-mini
    # OpenAI API基础域名
    base_domain: https://api.openai.com/
  # 向量配置
  vector:
    # 是否启用向量功能
    enable: true
    # 媒体向量配置
    media:
      enable: true
      min_similarity: 0.7
    # 标签向量配置
    tag:
      enable: true
      similarity_threshold: 0.05
      search_top_k: 3
    # 向量搜索配置
    search:
      weight: 0.6
    # 向量数据库配置
    db:
      provider: sqlite
      connection_string: Data Source=Database/vectors.db
      dimension: 1536
      batch_size: 100

# 媒体源相关配置
source:
  # 监控文件夹
  watch_folders:
    - F:\Videos
    - E:\Games

# 网站相关配置
website:
  # 网站识别的优先级
  priority:
    audio:
      - DLsite
    video:
      - DLsite
      - Bangumi
    game:
      - DLsite
      - Bangumi
    text:
      - Bangumi
    picture:
      - DLsite
      - Bangumi
    unknown:
      - DLsite
      - Bangumi
  # Dlsite配置
  dlsite:
    enable: true
    use_selenium_for_rating: false
  # Bangumi配置
  bangumi:
    enable: true
    api_key: your_bangumi_api_key

# 文件相关配置
files:
  minimum_file_size: 0
  ignored_files:
    - Thumbs.db
    - .DS_Store
    - desktop.ini
    - .gitkeep
    - .gitignore
  ignored_patterns:
    - .*
    - '*.tmp'
    - '*.temp'
    - '*.cache'
    - '*.log'
    - '*.bak'
    - '*.swp'
    - '*.swo'
  skip_hidden_files: true
  skip_system_files: true
  allowed_extensions: []

# 任务相关配置
tasks:
  scheduled_tasks:
    - name: CacheCleanup
      type: CacheCleanup
      cron: 0 0 * * *
      enabled: true
      description: 清理未使用的图片缓存
      parameters:
        max_age_days: 0
    - name: MediaCleanup
      type: MediaCleanup
      cron: 0 0 4 * *
      enabled: true
      description: 清理无效的媒体记录
      parameters:
        clean_duplicates: false
    - name: TagVectorSync
      type: TagVectorSync
      cron: 0 0 */6 * *
      enabled: true
      description: 同步标签数据到向量数据库
      parameters:
        batch_size: 100
        force_update: false
        cleanup_orphans: false
    - name: MediaVectorSync
      type: MediaVectorSync
      cron: 0 30 */6 * *
      enabled: true
      description: 同步媒体数据到向量数据库
      parameters:
        batch_size: 100
        force_update: false
        cleanup_orphans: false
  retry_count: 3
  max_concurrent_identification_tasks: 5
  cache_cleanup:
    max_age_days: 0
    cleanup_image_cache: true
    cleanup_temp_files: true

# 标签匹配配置
tag_matching:
  enable_fuzzy_matching: true
  similarity_threshold: 0.7
  enable_contains_matching: true
  enable_normalized_matching: true
  max_match_results: 5
  log_match_details: false

# 搜索配置
search:
  enable_global_search: true
  enable_search_cache: true
  cache_expiration_minutes: 5
  max_concurrent_searches: 10
  default_max_results_per_type: 20
  default_min_relevance_score: 0.3
  search_timeout_seconds: 30
  text_search:
    enable_highlighting: true
    highlight_tag: <mark>

# 识别默认配置
identification:
  skip_cache: false
  max_retries: 3
  timeout_seconds: 30
  min_similarity: 0.6
  strategy: Auto
  website_priority_override:
  auto_add_to_database: true
```

---

## 相关链接

- [`tags.yaml` 标签字典参考](tags-yaml.md) — 内置标签字典的格式、加载时机与自定义指南
- [AI系统架构](../architecture/ai-system.md)
- [媒体识别流程](../architecture/media-identification-flow.md)
- [任务管理系统架构](../architecture/task-management-system.md)
- [前端设计指南](../development/frontend-design.md)
- [搜索系统指南](../development/search-system.md)
