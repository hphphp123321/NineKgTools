namespace NineKgTools.Core.Services.Files.Models;

/// <summary>
/// 文件操作错误类型枚举
/// </summary>
public enum FileOperationError
{
    /// <summary>
    /// 未知错误
    /// </summary>
    Unknown,

    /// <summary>
    /// 当前环境不支持此操作（如远程访问时）
    /// </summary>
    NotSupported,

    /// <summary>
    /// 文件或文件夹不存在
    /// </summary>
    FileNotFound,

    /// <summary>
    /// 访问被拒绝（权限不足）
    /// </summary>
    AccessDenied,

    /// <summary>
    /// 无效的路径格式
    /// </summary>
    InvalidPath,

    /// <summary>
    /// 进程启动失败
    /// </summary>
    ProcessStartFailed
}
