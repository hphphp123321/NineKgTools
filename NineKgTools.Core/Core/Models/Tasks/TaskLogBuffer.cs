using System.Collections.Generic;
using System.Linq;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务日志缓冲区 - 带容量限制的环形缓冲区
/// 当超出容量时自动移除最早的条目
/// </summary>
public class TaskLogBuffer
{
    private readonly object _lock = new();
    private readonly LinkedList<TaskLogEntry> _entries;
    private readonly int _maxCapacity;

    /// <summary>
    /// 创建日志缓冲区
    /// </summary>
    /// <param name="maxCapacity">最大容量</param>
    public TaskLogBuffer(int maxCapacity = 200)
    {
        _maxCapacity = maxCapacity > 0 ? maxCapacity : 200;
        _entries = new LinkedList<TaskLogEntry>();
    }

    /// <summary>
    /// 添加日志条目
    /// </summary>
    /// <param name="entry">日志条目</param>
    public void Add(TaskLogEntry entry)
    {
        lock (_lock)
        {
            _entries.AddLast(entry);

            // 超出容量时移除最早的条目
            while (_entries.Count > _maxCapacity)
            {
                _entries.RemoveFirst();
            }
        }
    }

    /// <summary>
    /// 获取所有日志条目（按时间顺序）
    /// </summary>
    public IReadOnlyList<TaskLogEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }
}
