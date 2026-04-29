using System.Collections.Concurrent;
using Serilog;

namespace NineKgTools.Core.Services.Files;

/// <summary>
/// 文件/文件夹稳定性检测器 - 确保文件完全写入后再处理
/// </summary>
public class FileStabilityChecker
{
    private const int InitialDelayMs = 1500;
    private const int PollingIntervalMs = 2000;
    private const int RequiredStableChecks = 3;
    private const int MaxStabilityChecks = 60;
    private const int MaxLockRetries = 5;

    /// <summary>
    /// 防止同一路径重复检测
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 等待文件或文件夹稳定（完全写入）
    /// </summary>
    /// <param name="path">文件或文件夹路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>true 表示稳定可处理，false 表示应跳过</returns>
    public async Task<bool> WaitForStabilityAsync(string path, CancellationToken cancellationToken = default)
    {
        // 去重：同一路径只允许一个检测在进行
        if (!_pendingPaths.TryAdd(path, 0))
        {
            Log.Debug("路径已在稳定性检测中，跳过重复事件: {Path}", path);
            return false;
        }

        try
        {
            // 初始延迟，让操作系统完成基本的文件创建
            await Task.Delay(InitialDelayMs, cancellationToken);

            // 检查路径是否仍然存在（可能是临时文件已被删除）
            bool isDirectory = Directory.Exists(path);
            bool isFile = File.Exists(path);

            if (!isDirectory && !isFile)
            {
                Log.Debug("路径不存在，跳过: {Path}", path);
                return false;
            }

            // 大小稳定性轮询
            if (!await WaitForSizeStabilityAsync(path, isDirectory, cancellationToken))
            {
                return false;
            }

            // 对单文件额外进行文件锁探测
            if (isFile && !await WaitForFileLockReleaseAsync(path, cancellationToken))
            {
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            Log.Debug("稳定性检测被取消: {Path}", path);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "稳定性检测异常: {Path}", path);
            return false;
        }
        finally
        {
            _pendingPaths.TryRemove(path, out _);
        }
    }

    /// <summary>
    /// 等待文件/文件夹大小稳定
    /// </summary>
    private async Task<bool> WaitForSizeStabilityAsync(string path, bool isDirectory,
        CancellationToken cancellationToken)
    {
        long previousSize = -1;
        int previousFileCount = -1;
        int stableCount = 0;

        for (int i = 0; i < MaxStabilityChecks; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                long currentSize;
                int currentFileCount;

                if (isDirectory)
                {
                    var (size, count) = GetDirectoryMetrics(path);
                    currentSize = size;
                    currentFileCount = count;
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    if (!fileInfo.Exists)
                    {
                        Log.Debug("文件在稳定性检测期间被删除: {Path}", path);
                        return false;
                    }

                    currentSize = fileInfo.Length;
                    currentFileCount = 1;
                }

                // 检查是否与上次相同
                if (currentSize == previousSize && currentFileCount == previousFileCount)
                {
                    stableCount++;
                    if (stableCount >= RequiredStableChecks)
                    {
                        Log.Debug("路径已稳定 (大小: {Size}, 文件数: {Count}): {Path}",
                            currentSize, currentFileCount, path);
                        return true;
                    }
                }
                else
                {
                    stableCount = 0;
                }

                previousSize = currentSize;
                previousFileCount = currentFileCount;
            }
            catch (UnauthorizedAccessException)
            {
                // 复制过程中目录可能暂时无法访问，重置稳定计数
                stableCount = 0;
            }
            catch (IOException)
            {
                // 文件可能正在被写入，重置稳定计数
                stableCount = 0;
            }

            await Task.Delay(PollingIntervalMs, cancellationToken);
        }

        Log.Warning("路径稳定性检测超时 (等待 {Seconds} 秒): {Path}",
            MaxStabilityChecks * PollingIntervalMs / 1000, path);
        return false;
    }

    /// <summary>
    /// 获取目录的总大小和文件数
    /// </summary>
    private static (long totalSize, int fileCount) GetDirectoryMetrics(string directoryPath)
    {
        var dirInfo = new DirectoryInfo(directoryPath);
        if (!dirInfo.Exists) return (0, 0);

        try
        {
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            long totalSize = 0;
            foreach (var file in files)
            {
                try
                {
                    totalSize += file.Length;
                }
                catch (IOException)
                {
                    // 文件正在被写入，忽略
                }
            }

            return (totalSize, files.Length);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return (-1, -1); // 返回特殊值，表示无法读取，会导致稳定计数重置
        }
    }

    /// <summary>
    /// 等待文件锁释放（仅用于单文件）
    /// </summary>
    private async Task<bool> WaitForFileLockReleaseAsync(string filePath, CancellationToken cancellationToken)
    {
        for (int i = 0; i < MaxLockRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                // 成功打开，文件未被锁定
                return true;
            }
            catch (IOException)
            {
                Log.Debug("文件仍被锁定，重试 ({Retry}/{Max}): {Path}", i + 1, MaxLockRetries, filePath);
                await Task.Delay(PollingIntervalMs, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // 没有访问权限，但不代表文件在被写入
                return true;
            }
        }

        Log.Warning("文件锁检测超时: {Path}", filePath);
        return false;
    }
}
