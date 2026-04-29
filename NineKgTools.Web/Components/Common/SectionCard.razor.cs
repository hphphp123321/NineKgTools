using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Common;

public partial class SectionCard : ComponentBase
{
    [Parameter, EditorRequired] public string Icon { get; set; } = string.Empty;
    [Parameter] public Color IconColor { get; set; } = Color.Primary;
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public int Elevation { get; set; } = 3;
    [Parameter] public string? Class { get; set; }
    [Parameter] public string ContentClass { get; set; } = "py-4";
    [Parameter] public bool ShowDivider { get; set; } = true;
    [Parameter] public RenderFragment? HeaderActions { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private string CombinedClass =>
        string.IsNullOrEmpty(Class) ? "rounded-lg" : $"rounded-lg {Class}";
}
