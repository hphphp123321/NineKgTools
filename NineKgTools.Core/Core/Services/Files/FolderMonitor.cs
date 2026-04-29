using System.Collections.Concurrent;

namespace NineKgTools.Core.Services.Files;

public class FolderMonitor(FileSystemEventHandler onCreated, FileSystemEventHandler onDeleted)
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _fileSystemWatchers = new();

    public void MonitorFolder(string folderPath)
    {
        // 创建FileSystemWatcher实例
        var fileSystemWatcher = new FileSystemWatcher
        {
            Path = folderPath,           // 设置要监控的文件夹路径
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,  // 仅监控文件名和目录名变化
            Filter = "*.*",              // 监控所有文件和文件夹
            IncludeSubdirectories = false  // 不监控子目录中的变化，只监控第一级的文件和文件夹
        };
        
        // 注册事件处理器
        fileSystemWatcher.Created += onCreated;  // 当文件或文件夹创建时触发
        fileSystemWatcher.Deleted += onDeleted;  // 当文件或文件夹删除时触发
        
        // 启动监控
        fileSystemWatcher.EnableRaisingEvents = true;
        _fileSystemWatchers[folderPath] = fileSystemWatcher;

        // 移除阻塞循环 - FileSystemWatcher在后台线程池中处理事件
        // MonitorFolder方法现在立即返回，监控在后台持续运行
    }
    
    public void StopMonitorFolder(string folderPath)
    {
        if (_fileSystemWatchers.TryGetValue(folderPath, out var fileSystemWatcher))
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Dispose();
            _fileSystemWatchers.TryRemove(folderPath, out _);
        }
    }

    public void StopAllMonitoring()
    {
        foreach (var fileSystemWatcher in _fileSystemWatchers.Values)
        {
            fileSystemWatcher.EnableRaisingEvents = false;
            fileSystemWatcher.Dispose();
        }
        
        _fileSystemWatchers.Clear();
    }
}