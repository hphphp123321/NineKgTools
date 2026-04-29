using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Tags;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using Serilog;

namespace NineKgTools.Pages.Tags;

public enum TagSortMode
{
    IdAsc, // 按编号升序排序
    IdDesc, // 按编号降序排序
    NameAsc, // 按名称升序排序
    NameDesc, // 按名称降序排序
    CountAsc, // 按数量升序排序
    CountDesc, // 按数量降序排序
}

public partial class TagsPage : ComponentBase
{
    [Inject] private TagService TagService { get; set; } = null!;
    [Inject] private MediaService MediaService { get; set; } = null!;

    private List<TopTag> _shownTopTags = [];
    private HashSet<int> _loadingTagIds = new();
    private bool _isEditMode = false;
    private Dictionary<int, bool> _topTagExpandedStates = new();
    private const int LoadDelay = 150;
    private int _loadedTagCount = 0;
    private int _totalTagCount = 0;

    private static readonly DialogOptions DefaultDialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
    };

    private bool IsTopTagExpanded(int topTagId)
    {
        _topTagExpandedStates.TryAdd(topTagId, true);
        return _topTagExpandedStates[topTagId];
    }

    private void ToggleTopTagExpanded(int topTagId)
    {
        _topTagExpandedStates.TryAdd(topTagId, true);
        _topTagExpandedStates[topTagId] = !_topTagExpandedStates[topTagId];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadTopTags();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    // 加载所有顶层标签
    private async Task LoadTopTags()
    {
        try
        {
            _loadedTagCount = 0;
            StateHasChanged();

            // 加载数据库中的所有顶层标签
            _shownTopTags = await TagService.GetCopiedTopTagsAsync();
            
            // 确保每个顶层标签的标签集合被初始化为空
            foreach (var topTag in _shownTopTags)
            {
                topTag.Tags = new List<Tag>();
            }
            
            _totalTagCount = _shownTopTags.Count;
            StateHasChanged();

            // 异步加载每个顶层标签的标签
            await LoadTopTagsSequentially();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载顶层标签失败");
            SnackBar.Add("加载标签失败", Severity.Error);
        }
    }

    // 按顺序加载顶层标签
    private async Task LoadTopTagsSequentially()
    {
        try
        {
            foreach (var topTag in _shownTopTags)
            {
                await LoadTags(topTag.Id);
                _loadedTagCount++;
                StateHasChanged();
                await Task.Delay(LoadDelay);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "顺序加载标签失败");
            SnackBar.Add("加载标签失败", Severity.Error);
        }
    }

    // 加载所有标签（可选过滤）
    private async Task LoadAllTags(string? filter = null)
    {
        try
        {
            _loadedTagCount = 0;
            
            // 清空所有标签并显示加载动画
            foreach (var topTag in _shownTopTags)
            {
                topTag.Tags = new List<Tag>();
            }
            
            StateHasChanged();
            
            foreach (var shownTopTag in _shownTopTags)
            {
                await LoadTags(shownTopTag.Id, filter);
                _loadedTagCount++;
                StateHasChanged();
                await Task.Delay(LoadDelay);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载所有标签失败");
            SnackBar.Add("加载标签失败", Severity.Error);
        }
    }

    // 根据顶层标签ID加载标签
    private async Task LoadTags(int topTagId, string? filter = null)
    {
        try 
        {
            // 获取顶层标签
            var shownTopTag = _shownTopTags.FirstOrDefault(t => t.Id == topTagId);
            if (shownTopTag == null)
            {
                return;
            }

            var tags = await TagService.GetTagsByTopTagIdAsync(topTagId);

            // 如果有过滤条件，进行过滤
            if (!string.IsNullOrEmpty(filter))
            {
                tags = tags.Where(t => t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 根据排序模式进行排序
            tags = _sortMode switch
            {
                TagSortMode.IdAsc => tags.OrderBy(t => t.Id).ToList(),
                TagSortMode.IdDesc => tags.OrderByDescending(t => t.Id).ToList(),
                TagSortMode.NameAsc => tags.OrderBy(t => t.Name).ToList(),
                TagSortMode.NameDesc => tags.OrderByDescending(t => t.Name).ToList(),
                TagSortMode.CountAsc => tags.OrderBy(t => t.Count).ToList(),
                TagSortMode.CountDesc => tags.OrderByDescending(t => t.Count).ToList(),
                _ => tags
            };

            shownTopTag.Tags = tags;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载标签失败: {TopTagId}", topTagId);
            SnackBar.Add($"加载标签失败: {ex.Message}", Severity.Error);
        }
    }
    
    // 清除所有标签并重新加载
    private async Task ClearAllTags()
    {
        _tagNameFilter = "";
        await LoadTopTags();
    }

    private async Task AddTopTag()
    {
        var parameters = new DialogParameters<TopTagAdder>
            { { x => x.TopTagNameList, _shownTopTags.Select(x => x.Name).ToList() } };

        var dialog = await DialogService.ShowAsync<TopTagAdder>("添加顶层标签分类", parameters, DefaultDialogOptions);
        var result = await dialog.Result;

        if (result is not { Canceled: false, Data: TopTag topTag })
            return;

        await TagService.AddTopTagAsync(topTag);
        SnackBar.Add("添加成功", Severity.Success);
        await LoadTopTags();
    }

    private async Task RemoveTopTag(TopTag topTag)
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "删除顶层分类",
            "该顶层分类下的所有子标签会被一起删除。相关媒体的标签会解除关联。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: topTag.Name,
            targetIcon: Icons.Material.Filled.Category,
            warningLine: "包含子标签，删除后无法恢复");

        if (!confirmed) return;

        await TagService.RemoveTopTagAsync(topTag);
        SnackBar.Add($"删除顶层标签{topTag.Name}成功", Severity.Success);
        await LoadTopTags();
    }

    private async Task EditTopTag(TopTag topTag)
    {
        var parameters = new DialogParameters<TopTagEditor>
        {
            { x => x.TopTag, topTag }
        };

        var dialog = await DialogService.ShowAsync<TopTagEditor>("编辑顶层标签分类", parameters, DefaultDialogOptions);
        var result = await dialog.Result;

        if (result is null or { Canceled: true }) return;

        await TagService.UpdateTopTagAsync(topTag);
        SnackBar.Add("编辑成功", Severity.Success);
        await LoadTopTags();
    }

    private async Task AddTag(TopTag topTag)
    {
        var parameters = new DialogParameters<TagAdder>
        {
            { x => x.TagNameList, topTag.Tags.Select(x => x.Name).ToList() },
            { x => x.TopTag, topTag }
        };

        var dialog = await DialogService.ShowAsync<TagAdder>("添加标签", parameters, DefaultDialogOptions);
        var result = await dialog.Result;

        if (result is not { Canceled: false, Data: Tag tag })
            return;

        await TagService.AddTagAsync(tag);
        SnackBar.Add("添加成功", Severity.Success);
        await LoadTopTags();
    }

    private async Task RemoveTag(Tag tag)
    {
        try
        {
            _loadingTagIds.Add(tag.Id);
            StateHasChanged();

            var confirmed = await NineKgConfirmDialog.ShowAsync(
                DialogService,
                "删除标签",
                "标签会从所有关联的媒体上移除，媒体本身不受影响。",
                intent: ConfirmIntent.Destructive,
                confirmText: "删除",
                targetName: tag.Name,
                targetIcon: Icons.Material.Filled.LocalOffer);

            if (!confirmed) return;

            await TagService.RemoveTagAsync(tag);
            SnackBar.Add("删除成功", Severity.Success);
            await LoadAllTags(_tagNameFilter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除标签失败: {TagId}", tag.Id);
            SnackBar.Add("删除标签失败，请稍后重试。", Severity.Error);
        }
        finally
        {
            _loadingTagIds.Remove(tag.Id);
            StateHasChanged();
        }
    }

    private async Task EditTag(Tag tag)
    {
        try
        {
            _loadingTagIds.Add(tag.Id);
            StateHasChanged();

            var mediaTasks = tag.Medias.Select((m, i) => new { Index = i, Task = MediaService.GetMediaAsync(m.Id) }).ToList();
            await Task.WhenAll(mediaTasks.Select(t => t.Task));
            foreach (var mt in mediaTasks)
            {
                tag.Medias[mt.Index] = await mt.Task;
            }

            var parameters = new DialogParameters<TagEditor>
            {
                { x => x.Tag, tag.Copy() }
            };

            var dialog = await DialogService.ShowAsync<TagEditor>("编辑标签", parameters, DefaultDialogOptions);
            var result = await dialog.Result;

            if (result is not { Canceled: false, Data: Tag editedTag })
                return;

            await TagService.UpdateTagAsync(editedTag);
            SnackBar.Add("编辑成功", Severity.Success);
            await LoadAllTags(_tagNameFilter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "编辑标签失败: {TagId}", tag.Id);
            SnackBar.Add($"编辑标签失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loadingTagIds.Remove(tag.Id);
            StateHasChanged();
        }
    }
}