using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels.ModelResponseModels;
using Serilog;
using System.Text.Json;
using NineKgTools.Utils;

namespace NineKgTools.Core.Services.AI;

public class OpenaiService
{
    private OpenAIService? _openAiService;

    private readonly Config _config;

    private readonly HttpService _httpService;

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
        if (string.IsNullOrEmpty(apiKey))
        {
            // 尝试使用环境变量
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

        // var available = await TestAvailable();
        // if (!available)
        // {
        //     Log.Warning("OpenAI 服务不可用, 请检查配置项");
        // }
    }

    public async Task<bool> TestAvailable(CancellationToken token = default)
    {
        if (_openAiService == null)
        {
            return false;
        }

        var models = await GetModelList(token);
        return models.Count > 0;
    }

    public async Task<List<string>> GetModelList(CancellationToken token = default)
    {
        if (_openAiService == null)
        {
            return [];
        }

        ModelListResponse result = new();
        try
        {
            result = await _openAiService.Models.ListModel(token);
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
            {
                Log.Warning("OpenAI 获取模型列表失败, 错误信息: {ErrorMessage}", e);
            }

            return [];
        }

        if (result.Successful && result.Models.Count != 0) 
            return result.Models.Select(x => x.Id).ToList();
        
        if (!token.IsCancellationRequested)
        {
            Log.Warning("OpenAI 获取模型列表失败, 错误信息: {ErrorMessage}", result.Error?.Message);
        }
        return [];
    }

    public async Task<string?>? Chat(string text, CancellationToken cancellationToken = default)
    {
        if (_openAiService == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _openAiService.CreateCompletion(new ChatCompletionCreateRequest
        {
            Messages = new List<ChatMessage>()
            {
                ChatMessage.FromUser(text)
            },
        }, cancellationToken: cancellationToken);

        if (!result.Successful)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("OpenAI Chat 返回空值, 错误信息: {ErrorMessage}", result.Error?.Message);
            }
            return null;
        }

