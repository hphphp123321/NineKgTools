namespace NineKgTools.Utils;

public static class StringExtensions
{
    // 处理文件非法字符
    public static string ReplaceInvalidChars(this string str)
    {
        foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
        {
            str = str.Replace(invalidFileNameChar, '_');
        }
        
        return str;
    }
}