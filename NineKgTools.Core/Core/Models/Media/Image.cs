using NineKgTools.Utils;
using System.Security.Cryptography;

namespace NineKgTools.Core.Models.Media;

public class Image
{
    public int Id { get; set; }
    
    /// <summary>
    /// 图片的名称（包括扩展名）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图片的URL
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// 图片的本地文件
    /// </summary>
    public FileInfo? File { get; set; }
    
    /// <summary>
    /// 图片的原始二进制内容
    /// </summary>
    public byte[]? Content { get; set; }
    
    /// <summary>
    /// 图片内容的哈希值，用于快速比较图片是否相同
    /// </summary>
    public string? Hash { get; set; }
    
    /// <summary>
    /// 图片宽度（像素）
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// 图片高度（像素）
    /// </summary>
    public int Height { get; set; }
    
    /// <summary>
    /// 图片关联的媒体
    /// </summary>
    public MediaBase? Media { get; set; }

    public Image Copy()
    {
        return new Image
        {
            Name = Name,
            Url = Url,
            File = File,
            Content = Content,
            Hash = Hash,
            Width = Width,
            Height = Height,
            Media = Media,
        };
    }

    public Image()
    {
    }

    public Image(Uri url)
    {
        Url = url;
    }

    public Image(FileInfo file)
    {
        File = file;
    }

    public Image(byte[] content, string fileName = "image.jpg")
    {
        Content = content;
        Name = fileName;
        // 计算并设置哈希值
        Hash = CalculateHash(content);
    }

    public Image(Uri? url, FileInfo? file)
    {
        Url = url;
        File = file;
    }

    public string GetOriginalName()
    {
        return Url != null ? Path.GetFileName(Url.LocalPath) : File?.Name ?? Name;
    }

    public string GetImageUrl()
    {
        if (!string.IsNullOrEmpty(Name))
        {
            return $"api/image/{Name}";
        }
        return Url != null ? Url.ToString() : StaticStrings.ImageNotFound;
    }

    /// <summary>
    /// 获取图片的宽高比字符串，用于CSS aspect-ratio
    /// </summary>
    public string GetAspectRatio()
    {
        if (Width > 0 && Height > 0)
        {
            return $"{Width} / {Height}";
        }
        return "1 / 1"; // 默认正方形比例
    }

    /// <summary>
    /// 获取图片的宽高比数值
    /// </summary>
    public double GetAspectRatioValue()
    {
        if (Height > 0)
        {
            return (double)Width / Height;
        }
        return 1.0; // 默认正方形比例
    }
    
    /// <summary>
    /// 计算图片内容的哈希值
    /// </summary>
    private static string CalculateHash(byte[] content)
    {
        if (content == null || content.Length == 0)
            return string.Empty;
            
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(content);
        return Convert.ToBase64String(hashBytes);
    }
}