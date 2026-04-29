using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Components.Tags;

public partial class TagAdder : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; }
    
    [Parameter]
    public TopTag TopTag { get; set; }
    
    [Parameter]
    public List<string> TagNameList { get; set; }

}