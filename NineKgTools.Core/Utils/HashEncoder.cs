using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace NineKgTools.Utils;


public static class HashEncoder
{
    // 使用 SHA-256 生成哈希值
    public static byte[] ComputeSha256Hash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }

    // 将 SHA-256 的字节数组转换为 Base62 字符串
    public static string ToBase62(byte[] hashBytes)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = new StringBuilder();

        // 对哈希值进行 Base62 编码
        var value = new BigInteger(hashBytes);
        if (value < 0) value = -value; // 确保是正值

        do
        {
            int remainder = (int)(value % 62);
            result.Insert(0, chars[remainder]);
            value /= 62;
        } while (value > 0);

        return result.ToString();
    }

    // 公开的方法，将输入字符串生成短的哈希字符串
    public static string EncodeToShortHash(string input)
    {
        byte[] hashBytes = ComputeSha256Hash(input);
        string base62Hash = ToBase62(hashBytes);
        // 取前 8-10 个字符，避免字符串太长
        return base62Hash.Length > 10 ? base62Hash.Substring(0, 10) : base62Hash;
    }
}