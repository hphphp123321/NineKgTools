namespace NineKgTools.Core.Services.Files.Models;

/// <summary>
/// 文件操作结果类
/// </summary>
public class FileOperationResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 错误消息（仅在失败时有值）
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 错误类型（仅在失败时有值）
    /// </summary>
    public FileOperationError? Error { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static FileOperationResult Ok() => new() { Success = true };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="message">错误消息</param>
    /// <param name="error">错误类型</param>
    public static FileOperationResult Fail(string message, FileOperationError error = FileOperationError.Unknown)
        => new() { Success = false, ErrorMessage = message, Error = error };
}
