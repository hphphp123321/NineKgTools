using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Identification;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// IdentificationOptionsDialog 的视图上下文。对齐 Web 端 <c>IdentificationOptionsDialog</c>：
/// 4 个手风琴面板（基础 / 多网站ID映射 / 高级 / 优先级），点击"开始识别"通过 <see cref="BuildOptions"/>
/// 落成 <see cref="IdentificationOptions"/> 返回给调用方。
///
/// 设计要点：
/// 1) 重置 = 用 <paramref name="initialOptions"/> 恢复 UI 字段（不是 IdentificationOptions.Reset()——
///    那会丢掉调用方塞进来的 SkipCache/AutoAddToDatabase 默认值）
/// 2) Strategy / SuggestedCategory 用 enum 双向绑定 ComboBox（避免 RadioGroup + enum CommandParameter 陷阱）
/// 3) PriorityChoices 用 ToggleButton 多选，OrderedSelection 按选中先后排序（与 Web 多选 ComboBox 等价）
/// </summary>
public partial class IdentificationOptionsDialogContext : ObservableObject
{
    public string SourcePath { get; }
    public bool IsReidentify { get; }
    public string Title => IsReidentify ? "重新识别选项" : "手动识别选项";
    public string Subtitle => IsReidentify
        ? "指定网站、ID 与策略后开始重新识别"
        : "指定网站、ID 与策略后开始识别此路径";

    public IReadOnlyList<string> AvailableWebsites { get; }
    public IReadOnlyList<StrategyOption> StrategyOptions { get; }
    public IReadOnlyList<TopCategoryOption> CategoryOptions { get; }

    private readonly IdentificationOptions _initialOptions;

    // ---------- 基础选项 ----------

    [ObservableProperty] private string? _preferredWebsite;
    [ObservableProperty] private string? _websiteSpecificId;
    [ObservableProperty] private string? _customIdentificationName;
    [ObservableProperty] private StrategyOption? _strategy;
    [ObservableProperty] private TopCategoryOption? _suggestedCategory;

    // ---------- 多网站 ID 映射 ----------

    public ObservableCollection<WebsiteIdEntryVm> WebsiteIds { get; } = new();
    [ObservableProperty] private string? _newWebsiteName;
    [ObservableProperty] private string? _newWebsiteId;

    public bool HasWebsiteIds => WebsiteIds.Count > 0;
    public string WebsiteIdsCountText => WebsiteIds.Count > 0 ? $"({WebsiteIds.Count})" : "";

    // ---------- 高级选项 ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeoutDisplay))]
    private int _timeoutSeconds = 30;

    [ObservableProperty] private bool _skipCache;

    public string TimeoutDisplay => $"超时时间：{TimeoutSeconds} 秒";

    // ---------- 网站优先级 ----------

    public ObservableCollection<WebsitePriorityChoiceVm> PriorityChoices { get; } = new();

    public string PriorityDisplay
    {
        get
        {
            var selected = PriorityChoices.Where(c => c.IsSelected)
                .OrderBy(c => c.SelectionOrder)
                .Select(c => c.WebsiteName)
                .ToList();
            return selected.Count == 0 ? "（使用默认优先级）" : string.Join(" → ", selected);
        }
    }

    // ---------- 验证错误 ----------

    [ObservableProperty] private string? _errorMessage;

    public IdentificationOptionsDialogContext(
        string sourcePath,
        IdentificationOptions initialOptions,
        IReadOnlyList<string> availableWebsites,
        bool isReidentify)
    {
        SourcePath = sourcePath;
        IsReidentify = isReidentify;
        AvailableWebsites = availableWebsites;
        _initialOptions = initialOptions.Clone();

        StrategyOptions = System.Enum.GetValues<IdentificationStrategy>()
            .Select(s => new StrategyOption(s, GetStrategyName(s), GetStrategyDescription(s)))
            .ToList();

        CategoryOptions = new List<TopCategoryOption>
        {
            new(null, "（不指定）"),
        }.Concat(
            System.Enum.GetValues<TopCategory>()
                .Select(c => new TopCategoryOption(c, c.GetCnName()))
        ).ToList();

        foreach (var w in AvailableWebsites)
            PriorityChoices.Add(new WebsitePriorityChoiceVm(this, w));

        RestoreFromOptions(initialOptions);
    }

