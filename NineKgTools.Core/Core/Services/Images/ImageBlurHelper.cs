using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Serilog;

namespace NineKgTools.Core.Services.Images;

/// <summary>
/// SixLabors.ImageSharp 图像处理辅助 —— 单独文件单独 namespace 引入，避免与
/// <c>NineKgTools.Core.Models.Media.Image</c> 在 <see cref="ImageService"/> 内的命名冲突。
/// </summary>
internal static class ImageBlurHelper
{
    /// <summary>
    /// 把输入流的图像缩到 <paramref name="maxWidth"/>×<paramref name="maxHeight"/> 以内（保持比例，仅缩不放），
    /// 应用饱和度增强 + 高斯模糊，编码为 Jpeg byte[] 返回。失败时 Log Warning 并返回 null。
    ///
    /// **关键调参（v2 修复"看不出封面"问题）**：
    /// - 源图保留 900×1350（之前 400×600 太小，下采样本身就在丢识别度）
    /// - GaussianBlur σ=24（之前 60 把图磨成色场；24 是"软焦"级别保留主色块 + 轮廓）
    /// - Saturate(1.6) 抵消 blur 自带褪色 —— Apple Music / Spotify "Now Playing" 同款手法
    /// - 顺序：Resize → Saturate → GaussianBlur → SaveAsJpeg（饱和度在模糊前提升避免边缘 banding）
    /// </summary>
    public static async Task<byte[]?> BlurAndDownscaleAsync(
        Stream sourceStream,
        float blurRadius = 24f,
        int maxWidth = 900,
        int maxHeight = 1350,
        float saturation = 1.6f)
    {
        try
        {
            using var image = await Image.LoadAsync(sourceStream);

            var ratio = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
            if (ratio < 1.0)
            {
                var newW = Math.Max(1, (int)(image.Width * ratio));
                var newH = Math.Max(1, (int)(image.Height * ratio));
                image.Mutate(x => x.Resize(newW, newH));
            }

            image.Mutate(x => x.Saturate(saturation).GaussianBlur(blurRadius));

            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 85 });
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ImageBlurHelper 处理失败");
            return null;
        }
    }
}
