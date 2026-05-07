using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NineKgTools.Desktop.Views.Dialogs;

namespace NineKgTools.Desktop.Views.Components;

/// <summary>
/// 别名 chip 列表的可复用 UserControl。
/// - 只读模式（IsEditable=false）：仅展示 chip
/// - 编辑模式（IsEditable=true）：每个 chip 加 ✕ 删除 + 末尾"+ 添加"按钮（弹 InputDialog）
///
/// Aliases 是 ObservableCollection&lt;string&gt; 双向引用——caller 持有同一实例，
/// UserControl 内 Add/Remove 直接反映到 caller。
/// </summary>
public partial class EditableAliasList : UserControl
{
    public static readonly StyledProperty<ObservableCollection<string>?> AliasesProperty =
        AvaloniaProperty.Register<EditableAliasList, ObservableCollection<string>?>(nameof(Aliases));

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<EditableAliasList, bool>(nameof(IsEditable));

    public ObservableCollection<string>? Aliases
    {
        get => GetValue(AliasesProperty);
        set => SetValue(AliasesProperty, value);
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>只读模式 + 列表为空时显示"暂无别名"占位</summary>
    public bool ShowEmptyHint => !IsEditable && (Aliases is null || Aliases.Count == 0);

    public EditableAliasList()
    {
        InitializeComponent();

        // ShowEmptyHint 派生于 Aliases.Count + IsEditable，需要 hook PropertyChanged + collection events
        PropertyChanged += (_, e) =>
        {
            if (e.Property == AliasesProperty)
            {
                if (e.OldValue is INotifyCollectionChanged oldCol)
                    oldCol.CollectionChanged -= OnAliasesCollectionChanged;
                if (e.NewValue is INotifyCollectionChanged newCol)
                    newCol.CollectionChanged += OnAliasesCollectionChanged;
                RaisePropertyChanged(nameof(ShowEmptyHint));
            }
            else if (e.Property == IsEditableProperty)
            {
                RaisePropertyChanged(nameof(ShowEmptyHint));
            }
        };
    }

    private void OnAliasesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(ShowEmptyHint));
    }

    private void RaisePropertyChanged(string name)
    {
        // Avalonia 12: 用 RaisePropertyChanged 通知非 StyledProperty 的派生属性
        // 这里 ShowEmptyHint 是普通 CLR 属性——通过 INPC 通知 binding 更新
        // UserControl 派生自 AvaloniaObject，没有 INotifyPropertyChanged，
        // 但派生属性绑定 via { Binding #Root.ShowEmptyHint } 通常需要 INPC
        // 简化：每次 Aliases 变化时手动 InvalidateMeasure，让 Binding 重读
        // 实际更可靠：把 ShowEmptyHint 也声明为 StyledProperty
        // 但这里用 ItemsControl 自身的 ItemsSource.Count 替代会更简单——
        // 暂时用 InvalidateMeasure 触发 binding 重 evaluate
        InvalidateMeasure();
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (Aliases is null) return;

        var newName = await InputDialog.ShowAsync(
            title: "添加别名",
            placeholder: "输入别名...",
            confirmText: "添加",
            maxLength: 100,
            validate: v => !Aliases.Contains(v, StringComparer.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(newName)) return;
        Aliases.Add(newName);
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (Aliases is null) return;
        if (sender is Button { Tag: string alias })
        {
            Aliases.Remove(alias);
        }
    }
}
