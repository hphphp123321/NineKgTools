using NineKgTools.Core.Models.Categories;
using MudBlazor;

namespace NineKgTools.Utils;

public static class MediaUIHelper
{
    /// <summary>
    /// 获取媒体类别对应的MudBlazor颜色
    /// </summary>
    /// <param name="category">媒体类别</param>
    /// <returns>对应的MudBlazor颜色</returns>
    public static Color GetMediaColor(TopCategory category) => category switch
    {
        TopCategory.Video => Color.Primary,
        TopCategory.Audio => Color.Secondary,
        TopCategory.Picture => Color.Warning,
        TopCategory.Text => Color.Tertiary,
        TopCategory.Game => Color.Info,
        _ => Color.Default
    };

    public static string GetCategoryIcon(TopCategory category) => category switch
    {
        TopCategory.Video => Icons.Material.Filled.SmartDisplay,
        TopCategory.Audio => Icons.Material.Filled.Headphones,
        TopCategory.Picture => Icons.Material.Filled.Image,
        TopCategory.Text => Icons.Material.Filled.LibraryBooks,
        TopCategory.Game => Icons.Material.Filled.VideogameAsset,
        _ => Icons.Material.Filled.Category
    };
    
} 