    // ---------- 命令 ----------

    [RelayCommand]
    private void AddWebsiteId()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(NewWebsiteName))
        {
            ErrorMessage = "请选择网站名称";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewWebsiteId))
        {
            ErrorMessage = "请输入网站 ID";
            return;
        }

        if (WebsiteIds.Any(e => string.Equals(e.WebsiteName, NewWebsiteName, System.StringComparison.OrdinalIgnoreCase)))
        {
            ErrorMessage = $"网站 {NewWebsiteName} 已存在，请先删除旧映射";
            return;
        }

        WebsiteIds.Add(new WebsiteIdEntryVm(NewWebsiteName!, NewWebsiteId!));
        NewWebsiteName = null;
        NewWebsiteId = null;
        OnPropertyChanged(nameof(HasWebsiteIds));
        OnPropertyChanged(nameof(WebsiteIdsCountText));
    }

    [RelayCommand]
    private void RemoveWebsiteId(WebsiteIdEntryVm? entry)
    {
        if (entry is null) return;
        WebsiteIds.Remove(entry);
        OnPropertyChanged(nameof(HasWebsiteIds));
        OnPropertyChanged(nameof(WebsiteIdsCountText));
    }

    [RelayCommand]
    private void Reset()
    {
        ErrorMessage = null;
        RestoreFromOptions(_initialOptions);
    }

    // ---------- 状态恢复 + 构建 ----------

    private void RestoreFromOptions(IdentificationOptions opt)
    {
        PreferredWebsite = opt.PreferredWebsite;
        WebsiteSpecificId = opt.WebsiteSpecificId;
        CustomIdentificationName = opt.CustomIdentificationName;
        Strategy = StrategyOptions.FirstOrDefault(s => s.Value == opt.Strategy);
        SuggestedCategory = opt.SuggestedCategory.HasValue
            ? CategoryOptions.FirstOrDefault(c => c.Value == opt.SuggestedCategory.Value)
            : CategoryOptions.FirstOrDefault(c => c.Value == null);

        WebsiteIds.Clear();
        if (opt.WebsiteIds is { Count: > 0 })
        {
            foreach (var kv in opt.WebsiteIds)
                WebsiteIds.Add(new WebsiteIdEntryVm(kv.Key, kv.Value));
        }

        SkipCache = opt.SkipCache;
        TimeoutSeconds = (int)System.Math.Clamp(opt.Timeout.TotalSeconds, 5, 120);

        // 优先级：按 opt.WebsitePriorityOverride 顺序设 SelectionOrder
        foreach (var c in PriorityChoices) c.ResetSelectionWithoutNotify();
        if (opt.WebsitePriorityOverride is { Count: > 0 })
        {
            for (var i = 0; i < opt.WebsitePriorityOverride.Count; i++)
            {
                var name = opt.WebsitePriorityOverride[i];
                var match = PriorityChoices.FirstOrDefault(c => c.WebsiteName == name);
                if (match != null) match.MarkSelected(i);
            }
        }
        OnPropertyChanged(nameof(PriorityDisplay));
        OnPropertyChanged(nameof(HasWebsiteIds));
        OnPropertyChanged(nameof(WebsiteIdsCountText));
    }

    /// <summary>把 UI 字段写回 IdentificationOptions；返回 null 表示验证失败（错误文案已写入 <see cref="ErrorMessage"/>）。</summary>
    public IdentificationOptions? BuildOptions()
    {
        var result = _initialOptions.Clone();
        result.PreferredWebsite = string.IsNullOrWhiteSpace(PreferredWebsite) ? null : PreferredWebsite;
        result.WebsiteSpecificId = string.IsNullOrWhiteSpace(WebsiteSpecificId) ? null : WebsiteSpecificId;
        result.CustomIdentificationName = string.IsNullOrWhiteSpace(CustomIdentificationName) ? null : CustomIdentificationName;
        result.WebsiteIds = WebsiteIds.Count > 0
            ? WebsiteIds.ToDictionary(e => e.WebsiteName, e => e.WebsiteId)
            : null;
        result.SkipCache = SkipCache;
        result.Timeout = System.TimeSpan.FromSeconds(TimeoutSeconds);
        result.Strategy = Strategy?.Value ?? IdentificationStrategy.Auto;
        result.SuggestedCategory = SuggestedCategory?.Value;
        result.WebsitePriorityOverride = PriorityChoices
            .Where(c => c.IsSelected)
            .OrderBy(c => c.SelectionOrder)
            .Select(c => c.WebsiteName)
            .ToList() switch
        {
            { Count: 0 } => null,
            var list => list,
        };
        result.SourcePath = SourcePath;

        var validation = result.Validate();
        if (!validation.IsValid)
        {
            ErrorMessage = validation.GetErrorMessage();
            return null;
        }

        return result;
    }

    /// <summary>子 VM（优先级选项）调用此通知主 VM 重算 PriorityDisplay。</summary>
    internal void OnPrioritySelectionChanged() => OnPropertyChanged(nameof(PriorityDisplay));

    private static string GetStrategyName(IdentificationStrategy s) => s switch
    {
        IdentificationStrategy.Auto => "自动",
        IdentificationStrategy.Manual => "手动",
        IdentificationStrategy.Hybrid => "混合",
        IdentificationStrategy.ForceRefresh => "强制刷新",
        IdentificationStrategy.CacheOnly => "仅缓存",
        IdentificationStrategy.Quick => "快速",
        _ => s.ToString()
    };

    private static string GetStrategyDescription(IdentificationStrategy s) => s switch
    {
        IdentificationStrategy.Auto => "按默认流程识别",
        IdentificationStrategy.Manual => "使用指定的网站和 ID",
        IdentificationStrategy.Hybrid => "先尝试手动指定，失败后自动识别",
        IdentificationStrategy.ForceRefresh => "忽略缓存重新识别",
        IdentificationStrategy.CacheOnly => "只从缓存获取",
        IdentificationStrategy.Quick => "更激进的超时和并行策略",
        _ => string.Empty
    };
}

