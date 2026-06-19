using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NineKgTools.Desktop.ViewModels;

/// <summary>
/// 通用"字符串列表 chip 编辑器"的 VM：现有项是带 ✕ 的 chip，末尾输入框回车 / ﹢ 添加。
/// 设置页「文件过滤 → 高级过滤规则」三组（忽略文件名 / 忽略模式 / 允许扩展名）各持一个实例。
///
/// 自身只管 Items / 输入 / 增删/重置；持久化由宿主（SettingsViewModel）订阅
/// <see cref="ObservableCollection{T}.CollectionChanged"/> 写回 config + 防抖落盘。
/// </summary>
public partial class StringListEditorViewModel : ObservableObject
{
    /// <summary>当前条目（chip）。增删直接操作它，宿主订阅 CollectionChanged 落盘。</summary>
    public ObservableCollection<string> Items { get; } = new();

    /// <summary>输入框文本，回车 / ﹢ 提交。</summary>
    [ObservableProperty]
    private string _newEntry = "";

    /// <summary>输入框 placeholder。</summary>
    public string Placeholder { get; }

    /// <summary>列表为空时的提示（如"留空 = 允许所有扩展名"）；null 表示不显示空提示。</summary>
    public string? EmptyHint { get; }

    /// <summary>列表为空且配置了 EmptyHint 时显示。CollectionChanged 时手动 notify。</summary>
    public bool ShowEmptyHint => EmptyHint != null && Items.Count == 0;

    /// <summary>"恢复默认"用的默认值集合。</summary>
    private readonly IReadOnlyList<string> _defaults;

    /// <summary>添加前的规范化（如扩展名统一小写 + 补 "." 前缀）；null 表示仅 Trim。</summary>
    private readonly Func<string, string>? _normalizer;

    public StringListEditorViewModel(string placeholder, string? emptyHint,
        IReadOnlyList<string> defaults, Func<string, string>? normalizer = null)
    {
        Placeholder = placeholder;
        EmptyHint = emptyHint;
        _defaults = defaults;
        _normalizer = normalizer;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowEmptyHint));
    }

    /// <summary>用给定值填充（不触发额外副作用——宿主在 _suppressSave 期调用）。</summary>
    public void SetItems(IEnumerable<string>? values)
    {
        Items.Clear();
        if (values == null) return;
        foreach (var v in values) Items.Add(v);
    }

    [RelayCommand]
    private void Add()
    {
        var v = (_normalizer?.Invoke(NewEntry) ?? NewEntry.Trim());
        if (string.IsNullOrWhiteSpace(v)) { NewEntry = ""; return; }
        // 去重（不区分大小写）
        if (!Items.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
            Items.Add(v);
        NewEntry = "";
    }

    [RelayCommand]
    private void Remove(string? item)
    {
        if (item != null) Items.Remove(item);
    }

    [RelayCommand]
    private void Reset()
    {
        Items.Clear();
        foreach (var d in _defaults) Items.Add(d);
    }
}
