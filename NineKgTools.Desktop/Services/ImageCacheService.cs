using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using NineKgTools.Core.Services.Images;
using NineKgTools.Desktop.Services.Messages;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 内存级 LRU 图片缓存。封面图从 ImageService 拉 Stream 后解码成 Avalonia Bitmap，
/// 在容量上限内（200 张，约 50–100MB）保留；超过容量驱逐最久未访问的条目。
/// 媒体编辑后，调用方通过 <see cref="WeakReferenceMessenger"/> 广播
/// <see cref="ImageInvalidatedMessage"/>，本服务订阅后驱逐对应缓存条目。
/// </summary>
public sealed class ImageCacheService : IDisposable
{
    private readonly ImageService _imageService;
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly LinkedList<string> _lruOrder = new();
    private readonly Dictionary<string, (LinkedListNode<string> Node, Bitmap Bitmap)> _cache = new();

    public ImageCacheService(ImageService imageService, int capacity = 200)
    {
        _imageService = imageService;
        _capacity = capacity;

        // 订阅图片失效广播，编辑保存后自动驱逐
        WeakReferenceMessenger.Default.Register<ImageInvalidatedMessage>(this, (recipient, msg) =>
        {
            ((ImageCacheService)recipient).Invalidate(msg.ImageName);
        });
    }

    /// <summary>命中即返；未命中走 ImageService 加载、解码、入缓存。</summary>
    public async Task<Bitmap?> GetOrLoadAsync(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;

        // 先尝试命中
        lock (_lock)
        {
            if (_cache.TryGetValue(imageName, out var entry))
            {
                _lruOrder.Remove(entry.Node);
                _lruOrder.AddFirst(entry.Node);
                return entry.Bitmap;
            }
        }

        // 未命中：加载 + 解码（IO + 解码不持锁）
        Bitmap? bitmap = null;
        try
        {
            await using var stream = await _imageService.GetImageByNameAsync(imageName);
            if (stream is null) return null;
            bitmap = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ImageCacheService 解码图片失败：{Name}", imageName);
            return null;
        }

        // 入缓存（再次检查避免并发重复加载）
        lock (_lock)
        {
            if (_cache.TryGetValue(imageName, out var existing))
            {
                bitmap.Dispose(); // 已经被别的线程加载，丢弃我们的副本
                _lruOrder.Remove(existing.Node);
                _lruOrder.AddFirst(existing.Node);
                return existing.Bitmap;
            }

            var node = _lruOrder.AddFirst(imageName);
            _cache[imageName] = (node, bitmap);

            // 驱逐最久未访问的，直到容量内
            while (_cache.Count > _capacity)
            {
                var oldestNode = _lruOrder.Last;
                if (oldestNode is null) break;
                _lruOrder.RemoveLast();
                if (_cache.Remove(oldestNode.Value, out var stale))
                {
                    stale.Bitmap.Dispose();
                }
            }
        }

        return bitmap;
    }

    /// <summary>
    /// 取"重模糊 + 降采样"版本的封面图——给 MediaDetailContent 玻璃背景用。
    /// 命中缓存即返；未命中走 <see cref="ImageService.GetBlurredImageBytesAsync"/> 一次性
    /// 算出 byte[]（ImageSharp GaussianBlur）→ 解码 Avalonia Bitmap → 入缓存。
    /// 与原图共用同一 LRU，key 加 "blur:" 前缀避免冲突。
    /// </summary>
    public async Task<Bitmap?> GetOrLoadBlurredAsync(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;

        var cacheKey = "blur:" + imageName;

        // 先尝试命中
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                _lruOrder.Remove(entry.Node);
                _lruOrder.AddFirst(entry.Node);
                return entry.Bitmap;
            }
        }

        // 未命中：调 Core 生成模糊 bytes（不持锁，避免阻塞其他读）
        Bitmap? bitmap = null;
        try
        {
            var bytes = await _imageService.GetBlurredImageBytesAsync(imageName);
            if (bytes is null) return null;
            using var ms = new MemoryStream(bytes);
            bitmap = new Bitmap(ms);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ImageCacheService 生成 / 解码模糊图失败：{Name}", imageName);
            return null;
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                bitmap.Dispose();
                _lruOrder.Remove(existing.Node);
                _lruOrder.AddFirst(existing.Node);
                return existing.Bitmap;
            }

            var node = _lruOrder.AddFirst(cacheKey);
            _cache[cacheKey] = (node, bitmap);

            while (_cache.Count > _capacity)
            {
                var oldestNode = _lruOrder.Last;
                if (oldestNode is null) break;
                _lruOrder.RemoveLast();
                if (_cache.Remove(oldestNode.Value, out var stale))
                {
                    stale.Bitmap.Dispose();
                }
            }
        }

        return bitmap;
    }

    /// <summary>
    /// 显式驱逐：媒体封面被编辑替换后，发广播让 UI 重新加载新版本。
    /// 同时驱逐对应的 "blur:" 缓存项——避免显示旧封面的模糊版。
    /// </summary>
    public void Invalidate(string imageName)
    {
        lock (_lock)
        {
            if (_cache.Remove(imageName, out var entry))
            {
                _lruOrder.Remove(entry.Node);
                entry.Bitmap.Dispose();
            }
            var blurKey = "blur:" + imageName;
            if (_cache.Remove(blurKey, out var blurEntry))
            {
                _lruOrder.Remove(blurEntry.Node);
                blurEntry.Bitmap.Dispose();
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var (_, entry) in _cache)
            {
                entry.Bitmap.Dispose();
            }
            _cache.Clear();
            _lruOrder.Clear();
        }
    }

    public int Count
    {
        get { lock (_lock) return _cache.Count; }
    }

    public void Dispose() => Clear();
}
