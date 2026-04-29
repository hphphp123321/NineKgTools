using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Components.Tags;

public partial class TopTagEditor : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; }
    
    [Parameter]
    public required TopTag TopTag { get; set; }
    
    
    string _topTagName;
    protected override void OnInitialized()
    {
        _topTagName = TopTag.Name;
        base.OnInitialized();
    }
}