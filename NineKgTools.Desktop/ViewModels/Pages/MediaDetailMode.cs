namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体详情 VM 跑在哪种 host 里。决定 UI 中图钉 / pop-out / nav bar 等 host-specific 元素的可见性，
/// 以及 OpenRelatedMedia 的跳转分支。
/// </summary>
public enum MediaDetailMode
{
    /// <summary>默认：作为主窗内嵌页（由 NavigationService 渲染到 ContentControl）。
    /// 与 Web /media/{id} 体验一致，点关联媒体走主窗导航 + 历史栈支持 ← 返回</summary>
    EmbeddedPage,

    /// <summary>独立窗口（用户主动点 [↗] 弹出）。带 OS chrome + 图钉，
    /// 点关联媒体走同窗 LoadAsync 替换，不拉动主窗</summary>
    IndependentWindow,
}
