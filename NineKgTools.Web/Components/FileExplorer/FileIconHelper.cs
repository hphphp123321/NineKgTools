using MudBlazor;

namespace NineKgTools.Components.FileExplorer;

/// <summary>
/// 文件扩展名到图标/视觉色的映射。由 FileExplorer 和 DirectoryTreeView 共享，
/// 避免两个组件各维护一份容易 drift 的 switch 表。
///
/// 颜色刻意避开 <see cref="Color.Warning"/> 和 <see cref="Color.Error"/>——
/// 它们是 MudBlazor 的语义状态色（警告/错误），用在"PDF 文件"或"文件夹"上会让 UI 看起来
/// 永远处于错误态，屏幕阅读器也会朗读错误。
/// </summary>
internal static class FileIconHelper
{
    public static string GetFolderIcon() => Icons.Material.Filled.Folder;

    public static Color GetFolderColor() => Color.Tertiary;

    /// <summary>根据扩展名（小写，含点）返回图标；目录和未知类型走回退</summary>
    public static string GetFileIcon(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return Icons.Material.Filled.InsertDriveFile;

        return extension switch
        {
            ".pdf" => Icons.Material.Filled.PictureAsPdf,
            ".doc" or ".docx" => Icons.Material.Filled.Description,
            ".xls" or ".xlsx" => Icons.Material.Filled.TableChart,
            ".zip" or ".rar" or ".7z" => Icons.Material.Filled.Archive,
            ".txt" or ".md" => Icons.Material.Filled.Description,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => Icons.Material.Filled.Image,
            ".mp3" or ".wav" or ".flac" or ".ape" or ".ogg" or ".m4a" or ".aac" => Icons.Material.Filled.MusicNote,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => Icons.Material.Filled.Movie,
            ".exe" or ".msi" or ".app" => Icons.Material.Filled.Apps,
            ".cs" or ".js" or ".ts" or ".py" or ".java" => Icons.Material.Filled.Code,
            ".json" or ".xml" or ".yaml" or ".yml" => Icons.Material.Filled.DataObject,
            ".epub" or ".mobi" => Icons.Material.Filled.Book,
            _ => Icons.Material.Filled.InsertDriveFile
        };
    }

    /// <summary>根据扩展名返回视觉色。只使用非语义化色槽</summary>
    public static Color GetFileColor(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return Color.Default;

        return extension switch
        {
            ".pdf" => Color.Primary,
            ".doc" or ".docx" => Color.Primary,
            ".xls" or ".xlsx" => Color.Success,
            ".zip" or ".rar" or ".7z" => Color.Tertiary,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => Color.Info,
            ".mp3" or ".wav" or ".flac" or ".ape" or ".ogg" or ".m4a" or ".aac" => Color.Secondary,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" => Color.Tertiary,
            ".exe" or ".msi" or ".app" => Color.Primary,
            _ => Color.Default
        };
    }

    /// <summary>便捷重载：从文件名直接推断（`Path.GetExtension` + lowercase）</summary>
    public static string GetIconFromFileName(string? fileName) =>
        GetFileIcon(GetExtension(fileName));

    public static Color GetColorFromFileName(string? fileName) =>
        GetFileColor(GetExtension(fileName));

    private static string? GetExtension(string? fileName) =>
        string.IsNullOrEmpty(fileName) ? null : Path.GetExtension(fileName).ToLowerInvariant();
}
