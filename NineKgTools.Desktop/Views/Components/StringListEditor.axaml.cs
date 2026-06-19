using Avalonia.Controls;

namespace NineKgTools.Desktop.Views.Components;

/// <summary>
/// 通用字符串列表 chip 编辑器（VM 驱动，DataContext = <see cref="ViewModels.StringListEditorViewModel"/>）。
/// 现有项是带 ✕ 的 chip，末尾输入框回车 / ﹢ 添加，另有"恢复默认"。
/// 设置页「文件过滤 → 高级过滤规则」三组复用此控件。增删 / 持久化逻辑全在 VM + 宿主，本类无 code-behind 行为。
/// </summary>
public partial class StringListEditor : UserControl
{
    public StringListEditor()
    {
        InitializeComponent();
    }
}
