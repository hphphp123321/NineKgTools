using Avalonia.Controls;

namespace NineKgTools.Desktop.Views.Pages;

/// <summary>
/// 主窗内嵌版媒体详情页（默认入口——点 MediaCard / 跳关联媒体都走这里）。
///
/// 由 <c>NavigationService.NavigateToAsync&lt;MediaDetailViewModel&gt;(...)</c> 触发，
/// ViewLocator 通过命名约定（<c>MediaDetailViewModel</c> ↔ <c>MediaDetailPage</c>）自动渲染。
///
/// 内部布局 = 顶部 nav bar（[← 返回] [标题] [↗ 在新窗口]）+ 共享的 <c>MediaDetailContent</c> 主体。
/// 独立窗版（<c>MediaDetailWindow</c>）也包同一份 <c>MediaDetailContent</c>，差异由 VM Mode 控制。
/// </summary>
public partial class MediaDetailPage : UserControl
{
    public MediaDetailPage() => InitializeComponent();
}
