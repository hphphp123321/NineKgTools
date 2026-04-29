using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Websites;
using Xunit;
using Xunit.Abstractions;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Service;

public class MediaNameSplitterServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly MediaNameSplitterService _splitterService;
    private readonly Config _config;

    public MediaNameSplitterServiceTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 加载配置
        _config = new Config();
        _config.InitConfig().GetAwaiter().GetResult();
        
        // 创建服务
        var httpService = new HttpService(_config);
        var openaiService = new OpenaiService(_config, httpService);
        _splitterService = new MediaNameSplitterService(_config, openaiService);
    }

    [Fact]
    public async Task TestAiKeywordSplitting()
    {
        // 测试文件名
        var testFileNames = new[]
        {
            "[RJ01234567] 魔法少女的冒险 v1.0.5",
            "(C99) [サークル名] 夏日物語 (20240101)",
            "BJ567890 - Game Title - English Version",
            "【汉化组】【作品名】第一话【1080P】"
        };

        foreach (var fileName in testFileNames)
        {
            _output.WriteLine($"\n测试文件名: {fileName}");
            
            // 测试传统方法
            var traditionalResult = _splitterService.ExtractKeywords(fileName);
            _output.WriteLine($"传统方法 - 主关键词: {traditionalResult.PrimaryKeyword}");
            _output.WriteLine($"传统方法 - 产品代码: {traditionalResult.ProductCode}");
            _output.WriteLine($"传统方法 - 社团名: {traditionalResult.CircleName}");
            
            // 如果启用了AI，测试AI方法
            if (_config.Ai.UseAi && _config.Ai.UseAiForKeywordSplitting)
            {
                var aiResult = await _splitterService.ExtractKeywordsAsync(fileName);
                _output.WriteLine($"AI方法 - 主关键词: {aiResult.PrimaryKeyword}");
                _output.WriteLine($"AI方法 - 产品代码: {aiResult.ProductCode}");
                _output.WriteLine($"AI方法 - 社团名: {aiResult.CircleName}");
                _output.WriteLine($"AI方法 - 检测语言: {aiResult.DetectedLanguage}");
            }
        }
    }

    [Fact]
    public async Task TestAiVsTraditionalComparison()
    {
        // 临时启用AI切分进行测试
        var originalUseAi = _config.Ai.UseAiForKeywordSplitting;
        _config.Ai.UseAiForKeywordSplitting = false;
        
        var fileName = "[RJ01081508] ツンデレ彼女の甘い誘惑 (DL版) v2.1";
        
        // 传统方法
        var traditionalResult = _splitterService.ExtractKeywords(fileName);
        
        // AI方法（强制使用）
        _config.Ai.UseAiForKeywordSplitting = true;
        var aiResult = await _splitterService.ExtractKeywordsAsync(fileName);
        
        // 恢复原始设置
        _config.Ai.UseAiForKeywordSplitting = originalUseAi;
        
        // 输出比较结果
        _output.WriteLine($"原始文件名: {fileName}");
        _output.WriteLine("\n=== 传统方法 ===");
        _output.WriteLine($"主关键词: {traditionalResult.PrimaryKeyword}");
        _output.WriteLine($"产品代码: {traditionalResult.ProductCode}");
        _output.WriteLine($"社团名: {traditionalResult.CircleName}");
        _output.WriteLine($"清理标题: {traditionalResult.CleanedTitle}");
        _output.WriteLine($"语言: {traditionalResult.DetectedLanguage}");
        
        _output.WriteLine("\n=== AI方法 ===");
        _output.WriteLine($"主关键词: {aiResult.PrimaryKeyword}");
        _output.WriteLine($"产品代码: {aiResult.ProductCode}");
        _output.WriteLine($"社团名: {aiResult.CircleName}");
        _output.WriteLine($"清理标题: {aiResult.CleanedTitle}");
        _output.WriteLine($"语言: {aiResult.DetectedLanguage}");
        
        // 验证关键信息提取
        Assert.NotNull(aiResult.ProductCode);
        Assert.Equal("RJ01081508", aiResult.ProductCode);
    }
}