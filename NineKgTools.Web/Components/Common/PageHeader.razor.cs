using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Common;

public partial class PageHeader : ComponentBase
{
    [Parameter, EditorRequired] public string Icon { get; set; } = string.Empty;
    [Parameter] public Color IconColor { get; set; } = Color.Primary;
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public RenderFragment? SubtitleContent { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>
    /// 标题排版级别。页面顶层 PageHeader 应使用 Typo.h1 以满足 WCAG 1.3.1（每页一个 h1）。
    /// 默认 Typo.h4 保持向后兼容；页面级头部应显式传入 <c>Typo.h1</c>。
    /// </summary>
    [Parameter] public Typo TitleTypo { get; set; } = Typo.h4;
}
