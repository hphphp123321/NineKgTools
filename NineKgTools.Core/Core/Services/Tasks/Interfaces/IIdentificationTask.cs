using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 识别任务接口
/// </summary>
public interface IIdentificationTask : ITask
{
    /// <summary>
    /// 目标路径（文件或文件夹）
    /// </summary>
    string TargetPath { get; }
    
    /// <summary>
    /// 识别选项
    /// </summary>
    IdentificationOptions? Options { get; }
    
    /// <summary>
    /// 是否为批量识别
    /// </summary>
    bool IsBatch { get; }
    
    /// <summary>
    /// 执行单个识别
    /// </summary>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>识别的媒体信息</returns>
    Task<MediaBase?> IdentifyAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行批量识别
    /// </summary>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>批量识别结果</returns>
    Task<BatchIdentificationResult> IdentifyBatchAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取待识别的文件列表（用于批量识别）
    /// </summary>
    /// <returns>文件路径列表</returns>
    Task<List<string>> GetFilesToIdentifyAsync();
}