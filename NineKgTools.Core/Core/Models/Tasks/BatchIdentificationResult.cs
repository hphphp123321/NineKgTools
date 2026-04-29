using System;
using System.Collections.Generic;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 批量识别任务结果
/// </summary>
public class BatchIdentificationResult : TaskResult
{
    /// <summary>
    /// 成功识别的媒体列表
    /// </summary>
    public List<IdentifiedMedia> IdentifiedMedias { get; set; } = new();
    
    /// <summary>
    /// 失败的文件列表
    /// </summary>
    public List<FailedIdentification> FailedFiles { get; set; } = new();
}

/// <summary>
/// 已识别的媒体信息
/// </summary>
public class IdentifiedMedia
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = null!;
    
    /// <summary>
    /// 媒体ID
    /// </summary>
    public int MediaId { get; set; }
    
    /// <summary>
    /// 媒体标题
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// 媒体类型
    /// </summary>
    public string MediaType { get; set; } = null!;
}

/// <summary>
/// 识别失败的文件信息
/// </summary>
public class FailedIdentification
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = null!;
    
    /// <summary>
    /// 失败原因
    /// </summary>
    public string Reason { get; set; } = null!;
    
    /// <summary>
    /// 异常信息
    /// </summary>
    public Exception? Exception { get; set; }
}