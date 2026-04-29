using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Components.Tags;

public partial class TagEditor : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public required Tag Tag { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    string _tagName;
    string _tagDescription;

    protected override void OnInitialized()
    {
        _tagName = Tag.Name;
        _tagDescription = Tag.Description ?? "";
        base.OnInitialized();
    }

    /// <summary>
    /// 跳转到标签详情页面
    /// </summary>
    private void NavigateToTagPage()
    {
        // 关闭对话框
        MudDialog.Cancel();
        // 跳转到标签详情页
        Navigation.NavigateTo($"/tag/{Tag.Id}");
    }
}