using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Tags;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Components.Medias;

public partial class MediaFilterDialog : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    [Parameter] public TopCategory TopCategory { get; set; } = TopCategory.Unknown;
    [Parameter] public List<Category> Categories { get; set; } = new();
    [Parameter] public List<Tag> AllTags { get; set; } = new();
    [Parameter] public List<Favorite> AllFavorites { get; set; } = new();

    /// <summary>true 时只按 <see cref="SelectedTopCategoryFilter"/> 粗粒度过滤，忽略子分类选择</summary>
    [Parameter] public bool OnlyTopCategory { get; set; }

    [Parameter] public TopCategory SelectedTopCategoryFilter { get; set; } = TopCategory.Unknown;
    [Parameter] public List<Category> SelectedCategories { get; set; } = new();
    [Parameter] public string? SelectedTagName { get; set; }
    [Parameter] public string? SelectedFavoriteName { get; set; }
    [Parameter] public float? MinRating { get; set; }

    /// <summary>日期筛选依据的字段名（"ReleaseDate" / "AcquisitionDate"）</summary>
    [Parameter] public string DateFilterType { get; set; } = "ReleaseDate";

    [Parameter] public DateTime? StartDate { get; set; }
    [Parameter] public DateTime? EndDate { get; set; }

    private string GetCategoryDisplayText()
    {
        if (OnlyTopCategory && SelectedTopCategoryFilter != TopCategory.Unknown)
        {
            return SelectedTopCategoryFilter switch
            {
                TopCategory.Video => "全部视频",
                TopCategory.Audio => "全部音频",
                TopCategory.Picture => "全部图片",
                TopCategory.Text => "全部文本",
                TopCategory.Game => "全部游戏",
                _ => ""
            };
        }

        if (SelectedCategories.Count == 0) return "";
        if (SelectedCategories.Count == 1) return SelectedCategories[0].Name;
        return $"{SelectedCategories[0].Name} 等 {SelectedCategories.Count} 个分类";
    }

    private bool HasCategoryFilter => OnlyTopCategory
        ? SelectedTopCategoryFilter != TopCategory.Unknown
        : SelectedCategories.Count > 0;

    private void Apply()
    {
        var result = new MediaFilterResult
        {
            OnlyTopCategory = OnlyTopCategory,
            SelectedTopCategoryFilter = SelectedTopCategoryFilter,
            SelectedCategories = new List<Category>(SelectedCategories),
            SelectedTagName = SelectedTagName,
            SelectedFavoriteName = SelectedFavoriteName,
            MinRating = MinRating,
            DateFilterType = DateFilterType,
            StartDate = StartDate,
            EndDate = EndDate
        };

        MudDialog.Close(DialogResult.Ok(result));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private void OnReset()
    {
        OnlyTopCategory = false;
        SelectedTopCategoryFilter = TopCategory.Unknown;
        SelectedCategories.Clear();
        SelectedTagName = null;
        SelectedFavoriteName = null;
        MinRating = null;
        StartDate = null;
        EndDate = null;
        DateFilterType = "ReleaseDate";
    }

    private void ClearCategoryFilter()
    {
        OnlyTopCategory = false;
        SelectedTopCategoryFilter = TopCategory.Unknown;
        SelectedCategories.Clear();
    }

    private void RemoveCategory(Category category)
    {
        SelectedCategories.Remove(category);
    }

    private async Task OpenCategorySelector()
    {
        var parameters = new DialogParameters
        {
            ["FilterTopCategory"] = TopCategory, // 过滤到当前媒体类型
            ["InitialSelectedTopCategory"] = OnlyTopCategory ? SelectedTopCategoryFilter : (TopCategory?)null,
            ["InitialSelectedCategories"] = SelectedCategories,
            ["InitialOnlyTopCategory"] = OnlyTopCategory
        };

        var options = new DialogOptions
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
            CloseButton = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<CategorySelectorDialog>("选择分类", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is CategoryFilterResult categoryResult)
        {
            OnlyTopCategory = categoryResult.OnlyTopCategory;
            SelectedTopCategoryFilter = categoryResult.SelectedTopCategory;
            SelectedCategories = categoryResult.SelectedCategories;
            StateHasChanged();
        }
    }

    private async Task OpenTagSelector()
    {
        // 获取当前选中的标签
        var initialSelectedTags = new List<Tag>();
        if (!string.IsNullOrEmpty(SelectedTagName))
        {
            var selectedTag = AllTags.FirstOrDefault(t => t.Name == SelectedTagName);
            if (selectedTag != null)
            {
                initialSelectedTags.Add(selectedTag);
            }
        }

        var parameters = new DialogParameters
        {
            ["AllowMultiSelect"] = false, // 单选模式
            ["InitialSelectedTags"] = initialSelectedTags
        };

        var options = new DialogOptions
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Medium,
            CloseButton = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<TagSelectorDialog>("选择标签", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is List<Tag> selectedTags && selectedTags.Count > 0)
        {
            SelectedTagName = selectedTags[0].Name;
            StateHasChanged();
        }
    }

    private string GetCategoryLabel()
    {
        return TopCategory switch
        {
            TopCategory.Audio => "音频分类",
            TopCategory.Video => "视频分类",
            TopCategory.Game => "游戏分类",
            TopCategory.Picture => "图片分类",
            TopCategory.Text => "文本分类",
            _ => "媒体分类"
        };
    }

    private Color GetTopCategoryColor()
    {
        return TopCategory switch
        {
            TopCategory.Audio => Color.Primary,
            TopCategory.Video => Color.Secondary,
            TopCategory.Game => Color.Success,
            TopCategory.Picture => Color.Info,
            TopCategory.Text => Color.Warning,
            _ => Color.Default
        };
    }
}

public class MediaFilterResult
{
    public bool OnlyTopCategory { get; set; }
    public TopCategory SelectedTopCategoryFilter { get; set; } = TopCategory.Unknown;
    public List<Category> SelectedCategories { get; set; } = new();

    public bool HasCategoryFilter => OnlyTopCategory
        ? SelectedTopCategoryFilter != TopCategory.Unknown
        : SelectedCategories.Count > 0;

    public string? SelectedTagName { get; set; }
    public string? SelectedFavoriteName { get; set; }
    public float? MinRating { get; set; }
    public string DateFilterType { get; set; } = "ReleaseDate";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CategoryFilterResult
{
    public bool OnlyTopCategory { get; set; }
    public TopCategory SelectedTopCategory { get; set; } = TopCategory.Unknown;
    public List<Category> SelectedCategories { get; set; } = new();

    public bool HasFilter => OnlyTopCategory
        ? SelectedTopCategory != TopCategory.Unknown
        : SelectedCategories.Count > 0;

    public string GetDisplayText()
    {
        if (OnlyTopCategory && SelectedTopCategory != TopCategory.Unknown)
        {
            return SelectedTopCategory switch
            {
                TopCategory.Video => "全部视频",
                TopCategory.Audio => "全部音频",
                TopCategory.Picture => "全部图片",
                TopCategory.Text => "全部文本",
                TopCategory.Game => "全部游戏",
                _ => ""
            };
        }

        if (SelectedCategories.Count == 0) return "";
        if (SelectedCategories.Count == 1) return SelectedCategories[0].Name;
        return $"{SelectedCategories[0].Name} 等 {SelectedCategories.Count} 个分类";
    }
}
