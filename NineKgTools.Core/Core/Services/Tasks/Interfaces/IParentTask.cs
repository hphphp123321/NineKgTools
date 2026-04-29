using System.Collections.Generic;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 父任务接口 - 可以包含和管理子任务
/// </summary>
public interface IParentTask : ITask
{
    /// <summary>
    /// 创建子任务列表
    /// 在父任务提交时调用，用于动态生成所有子任务
    /// </summary>
    /// <returns>子任务列表</returns>
    Task<List<ITask>> CreateChildTasksAsync();

    /// <summary>
    /// 单个子任务完成时的回调
    /// 可用于实时更新父任务状态或进行增量处理
    /// </summary>
    /// <param name="childTaskId">完成的子任务ID</param>
    /// <param name="result">子任务执行结果</param>
    /// <returns>异步任务</returns>
    Task OnChildTaskCompletedAsync(string childTaskId, TaskResult result)
    {
        // 默认空实现，子类可按需覆盖
        return Task.CompletedTask;
    }

    /// <summary>
    /// 所有子任务完成后的回调
    /// 用于汇总结果、发送通知、生成报告等
    /// </summary>
    /// <param name="childResults">所有子任务的执行结果列表</param>
    /// <returns>异步任务</returns>
    Task OnAllChildTasksCompletedAsync(List<TaskResult> childResults);
}
