using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks;

/// <summary>
/// 定时任务工厂 - 使用反射自动发现带有 [ScheduledTask] 特性的任务
/// </summary>
public class ScheduledTaskFactory
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // 核心映射表
    private readonly Dictionary<string, Type> _tasksByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TaskType, Type> _tasksByType = new();
    private readonly Dictionary<string, ScheduledTaskMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public ScheduledTaskFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        DiscoverAndRegisterTasks();
    }

    /// <summary>
    /// 自动发现并注册所有标记了 [ScheduledTask] 的任务类
    /// </summary>
    private void DiscoverAndRegisterTasks()
    {
        var taskTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IScheduledTask).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<ScheduledTaskAttribute>() != null);

        foreach (var type in taskTypes)
        {
            var attr = type.GetCustomAttribute<ScheduledTaskAttribute>()!;

            // 注册主键
            _tasksByKey[attr.Key] = type;
            _tasksByType[attr.TaskType] = type;

            // 存储元数据
            _metadata[attr.Key] = new ScheduledTaskMetadata
            {
                Key = attr.Key,
                DisplayName = attr.DisplayName,
                TaskType = attr.TaskType,
                ImplementationType = type
            };

            Log.Debug("注册定时任务: {Key} -> {Type}", attr.Key, type.Name);
        }

        Log.Information("已注册 {Count} 个定时任务", _metadata.Count);
    }

    /// <summary>
    /// 根据键名创建任务实例
    /// </summary>
    public IScheduledTask? CreateTask(string key)
    {
        if (!_tasksByKey.TryGetValue(key, out var type))
        {
            Log.Warning("未找到任务: {Key}", key);
            return null;
        }
        return CreateTaskInstance(type);
    }

    /// <summary>
    /// 根据 TaskType 枚举创建任务实例
    /// </summary>
    public IScheduledTask? CreateTask(TaskType taskType)
    {
        if (!_tasksByType.TryGetValue(taskType, out var type))
        {
            Log.Warning("未找到任务类型: {TaskType}", taskType);
            return null;
        }
        return CreateTaskInstance(type);
    }

    /// <summary>
    /// 获取所有任务元数据
    /// </summary>
    public IReadOnlyCollection<ScheduledTaskMetadata> GetAllTaskMetadata()
        => _metadata.Values.ToList();

    /// <summary>
    /// 检查任务是否存在（通过键名）
    /// </summary>
    public bool TaskExists(string key) => _tasksByKey.ContainsKey(key);

    /// <summary>
    /// 检查任务类型是否存在
    /// </summary>
    public bool TaskTypeExists(TaskType taskType) => _tasksByType.ContainsKey(taskType);

    /// <summary>
    /// 获取所有可用的任务类型
    /// </summary>
    public IEnumerable<TaskType> GetAvailableTaskTypes() => _tasksByType.Keys;

    /// <summary>
    /// 获取任务类型信息
    /// </summary>
    public Dictionary<TaskType, string> GetTaskTypeInfo()
    {
        var info = new Dictionary<TaskType, string>();
        foreach (var kvp in _tasksByType)
        {
            info[kvp.Key] = kvp.Value.FullName ?? kvp.Value.Name;
        }
        return info;
    }

    private IScheduledTask? CreateTaskInstance(Type type)
    {
        try
        {
            var scope = _serviceScopeFactory.CreateScope();
            var task = scope.ServiceProvider.GetService(type) as IScheduledTask;
            if (task != null)
            {
                Log.Debug("从作用域 DI 容器创建任务实例: {Type}", type.Name);
                return new ScopedTaskWrapper(task, scope);
            }

            scope.Dispose();
            Log.Warning("无法从 DI 容器创建任务实例: {Type}", type.Name);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建任务实例失败: {Type}", type.Name);
            return null;
        }
    }
}

/// <summary>
/// 作用域任务包装器，管理依赖服务的生命周期
/// </summary>
internal class ScopedTaskWrapper : IScheduledTask, IDisposable
{
    private readonly IScheduledTask _innerTask;
    private readonly IServiceScope _scope;

    public ScopedTaskWrapper(IScheduledTask innerTask, IServiceScope scope)
    {
        _innerTask = innerTask;
        _scope = scope;
    }

    // ITask 接口成员
    public string TaskId => _innerTask.TaskId;
    public string TaskName => _innerTask.TaskName;
    public TaskType TaskType => _innerTask.TaskType;
    public string? Description => _innerTask.Description;
    public TaskPriority Priority => _innerTask.Priority;
    public Dictionary<string, object>? Parameters
    {
        get => _innerTask.Parameters;
        set => _innerTask.Parameters = value;
    }
    public string? ParentTaskId
    {
        get => _innerTask.ParentTaskId;
        set => _innerTask.ParentTaskId = value;
    }
    public List<string> ChildTaskIds => _innerTask.ChildTaskIds;
    public string? HangfireBatchId
    {
        get => _innerTask.HangfireBatchId;
        set => _innerTask.HangfireBatchId = value;
    }

    // IScheduledTask 接口成员
    public string? CronExpression => _innerTask.CronExpression;
    public DateTime? LastExecutionTime
    {
        get => _innerTask.LastExecutionTime;
        set => _innerTask.LastExecutionTime = value;
    }
    public DateTime? NextExecutionTime
    {
        get => _innerTask.NextExecutionTime;
        set => _innerTask.NextExecutionTime = value;
    }

    public async Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerTask.ExecuteAsync(progressReporter, cancellationToken);
        }
        finally
        {
            _scope?.Dispose();
        }
    }

    public async Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerTask.ExecuteAsync(parameters, cancellationToken);
        }
        finally
        {
            _scope?.Dispose();
        }
    }

    public bool ValidateParameters() => _innerTask.ValidateParameters();
    public TimeSpan? GetEstimatedDuration() => _innerTask.GetEstimatedDuration();

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