        return result.Choices.FirstOrDefault()?.Message.Content;
    }

    /// <summary>
    /// 使用AI将文本从一种语言翻译成另一种语言
    /// </summary>
    /// <param name="text">原文本</param>
    /// <param name="targetLanguage">目标语言，默认为"中文"</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>翻译后的文本，失败则返回null</returns>
    public async Task<string?> Translate(string text, string targetLanguage = "中文", CancellationToken cancellationToken = default)
    {
        if (_openAiService == null || string.IsNullOrEmpty(text))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 截取前1000个字符
        Log.Information("开始翻译文本: {Text}", text.Length > 1000 ? text[..1000] : text);

        try
        {
            var prompt = $"请将以下文本翻译成{targetLanguage}，只需返回翻译结果，不要解释或添加其他内容：\n\n{text}";

            var result = await _openAiService.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>()
                {
                    ChatMessage.FromSystem("你是一个专业的翻译助手，擅长准确、流畅地翻译文本。请直接返回翻译结果，不要添加任何额外解释。"),
                    ChatMessage.FromUser(prompt)
                },
                Temperature = 0.2f, // 降低随机性，使翻译更准确
                MaxTokens = 2048
            }, cancellationToken: cancellationToken);

            if (!result.Successful)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Warning("OpenAI 翻译失败, 错误信息: {ErrorMessage}", result.Error?.Message);
                }
                return null;
            }

            return result.Choices.FirstOrDefault()?.Message.Content?.Trim();
        }
        catch (OperationCanceledException)
        {
            Log.Information("翻译操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "OpenAI 翻译出错");
            }
            return null;
        }
    }

    public async Task<List<double>?>? Embed(string text, CancellationToken cancellationToken = default)
    {
        if (_openAiService == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _openAiService.Embeddings.CreateEmbedding(
            new EmbeddingCreateRequest
            {
                Input = text,
                Model = Betalgo.Ranul.OpenAI.ObjectModels.Models.TextEmbeddingAdaV2
            },
            cancellationToken: cancellationToken
        );

        if (!result.Successful || result.Data.Count == 0)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("OpenAI Embedding 返回空值, 错误信息: {ErrorMessage}", result.Error?.Message);
            }
            return null;
        }

        var data = result.Data.FirstOrDefault();
        if (!result.Successful || data == null)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("OpenAI Embedding 返回空值, 错误信息: {ErrorMessage}", result.Error?.Message);
            }
            return null;
        }

        return data.Embedding;
    }

    public async Task<List<List<double>>?>? Embed(List<string> texts, CancellationToken cancellationToken = default)
    {
        if (_openAiService == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _openAiService.Embeddings.CreateEmbedding(
            new EmbeddingCreateRequest
            {
                InputAsList = texts,
                Model = Betalgo.Ranul.OpenAI.ObjectModels.Models.TextEmbeddingAdaV2,
            },
            cancellationToken: cancellationToken
        );

        var data = result.Data;
        if (!result.Successful)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning("OpenAI Embedding 返回空值, 错误信息: {ErrorMessage}", result.Error?.Message);
            }
            return null;
        }

        return data.Select(x => x.Embedding).ToList();
    }

    /// <summary>
    /// 使用AI智能切分媒体文件名的关键词
    /// </summary>
    /// <param name="fileName">原始文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结构化的关键词信息</returns>
    public async Task<MediaKeywords?> SplitKeywords(string fileName, CancellationToken cancellationToken = default)
    {
        if (_openAiService == null || string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var systemPrompt = @"你是一个专业的媒体文件名分析助手，擅长从文件名中提取结构化信息。
请分析文件名并提取以下信息：
1. 产品代码（如RJ/BJ/VJ/RE/RG开头的编号）
2. 社团名/制作组名（通常在方括号[]或圆括号()中）
3. 作品主标题（去除多余信息后的核心标题）
4. 关键词列表（重要的关键词，用于搜索）
5. 语言类型（japanese/chinese/english/mixed）
6. 日期（如果有）
7. 版本号（如v1.0等）

返回JSON格式，所有字段都使用原始语言，不要翻译。";

            var userPrompt = $@"请分析以下文件名：
{fileName}

返回JSON格式：
{{
  ""product_code"": ""产品代码，如RJ12345，没有则为null"",
  ""circle_name"": ""社团名或制作组名，没有则为null"",
  ""title"": ""作品主标题"",
  ""keywords"": [""关键词1"", ""关键词2"", ...],
  ""language"": ""japanese/chinese/english/mixed"",
  ""date"": ""发布日期，没有则为null"",
  ""version"": ""版本号，没有则为null""
}}";

            var result = await _openAiService.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>()
                {
                    ChatMessage.FromSystem(systemPrompt),
                    ChatMessage.FromUser(userPrompt)
                },
                Temperature = 0.1f, // 降低随机性，使结果更稳定
                MaxTokens = 500,
                ResponseFormat = new ResponseFormat { Type = "json_object" }
            }, cancellationToken: cancellationToken);

            if (!result.Successful)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Warning("OpenAI 切分关键词失败: {ErrorMessage}", result.Error?.Message);
                }
                return null;
            }

            var jsonResponse = result.Choices.FirstOrDefault()?.Message.Content?.Trim();
            if (string.IsNullOrEmpty(jsonResponse))
            {
                return null;
            }

            // 解析JSON响应
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            var keywords = new MediaKeywords
            {
                ProductCode = root.TryGetProperty("product_code", out var productCode) && productCode.ValueKind != JsonValueKind.Null
                    ? productCode.GetString() : null,
                CircleName = root.TryGetProperty("circle_name", out var circleName) && circleName.ValueKind != JsonValueKind.Null
                    ? circleName.GetString() : null,
                CleanedTitle = root.TryGetProperty("title", out var title)
                    ? title.GetString() ?? fileName : fileName,
                Date = root.TryGetProperty("date", out var date) && date.ValueKind != JsonValueKind.Null
                    ? date.GetString() : null,
                Version = root.TryGetProperty("version", out var version) && version.ValueKind != JsonValueKind.Null
                    ? version.GetString() : null
            };

            // 处理关键词列表
            if (root.TryGetProperty("keywords", out var keywordsArray) && keywordsArray.ValueKind == JsonValueKind.Array)
            {
                var keywordsList = new List<string>();
                foreach (var keyword in keywordsArray.EnumerateArray())
                {
                    var kw = keyword.GetString();
                    if (!string.IsNullOrEmpty(kw))
                    {
                        keywordsList.Add(kw);
                    }
                }

                if (keywordsList.Count > 0)
                {
                    keywords.PrimaryKeyword = keywordsList[0];
                    if (keywordsList.Count > 1)
                    {
                        keywords.SecondaryKeywords = keywordsList.Skip(1).ToList();
                    }
                }
            }

            // 如果没有主关键词，使用清理后的标题
            if (string.IsNullOrEmpty(keywords.PrimaryKeyword))
            {
                keywords.PrimaryKeyword = keywords.CleanedTitle;
            }

            // 检测语言
            if (root.TryGetProperty("language", out var language))
            {
                var lang = language.GetString()?.ToLower();
                keywords.DetectedLanguage = lang switch
                {
                    "japanese" => Language.Japanese,
                    "chinese" => Language.Chinese,
                    "english" => Language.English,
                    _ => Language.Unknown
                };
            }

            Log.Information("AI成功切分关键词: {FileName} -> 主关键词: {Primary}, 产品代码: {Code}, 社团: {Circle}",
                fileName, keywords.PrimaryKeyword, keywords.ProductCode, keywords.CircleName);

            return keywords;
        }
        catch (OperationCanceledException)
        {
            Log.Information("切分关键词操作已被取消: {FileName}", fileName);
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "AI切分关键词出错: {FileName}", fileName);
            }
            return null;
        }
    }
}