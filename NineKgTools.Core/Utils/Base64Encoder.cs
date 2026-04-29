using System.Text;

namespace NineKgTools.Utils;

public class Base64Encoder
{
    /// <summary>
    /// 将字符串转换为Base64字符串
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string EncodeStringToBase64(string text)
    {
        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(textBytes);
    }
    
    /// <summary>
    /// 将对应路径的文件转换为Base64字符串
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>base64编码</returns>
    /// <exception cref="FileNotFoundException">文件不存在</exception>
    public static string EncodeFileToBase64(string filePath)
    {
        // 确保文件存在
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定的文件未找到", filePath);
        }

        // 读取文件内容到字节数组
        byte[] fileBytes = File.ReadAllBytes(filePath);

        // 将字节数组转换为Base64字符串
        return Convert.ToBase64String(fileBytes);
    }
    
    public static string EncodeFileToBase64(FileInfo file)
    {
        return EncodeFileToBase64(file.FullName);
    }
}
