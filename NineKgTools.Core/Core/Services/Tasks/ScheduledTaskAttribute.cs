using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks;

/// <summary>
/// 定时任务元数据特性
/// 用于声明定时任务的键名、显示名称和类型
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ScheduledTaskAttribute : Attribute
{
    /// <summary>
    /// 任务键名（唯一标识，用于配置文件和 API 调用）
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 任务类型枚举
    /// </summary>
    public TaskType TaskType { get; }

    public ScheduledTaskAttribute(string key, string displayName, TaskType taskType)
    {
        Key = key;
        DisplayName = displayName;
        TaskType = taskType;
    }
}

/// <summary>
/// 任务元数据（只读）
/// </summary>
public record ScheduledTaskMetadata
{
    public string Key { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public TaskType TaskType { get; init; }
    public Type ImplementationType { get; init; } = null!;
}
