using Avalonia.Controls;

namespace NineKgTools.Desktop.Views.Components;

/// <summary>
/// 媒体详情完整 UI 主体（Hero / 图片画廊 / 右栏元数据）—— 从原 MediaDetailWindow 抽出。
///
/// 同一份 UI 被两个 host 复用：
/// 1) <c>Views/Windows/MediaDetailWindow</c>（独立窗）—— 用 OS chrome + 图钉
/// 2) <c>Views/Pages/MediaDetailPage</c>（主窗内嵌）—— 加顶部 nav bar (← / 标题 / ↗)
///
/// 按 <c>MediaDetailViewModel.Mode</c> 切换部分按钮可见性（图钉 / pop-out 等）。
/// </summary>
public partial class MediaDetailContent : UserControl
{
    public MediaDetailContent() => InitializeComponent();
}
