using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Components.Creators;

public partial class EditableCreatorList : ComponentBase
{
    [Inject] private IDialogService DialogService { get; set; } = null!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string Icon { get; set; } = Icons.Material.Filled.Person;
    [Parameter] public Color Color { get; set; } = Color.Primary;
    [Parameter] public List<Creator>? Creators { get; set; }
    [Parameter] public EventCallback<List<Creator>> CreatorsChanged { get; set; }
    [Parameter] public bool Editable { get; set; } = true;
    [Parameter] public CreatorType? FilterByType { get; set; }

    private async Task OpenCreatorSelector()
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        var parameters = new DialogParameters<CreatorSelectorDialog>
        {
            { nameof(CreatorSelectorDialog.InitialSelectedCreators), Creators?.ToList() ?? new List<Creator>() },
            { nameof(CreatorSelectorDialog.AllowMultiSelect), true },
            { nameof(CreatorSelectorDialog.FilterByType), FilterByType }
        };

        var dialog = await DialogService.ShowAsync<CreatorSelectorDialog>("选择创作者", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is List<Creator> selectedCreators)
        {
            Creators = selectedCreators;
            await CreatorsChanged.InvokeAsync(Creators);
            StateHasChanged();
        }
    }

    private async Task RemoveCreator(Creator creator)
    {
        if (Creators == null) return;

        Creators.Remove(creator);
        await CreatorsChanged.InvokeAsync(Creators);
        StateHasChanged();
    }
}
