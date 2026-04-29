using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Tags;

public partial class TopTagAdder : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; }
    
    [Parameter]
    public List<string> TopTagNameList { get; set; }

}