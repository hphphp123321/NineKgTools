using FluentAssertions;
using NineKgTools.Utils;
using Xunit;
using Xunit.Abstractions;

namespace NineKgTools.Tests.IntegrationTests;

/// <summary>
/// 关键词提取集成测试 - 测试真实场景的文件名
/// </summary>
public class KeywordExtractionIntegrationTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> RealWorldFileNames => new List<object[]>
    {
        new object[] { "[ILLUSION] RJ01081508 HoneySelect2 v1.2" },
        new object[] { "[20240101][サークル名] 作品名 (DL版)" },
        new object[] { "魔法少女まどか☆マギカ 第01話" },
        new object[] { "The_Witcher_3_Wild_Hunt_GOTY" },
        new object[] { "[梦之音汉化组] 游戏名 完全汉化版" },
        new object[] { "BJ566243_漫画名_全彩版" },
        new object[] { "VJ014316-美少女ゲーム" },
        new object[] { "中文作品名【完整版】" },
        new object[] { "Mixed混合Language语言_v2.1" },
        new object[] { "[KISS] カスタムオーダーメイド3D2" },
        new object[] { "RJ338582_あまあま男の娘ボイス_CV秋野かえで" },
        new object[] { "[同人音声] [RJ298398] お姉ちゃんと一緒" },
        new object[] { "(C97) [サークル名 (作者名)] 作品タイトル" },
        new object[] { "[ASL] 淫魔の繭 -沙耶編- [RJ309573]" },
        new object[] { "【AI少女】【璇玑公主】高品质MOD合集" }
    };
    
    [Theory]
    [MemberData(nameof(RealWorldFileNames))]
    public void RealWorldFileNames_ShouldBeProcessedCorrectly(string fileName)
    {
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.PrimaryKeyword.Should().NotBeNullOrEmpty($"文件名 '{fileName}' 应该有主关键词");
        result.CleanedTitle.Should().NotContain("[]", $"清理后的标题不应包含方括号");
        result.CleanedTitle.Should().NotContain("()", $"清理后的标题不应包含空圆括号");
        
        // 输出详细信息用于调试
        output.WriteLine($"文件名: {fileName}");
        output.WriteLine($"  主关键词: {result.PrimaryKeyword}");
        output.WriteLine($"  产品代码: {result.ProductCode ?? "无"}");
        output.WriteLine($"  社团名: {result.CircleName ?? "无"}");
        output.WriteLine($"  版本: {result.Version ?? "无"}");
        output.WriteLine($"  日期: {result.Date ?? "无"}");
        output.WriteLine($"  语言: {result.DetectedLanguage}");
        output.WriteLine($"  清理后: {result.CleanedTitle}");
        output.WriteLine($"  次要关键词: {string.Join(", ", result.SecondaryKeywords)}");
        output.WriteLine("---");
    }
    
    [Fact]
    public void DLsiteFormat_ShouldExtractAllComponents()
    {
        // Arrange
        var fileName = "[20240101][ILLUSION] RJ12345 ハニーセレクト2 v1.2 (DL版)";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Date.Should().Be("[20240101]");
        result.CircleName.Should().Be("ILLUSION");
        result.ProductCode.Should().Be("RJ12345");
        result.Version.Should().Be("v1.2");
        result.CleanedTitle.Should().Contain("ハニーセレクト");
        result.DetectedLanguage.Should().Be(Language.Japanese); // 有片假名，判定为日语
    }
    
    [Fact]
    public void BangumiFormat_ShouldExtractCorrectly()
    {
        // Arrange
        var fileName = "[Lilith-Raws] 無職転生 / Mushoku Tensei - 01 [Baha][WEB-DL][1080p][AVC AAC][CHT][MP4]";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("Lilith-Raws");
        result.CleanedTitle.Should().Contain("無職転生");
        result.CleanedTitle.Should().Contain("Mushoku Tensei");
        result.GetAllKeywords().Should().Contain(k => k.Contains("無職転生") || k.Contains("Mushoku"));
    }
    
    [Fact]
    public void ChineseTranslationGroup_ShouldExtractCorrectly()
    {
        // Arrange
        var fileName = "[梦之音汉化组] RJ345678 魔法少女 完全汉化版 v2.0";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("梦之音汉化组");
        result.ProductCode.Should().Be("RJ345678");
        result.Version.Should().Be("v2.0");
        result.CleanedTitle.Should().Contain("魔法少女");
        result.DetectedLanguage.Should().Be(Language.Chinese);
    }
    
    [Fact]
    public void ComicMarketFormat_ShouldExtractCorrectly()
    {
        // Arrange
        var fileName = "(C97) [サークル名 (作者名)] 同人誌タイトル (オリジナル)";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("サークル名 (作者名)"); // 整个方括号内容
        result.CleanedTitle.Should().Contain("同人誌タイトル");
        result.CleanedTitle.Should().Contain("オリジナル");
    }
    
    [Fact]
    public void MultipleProductCodes_ShouldExtractFirst()
    {
        // Arrange
        var fileName = "RJ111111 [Another Code BJ222222] 作品名";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.ProductCode.Should().Be("RJ111111"); // 应该提取第一个
    }
    
    [Fact]
    public void NoMetadata_ShouldStillWork()
    {
        // Arrange
        var fileName = "普通的文件名没有任何元数据";
        
        // Act
        var result = MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.ProductCode.Should().BeNull();
        result.CircleName.Should().BeNull();
        result.Version.Should().BeNull();
        result.Date.Should().BeNull();
        result.CleanedTitle.Should().Be("普通的文件名没有任何元数据");
        result.DetectedLanguage.Should().Be(Language.Chinese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
    }
}