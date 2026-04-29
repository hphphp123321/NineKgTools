using JiebaNet.Segmenter;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Utils;
using NMeCab.Specialized;
using Serilog;

namespace NineKgTools.Core.Services.Websites;

/// <summary>
/// 媒体名称分词服务，支持AI和传统方法，用于搜索
/// </summary>
public class MediaNameSplitterService
{
    private readonly Config _config;
    private readonly OpenaiService _openaiService;
    
    // 保留原有的静态分词器实例
    private static readonly Lazy<JiebaSegmenter?> ChineseSegmenter = new(() =>
    {
        try
        {
            return new JiebaSegmenter();
        }
        catch
        {
            return null;
        }
    });
    
    private static readonly Lazy<MeCabIpaDicTagger?> JapaneseTagger = new(() =>
    {
        try
        {
            return MeCabIpaDicTagger.Create();
        }
        catch
        {
            return null;
        }
    });
    
    public MediaNameSplitterService(Config config, OpenaiService openaiService)
    {
        _config = config;
        _openaiService = openaiService;
    }
    
    /// <summary>
    /// 从文件名中提取结构化的关键词信息
    /// </summary>
    public async Task<MediaKeywords> ExtractKeywordsAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // 检查是否启用AI切分
        if (_config.Ai.UseAi && _config.Ai.UseAiForKeywordSplitting)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Log.Debug("尝试使用AI切分关键词: {FileName}", fileName);
                var aiResult = await _openaiService.SplitKeywords(fileName, cancellationToken);
                if (aiResult != null)
                {
                    Log.Information("成功使用AI切分关键词");
                    return aiResult;
                }
                Log.Warning("AI切分关键词返回null，切换到传统方法");
            }
            catch (OperationCanceledException)
            {
                Log.Information("AI切分关键词操作已被取消: {FileName}", fileName);
                throw;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Error(ex, "AI切分关键词失败，切换到传统方法");
                }
            }
        }

        // 使用传统方法（调用静态方法）
        return MediaNameSplitter.ExtractKeywords(fileName);
    }
    
    /// <summary>
    /// 保留同步版本的方法，供不支持异步的场景使用
    /// </summary>
    public MediaKeywords ExtractKeywords(string fileName)
    {
        // 如果配置了AI但是在同步方法中调用，记录警告并使用传统方法
        if (_config.Ai.UseAi && _config.Ai.UseAiForKeywordSplitting)
        {
            Log.Warning("在同步方法中无法使用AI切分，将使用传统方法: {FileName}", fileName);
        }
        
        return MediaNameSplitter.ExtractKeywords(fileName);
    }
}