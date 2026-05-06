using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media.Source;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// ManualAddMediaDialog 的视图上下文。承载用户在对话框内的输入（标题 / TopCategory /
/// 子分类 / 简介 / 评分），并派生出 CanSubmit / HasOptionalContent / HintMessage 等
/// 视图绑定。切换 TopCategory 会自动刷新可选子分类列表。
/// </summary>
public partial class ManualAddMediaDialogContext : ObservableObject
{
    public MediaSource Source { get; }

    public ManualAddMediaDialogContext(MediaSource source)
    {
        Source = source;

        TitleValue = Path.GetFileNameWithoutExtension(source.FullPath);
        if (string.IsNullOrWhiteSpace(TitleValue))
            TitleValue = Path.GetFileName(source.FullPath);

        // PossibleTopCategory == Unknown 时不预选，让用户主动选
        if (source.PossibleTopCategory != TopCategory.Unknown)
        {
            SelectedTopCategory = TopCategoryOptions
                .FirstOrDefault(c => c.Value == source.PossibleTopCategory);
        }

        BuildSourceKindHint();
    }

    // ===== Source meta（只读 / 一次计算） =====

    public string SourceFileName => string.IsNullOrEmpty(Source.FullPath)
        ? ""
        : Path.GetFileName(Source.FullPath);

    public string SourceFullPath => Source.FullPath;

    public string SourceKindLabel => Source.IsFolder ? "文件夹" : "文件";

    public string? SourceKindHint { get; private set; }

    public bool HasSourceKindHint => !string.IsNullOrEmpty(SourceKindHint);

    // ===== Form fields =====

    [ObservableProperty]
    private string _titleValue = "";

    [ObservableProperty]
    private TopCategoryChoice? _selectedTopCategory;

    [ObservableProperty]
    private Category? _selectedSubCategory;

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private int _rating;

    [ObservableProperty]
    private bool _optionalExpanded;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>5 个可选 TopCategory（不含 Unknown），固定列表，AXAML ListBox 直接绑定。</summary>
    public ObservableCollection<TopCategoryChoice> TopCategoryOptions { get; } = new()
    {
        new TopCategoryChoice(TopCategory.Video, "视频"),
        new TopCategoryChoice(TopCategory.Audio, "音频"),
        new TopCategoryChoice(TopCategory.Picture, "图片"),
        new TopCategoryChoice(TopCategory.Text, "文本"),
        new TopCategoryChoice(TopCategory.Game, "游戏"),
    };

    /// <summary>当前 TopCategory 下的可选具体分类，OnSelectedTopCategoryChanged 时刷新。</summary>
    public ObservableCollection<Category> AvailableSubCategories { get; } = new();

    // ===== Computed (raise PropertyChanged in partial OnXxxChanged hooks) =====

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasTopCategorySelected => SelectedTopCategory is not null;

    public bool HasOptionalContent =>
        SelectedSubCategory is not null
        || !string.IsNullOrWhiteSpace(Summary)
        || Rating > 0;

    /// <summary>用户全填了选填项 → 显示"创建"；未填 → 显示"创建并编辑"提示后续可去详情页继续补</summary>
    public string ConfirmButtonText => HasOptionalContent ? "创建" : "创建并编辑";

    public string HintMessage => HasOptionalContent
        ? "信息已足够，创建后将进入媒体详情"
        : "其余字段（标签、封面、创作者…）可稍后在媒体详情页继续补充";

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(TitleValue) && SelectedTopCategory is not null;

    // ===== Property change hooks =====

    partial void OnTitleValueChanged(string value)
    {
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnSelectedTopCategoryChanged(TopCategoryChoice? value)
    {
        // 切换 TopCategory：原 SubCategory 可能已不属于新类型，清掉 + 重算
        SelectedSubCategory = null;
        RefreshSubCategories();
        OnPropertyChanged(nameof(HasTopCategorySelected));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnSelectedSubCategoryChanged(Category? value)
    {
        OnPropertyChanged(nameof(HasOptionalContent));
        OnPropertyChanged(nameof(ConfirmButtonText));
        OnPropertyChanged(nameof(HintMessage));
    }

    partial void OnSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(HasOptionalContent));
        OnPropertyChanged(nameof(ConfirmButtonText));
        OnPropertyChanged(nameof(HintMessage));
    }

    partial void OnRatingChanged(int value)
    {
        OnPropertyChanged(nameof(HasOptionalContent));
        OnPropertyChanged(nameof(ConfirmButtonText));
        OnPropertyChanged(nameof(HintMessage));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void RefreshSubCategories()
    {
        AvailableSubCategories.Clear();
        if (SelectedTopCategory is null) return;

        var topVal = SelectedTopCategory.Value;
        foreach (var c in StaticCategories.CategoryList
                     .Where(c => c.TopCategory == topVal && c.TopCategory != TopCategory.Unknown)
                     .OrderBy(c => c.Id))
        {
            AvailableSubCategories.Add(c);
        }
    }

    private void BuildSourceKindHint()
    {
        try
        {
            if (Source.IsFolder)
            {
                var count = new DirectoryInfo(Source.FullPath).GetFileSystemInfos().Length;
                SourceKindHint = count > 0 ? $"{count} 个条目" : null;
            }
            else
            {
                var ext = Path.GetExtension(Source.FullPath);
                SourceKindHint = string.IsNullOrEmpty(ext)
                    ? null
                    : ext.TrimStart('.').ToLowerInvariant();
            }
        }
        catch
        {
            // Hero chip 装饰失败静默——不影响主流程
            SourceKindHint = null;
        }
    }
}

/// <summary>
/// TopCategory + 中文显示名的轻量包装，给 AXAML ListBox.ItemsSource 用。
/// 比直接绑 enum 简单——不用写 ValueConverter 把枚举转中文。
/// </summary>
public sealed record TopCategoryChoice(TopCategory Value, string DisplayName);
