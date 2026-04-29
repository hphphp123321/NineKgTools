using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Tasks;

public partial class ScheduledTasksPage : ComponentBase
{
    [Inject] private Config Config { get; set; } = null!;
    [Inject] private UnifiedTaskService UnifiedTaskService { get; set; } = null!;
    [Inject] private ScheduledTaskFactory ScheduledTaskFactory { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    // 任务配置列表
    private List<ScheduledTaskConfig>? _taskConfigs;
    private Dictionary<string, ScheduledTaskMetadata> _taskMetadata = new();
    private Dictionary<string, DateTime?> _lastExecutionTimes = new();
    private readonly HashSet<string> _executingTasks = new();

    // UI 状态
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showCronHelp;

    protected override async Task OnInitializedAsync()
    {
        await LoadTasksAsync();
    }

    /// <summary>
    /// 加载任务列表
    /// </summary>
    private Task LoadTasksAsync()
    {
        _isLoading = true;

        try
        {
            // 从配置加载任务
            _taskConfigs = Config.Tasks.ScheduledTasks?.ToList() ?? new List<ScheduledTaskConfig>();

            // 从工厂获取任务元数据
            var allMetadata = ScheduledTaskFactory.GetAllTaskMetadata();
            _taskMetadata = allMetadata.ToDictionary(m => m.Key, m => m);

            // 获取执行历史
            LoadExecutionHistory();

            Log.Debug("加载了 {Count} 个定时任务配置", _taskConfigs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载定时任务配置失败");
            Snackbar.Add("加载定时任务失败，请重试", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载执行历史
    /// </summary>
    private void LoadExecutionHistory()
    {
        try
        {
            // 获取所有执行历史
            var history = UnifiedTaskService.GetExecutionHistory();

            // 按任务名称分组，获取每个任务的最后执行时间
            _lastExecutionTimes = history
                .GroupBy(h => h.TaskName)
                .ToDictionary(
                    g => g.Key,
                    g => (DateTime?)g.Max(h => h.EndTime)
                );
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载执行历史失败");
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    private async Task SaveConfigAsync()
    {
        _isSaving = true;
        StateHasChanged();

        try
        {
            // 验证所有启用任务的 Cron 表达式
            if (_taskConfigs != null)
            {
                foreach (var task in _taskConfigs.Where(t => t.Enabled))
                {
                    if (!CronValidator.IsValid(task.CronExpression))
                    {
                        Snackbar.Add($"任务「{task.Name}」的 Cron 表达式格式不正确，已禁用", Severity.Warning);
                        task.Enabled = false;
                    }
                }
            }

            // 更新配置
            Config.Tasks.ScheduledTasks = _taskConfigs;
            await Config.SaveConfig();

            Snackbar.Add("定时任务配置保存成功", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存定时任务配置失败");
            Snackbar.Add("保存配置失败，请重试", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 手动执行任务
    /// </summary>
    private async Task ExecuteTaskAsync(string taskType)
    {
        if (_executingTasks.Contains(taskType))
        {
            Snackbar.Add("该任务正在执行中", Severity.Warning);
            return;
        }

        _executingTasks.Add(taskType);
        StateHasChanged();

        try
        {
            await UnifiedTaskService.ExecuteScheduledTaskAsync(taskType, CancellationToken.None);

            var taskConfig = _taskConfigs?.FirstOrDefault(t => t.Type == taskType);
            var taskName = taskConfig?.Name ?? taskType;

            Snackbar.Add($"已提交任务「{taskName}」，可在任务概览页面查看进度", Severity.Success);

            // 刷新执行历史
            LoadExecutionHistory();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "执行定时任务失败: {TaskType}", taskType);
            Snackbar.Add("执行任务失败，请重试", Severity.Error);
        }
        finally
        {
            _executingTasks.Remove(taskType);
            StateHasChanged();
        }
    }

    /// <summary>
    /// 获取 Cron 验证错误信息
    /// </summary>
    private string? GetCronValidationError(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        var result = CronValidator.Validate(cronExpression);
        return result.IsValid ? null : result.ErrorMessage;
    }

    /// <summary>
    /// 获取 Cron 帮助文本
    /// </summary>
    private string GetCronHelperText(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return "请输入 Cron 表达式";

        return CronValidator.GetDescription(cronExpression);
    }

    /// <summary>
    /// 计算下次执行时间
    /// </summary>
    private string GetNextExecutionTime(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression) || !CronValidator.IsValid(cronExpression))
            return "无效表达式";

        try
        {
            var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                return "无效表达式";

            var now = DateTime.Now;
            var minute = parts[0];
            var hour = parts[1];

            DateTime? nextTime = null;

            if (minute == "*" && hour == "*")
            {
                nextTime = now.AddMinutes(1);
            }
            else if (minute == "0" && hour == "*")
            {
                nextTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
            }
            else if (minute == "0" && hour == "0" && parts[2] == "*" && parts[3] == "*" && parts[4] == "*")
            {
                nextTime = now.Date.AddDays(1);
            }
            else if (hour.StartsWith("*/") && int.TryParse(hour.AsSpan(2), out var interval) && interval > 0)
            {
                var targetMinute = int.TryParse(minute, out var m) ? m : 0;
                var nextHour = ((now.Hour / interval) + 1) * interval;
                if (nextHour >= 24)
                    nextTime = now.Date.AddDays(1).AddHours(nextHour % 24).AddMinutes(targetMinute);
                else
                    nextTime = now.Date.AddHours(nextHour).AddMinutes(targetMinute);
            }

            if (nextTime.HasValue)
                return nextTime.Value.ToString("yyyy-MM-dd HH:mm");

            // 无法精确计算的表达式，显示调度描述
            return CronValidator.GetDescription(cronExpression);
        }
        catch
        {
            return CronValidator.GetDescription(cronExpression);
        }
    }

    /// <summary>
    /// 获取上次执行时间
    /// </summary>
    private string? GetLastExecutionTime(string taskType)
    {
        // 根据任务类型找到任务名称
        var taskConfig = _taskConfigs?.FirstOrDefault(t => t.Type == taskType);
        if (taskConfig != null && _lastExecutionTimes.TryGetValue(taskConfig.Name, out var lastTime) && lastTime.HasValue)
        {
            return lastTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // 尝试直接通过类型查找
        if (_lastExecutionTimes.TryGetValue(taskType, out lastTime) && lastTime.HasValue)
        {
            return lastTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return null;
    }

    /// <summary>
    /// 获取任务卡片样式
    /// </summary>
    private string GetTaskCardClass(bool enabled)
    {
        return enabled
            ? "card-base card-bordered-primary"
            : "card-base card-bordered-default opacity-secondary";
    }

    // Cron 帮助对话框数据
    private static readonly (string Icon, string Text)[] CronFields =
    [
        (Icons.Material.Filled.Timer, "分钟：0-59"),
        (Icons.Material.Filled.Schedule, "小时：0-23"),
        (Icons.Material.Filled.DateRange, "日期：1-31"),
        (Icons.Material.Filled.CalendarMonth, "月份：1-12"),
        (Icons.Material.Filled.CalendarViewWeek, "星期：0-6（星期日为0）")
    ];

    private static readonly (string Icon, string Text)[] CronSymbols =
    [
        (Icons.Material.Filled.Star, "* — 表示任意值"),
        (Icons.Material.Filled.UnfoldMore, "/ — 表示间隔，如 */2 表示每隔2个单位"),
        (Icons.Material.Filled.DashboardCustomize, "- — 表示范围，如 1-5 表示1到5"),
        (Icons.Material.Filled.List, ", — 表示列表，如 1,3,5 表示1、3和5")
    ];

    private static readonly (string Icon, string Text)[] CronExamples =
    [
        (Icons.Material.Filled.Timer, "0 * * * * — 每小时整点执行"),
        (Icons.Material.Filled.Timer, "0 */2 * * * — 每2小时执行一次"),
        (Icons.Material.Filled.Schedule, "0 0 * * * — 每天0点执行"),
        (Icons.Material.Filled.Weekend, "0 10 * * 1 — 每周一10点执行"),
        (Icons.Material.Filled.CalendarToday, "0 0 1 * * — 每月1日0点执行")
    ];

    /// <summary>
    /// 获取任务图标
    /// </summary>
    private string GetTaskIcon(string taskType)
    {
        return taskType.ToLower() switch
        {
            "cachecleanup" => Icons.Material.Filled.CleaningServices,
            "mediacleanup" => Icons.Material.Filled.DeleteSweep,
            "tagvectorsync" => Icons.Material.Filled.Label,
            "mediavectorsync" => Icons.Material.Filled.VideoLibrary,
            _ => Icons.Material.Filled.Task
        };
    }
}
