using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Services.Files;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 手动添加媒体对话框（v2 改版）。
///
/// 交互要点：
/// - Hero 横幅显示文件名 / 路径 / 文件-夹标识，会随所选顶层分类变色（primary→success→warning...）。
/// - 核心字段：Title + TopCategory。用户可只填这两项就提交，原流程不变。
/// - 手风琴"填更多信息（可选）"：子分类、简介、评分。任何一个被填都会：
///     · 提交按钮从"创建并编辑"变"创建"
///     · 通过 <see cref="ManualAddMediaResult"/> 的 <c>FullyFilled=true</c> 通知调用方跳过 ?edit=true
/// - 错误消息脱敏（反模式 #3）：详细异常写 Serilog，用户只看到"创建失败，请稍后重试"。
///
/// 对话框成功关闭时通过 DialogResult 返回 <see cref="ManualAddMediaResult"/>。调用方根据 FullyFilled
/// 决定是否附加 ?edit=true。
/// </summary>
public partial class ManualAddMediaDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>要关联的媒体源。对话框内只读展示，入库时会直接挂到新建的 MediaBase 上。</summary>
    [Parameter, EditorRequired] public MediaSource Source { get; set; } = null!;

    [Inject] private FilesService FilesService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    // 核心字段
    private string _title = string.Empty;
    private TopCategory? _selectedTopCategory;
    private bool _isSaving;
    private string? _errorMessage;
    private string? _sourceKindHint;

    // 选填字段（手风琴）
    private bool _optionalExpanded;
    private Category? _selectedSubCategory;
    private string _summary = string.Empty;
    private int _ratingInt; // MudRating 用 int，保存时转 float

    /// <summary>类型选择器可选项：五个有效 TopCategory，不含 Unknown。</summary>
    private static readonly TopCategory[] _topCategoryOptions =
    [
        TopCategory.Video, TopCategory.Audio, TopCategory.Picture,
        TopCategory.Text, TopCategory.Game
    ];

    /// <summary>当前 TopCategory 下可选的具体分类列表（随选择动态变化）。</summary>
    private List<Category> _availableSubCategories = new();

    private bool CanSubmit =>
        !string.IsNullOrWhiteSpace(_title) && _selectedTopCategory.HasValue && !_isSaving;

    private bool HasOptionalContent =>
        _selectedSubCategory is not null ||
        !string.IsNullOrWhiteSpace(_summary) ||
        _ratingInt > 0;

    private int OptionalFilledCount =>
        (_selectedSubCategory is not null ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(_summary) ? 1 : 0) +
        (_ratingInt > 0 ? 1 : 0);

    private string ConfirmButtonText =>
        HasOptionalContent ? "创建" : "创建并编辑";

    // Hero 色跟随 —— 未选类型时给中性 default，避免强行染色
    private string IntentSuffix => _selectedTopCategory switch
    {
        TopCategory.Video => "primary",
        TopCategory.Audio => "secondary",
        TopCategory.Picture => "warning",
        TopCategory.Text => "tertiary",
        TopCategory.Game => "info",
        _ => "default"
    };

    private Color SelectedColor => _selectedTopCategory.HasValue
        ? MediaUIHelper.GetMediaColor(_selectedTopCategory.Value)
        : Color.Default;

    private string HeroIcon => _selectedTopCategory.HasValue
        ? MediaUIHelper.GetCategoryIcon(_selectedTopCategory.Value)
        : (Source.IsFolder
            ? Icons.Material.Filled.FolderOpen
            : Icons.Material.Filled.InsertDriveFile);

    private string FrameCssClass => $"nk-dialog-frame nk-dialog-frame--{IntentSuffix} manual-add-frame";
    private string HeroCssClass => $"nk-dialog-hero nk-dialog-hero--{IntentSuffix} manual-add-hero";

    protected override void OnInitialized()
    {
        // 预填标题为文件名（不含扩展名）
        _title = Path.GetFileNameWithoutExtension(Source.FullPath);
        if (string.IsNullOrWhiteSpace(_title))
            _title = Source.GetFileName();

        // 预选 TopCategory：若源的 PossibleTopCategory 是 Unknown 则不预选
        _selectedTopCategory = Source.PossibleTopCategory == TopCategory.Unknown
            ? null
            : Source.PossibleTopCategory;

        RefreshSubCategories();
        BuildSourceKindHint();
    }

    private void SelectTopCategory(TopCategory category)
    {
        if (_isSaving) return;

        if (_selectedTopCategory != category)
        {
            _selectedTopCategory = category;
            // 切换顶层分类后，原来的子分类选择可能已不属于新类型，清掉
            _selectedSubCategory = null;
            RefreshSubCategories();
        }
    }

    private void RefreshSubCategories()
    {
        if (_selectedTopCategory is null)
        {
            _availableSubCategories = new List<Category>();
            return;
        }

        _availableSubCategories = StaticCategories.CategoryList
            .Where(c => c.TopCategory == _selectedTopCategory.Value
                        && c.TopCategory != TopCategory.Unknown)
            .OrderBy(c => c.Id)
            .ToList();
    }

    private void BuildSourceKindHint()
    {
        try
        {
            if (Source.IsFolder)
            {
                // 同级 top-level 项数（不递归，保证 UI 快）
                var count = new DirectoryInfo(Source.FullPath).GetFileSystemInfos().Length;
                _sourceKindHint = count > 0 ? $"{count} 个条目" : null;
            }
            else
            {
                var ext = Path.GetExtension(Source.FullPath);
                _sourceKindHint = string.IsNullOrEmpty(ext) ? null : ext.TrimStart('.').ToLowerInvariant();
            }
        }
        catch
        {
            // 读取失败（权限/路径不存在）时静默 —— 只是 Hero 上的装饰 chip
            _sourceKindHint = null;
        }
    }

    private void Cancel()
    {
        if (_isSaving) return;
        MudDialog.Cancel();
    }

    private async Task CopyPathAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", Source.FullPath);
            Snackbar.Add("路径已复制", Severity.Info);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "复制路径失败 Path={Path}", Source.FullPath);
            Snackbar.Add("复制失败，请手动选取", Severity.Warning);
        }
    }

    private async Task OnPathKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            await CopyPathAsync();
    }

    private async Task CreateAsync()
    {
        if (!CanSubmit) return;

        _errorMessage = null;
        _isSaving = true;
        StateHasChanged();

        try
        {
            var top = _selectedTopCategory!.Value;

            // 子分类：优先用户选的，否则 fallback 到"其他X"
            var category = _selectedSubCategory ?? GetDefaultCategoryFor(top);

            // 简介：用户填了就用，没填保留 MediaBase 默认"暂无简介"
            var summary = string.IsNullOrWhiteSpace(_summary) ? "暂无简介" : _summary.Trim();

            var baseline = new MediaBase
            {
                Title = _title.Trim(),
                Category = category,
                Source = Source,
                Summary = summary,
                Rating = _ratingInt
            };

            MediaBase media = top switch
            {
                TopCategory.Video => new VideoMedia(baseline),
                TopCategory.Audio => new AudioMedia(baseline),
                TopCategory.Picture => new PictureMedia(baseline),
                TopCategory.Text => new TextMedia(baseline),
                TopCategory.Game => new GameMedia(baseline),
                _ => throw new InvalidOperationException($"不支持的顶层分类: {top}")
            };

            await FilesService.AddMediaToDatabase(media);

            Log.Information(
                "手动创建媒体成功: Id={Id}, Title={Title}, TopCategory={Top}, Category={Category}, " +
                "FullyFilled={Full}, HasSummary={HasSummary}, Rating={Rating}",
                media.Id, media.Title, top, category.Name,
                HasOptionalContent, !string.IsNullOrWhiteSpace(_summary), _ratingInt);

            MudDialog.Close(DialogResult.Ok(new ManualAddMediaResult(media.Id, HasOptionalContent)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动创建媒体失败: SourcePath={Path}", Source.FullPath);
            _errorMessage = "创建失败，请稍后重试。";
            _isSaving = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 为每个 TopCategory 选择一个"其他X"作为默认具体分类（语义上表示"未细化"），
    /// 用户进入 MediaPage 后可通过 CategorySelector 修改到更精确的分类。
    /// </summary>
    private static Category GetDefaultCategoryFor(TopCategory top) => top switch
    {
        TopCategory.Video => StaticCategories.OtherVideo,
        TopCategory.Audio => StaticCategories.OtherAudio,
        TopCategory.Picture => StaticCategories.OtherPicture,
        TopCategory.Text => StaticCategories.OtherText,
        TopCategory.Game => StaticCategories.OtherGame,
        _ => StaticCategories.Unknown
    };

    private static string GetTopCategoryName(TopCategory category) => category switch
    {
        TopCategory.Video => "视频",
        TopCategory.Audio => "音频",
        TopCategory.Picture => "图片",
        TopCategory.Text => "文本",
        TopCategory.Game => "游戏",
        _ => "未知"
    };
}

/// <summary>
/// 手动添加媒体对话框成功时返回的结果。
/// </summary>
/// <param name="MediaId">新建媒体的 Id。</param>
/// <param name="FullyFilled">
/// 用户是否在对话框里展开手风琴并至少填了一个选填字段。
/// 为 true 时调用方应直接跳转 <c>/media/{id}</c>，不附加 <c>?edit=true</c>，避免立即进入编辑态打断流程。
/// </param>
public record ManualAddMediaResult(int MediaId, bool FullyFilled);