public sealed record StrategyOption(IdentificationStrategy Value, string Name, string Description)
{
    public string Display => $"{Name}  ·  {Description}";
}

public sealed record TopCategoryOption(TopCategory? Value, string Display);

public partial class WebsiteIdEntryVm : ObservableObject
{
    public string WebsiteName { get; }
    public string WebsiteId { get; }
    public string Display => $"{WebsiteName}  ·  {WebsiteId}";

    public WebsiteIdEntryVm(string websiteName, string websiteId)
    {
        WebsiteName = websiteName;
        WebsiteId = websiteId;
    }
}

public partial class WebsitePriorityChoiceVm : ObservableObject
{
    private readonly IdentificationOptionsDialogContext _parent;

    public string WebsiteName { get; }

    [ObservableProperty] private bool _isSelected;

    /// <summary>用户选中先后顺序：第一个选中=0，第二个=1 等；未选中时为 -1。</summary>
    public int SelectionOrder { get; private set; } = -1;

    public WebsitePriorityChoiceVm(IdentificationOptionsDialogContext parent, string websiteName)
    {
        _parent = parent;
        WebsiteName = websiteName;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            // 新选中：拿到当前已选项中最大 order + 1
            var maxOrder = -1;
            foreach (var c in _parent.PriorityChoices)
                if (c.IsSelected && c != this && c.SelectionOrder > maxOrder)
                    maxOrder = c.SelectionOrder;
            SelectionOrder = maxOrder + 1;
        }
        else
        {
            var removedOrder = SelectionOrder;
            SelectionOrder = -1;
            // 把比 removedOrder 大的全部前移 1，保持紧凑
            if (removedOrder >= 0)
            {
                foreach (var c in _parent.PriorityChoices)
                    if (c.IsSelected && c.SelectionOrder > removedOrder)
                        c.SelectionOrder--;
            }
        }
        _parent.OnPrioritySelectionChanged();
    }

    /// <summary>RestoreFromOptions 调用：跳过 OnChanged 副作用，仅复位状态。</summary>
    internal void ResetSelectionWithoutNotify()
    {
        SelectionOrder = -1;
        SetProperty(ref _isSelected, false, nameof(IsSelected));
    }

    /// <summary>RestoreFromOptions 调用：按指定 order 标记为选中。</summary>
    internal void MarkSelected(int order)
    {
        SelectionOrder = order;
        SetProperty(ref _isSelected, true, nameof(IsSelected));
    }
}
