using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using Serilog;

namespace NineKgTools.Components.Creators;

public partial class CreatorSelectorDialog : ComponentBase
{
    // 性能边界：客户端单次渲染的创作者上限，与 CreatorService.SearchCreatorsByNameAsync 的 maxResults 对齐
    private const int MaxRenderedCreators = 50;

    // 触发服务端搜索的最小字符数，避免单字符（尤其是中文 IME 组字阶段）引起的无效查询风暴
    private const int MinSearchLength = 2;

    // 只有通过 DialogService.ShowAsync 弹出时才级联得到；直接嵌入页面时为 null
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public bool AllowMultiSelect { get; set; } = true;
    [Parameter] public List<Creator> InitialSelectedCreators { get; set; } = new();
    [Parameter] public CreatorType? FilterByType { get; set; }
    [Parameter] public EventCallback<List<Creator>> OnCreatorsSelected { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    [Inject] private CreatorService CreatorService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isLoading;
    private string _searchTerm = string.Empty;
    private CreatorType? _selectedType;
    private List<Creator> _filteredCreators = new();
    private List<Creator> _tempSelectedCreators = new();

    // 选中集合的 O(1) 索引，IsCreatorSelected 由 O(n) 降为 O(1)，
    // 配合 _tempSelectedCreators 使用（列表保持插入顺序与 Dialog 返回结果一致）
    private readonly HashSet<int> _selectedIds = new();

    // 结果是否被客户端 MaxRenderedCreators 截断；用于向用户说明"还有更多匹配，请缩小范围"
    private bool _isResultsTruncated;

    // 新增Creator相关
    private bool _showAddCreatorPanel;
    private string _newCreatorName = string.Empty;
    private List<CreatorType> _newCreatorTypes = new();
    private MudTextField<string>? _newCreatorNameField;

    protected override async Task OnInitializedAsync()
    {
        _tempSelectedCreators = new List<Creator>(InitialSelectedCreators);
        _selectedIds.Clear();
        foreach (var creator in _tempSelectedCreators)
        {
            _selectedIds.Add(creator.Id);
        }
        _selectedType = FilterByType;
        await LoadCreators();
    }

    // 始终通过 MaxRenderedCreators 客户端封顶——服务端 SearchCreatorsByNameAsync 已有 maxResults，
    // 但空搜索词路径会绕道 GetAllCreatorsAsync 全表，必须在这里兜底
    private async Task LoadCreators()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var creators = string.IsNullOrWhiteSpace(_searchTerm)
                ? await CreatorService.GetAllCreatorsAsync()
                : await CreatorService.SearchCreatorsByNameAsync(_searchTerm, MaxRenderedCreators);

            // 按类型筛选（服务端没有组合查询时的客户端兜底）
            if (_selectedType.HasValue)
            {
                creators = creators.Where(c => c.Types.Contains(_selectedType.Value)).ToList();
            }

            // 客户端上限：即使返回几千条也只渲染 N 张卡片
            _isResultsTruncated = creators.Count > MaxRenderedCreators;
            _filteredCreators = _isResultsTruncated
                ? creators.Take(MaxRenderedCreators).ToList()
                : creators;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载创作者失败 SearchTerm={SearchTerm} Type={Type}", _searchTerm, _selectedType);
            Snackbar.Add("加载创作者失败，请稍后重试。", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // 少于 MinSearchLength 字符时不查询，过滤掉 IME 组字和用户手抖的瞬态
    // （但清空回到空态仍视为有效操作，展示首屏全量）
    private async Task HandleSearchTermChanged()
    {
        var trimmed = _searchTerm?.Trim() ?? string.Empty;
        if (trimmed.Length > 0 && trimmed.Length < MinSearchLength)
        {
            return;
        }
        await LoadCreators();
    }

    private async Task HandleTypeFilterChanged(CreatorType? type)
    {
        _selectedType = type;
        await LoadCreators();
    }

    // 列表保留插入顺序（作为 Dialog 返回结果），HashSet 负责 O(1) 查重，两者同步更新
    private void ToggleCreator(Creator creator)
    {
        if (_selectedIds.Contains(creator.Id))
        {
            _tempSelectedCreators.RemoveAll(c => c.Id == creator.Id);
            _selectedIds.Remove(creator.Id);
        }
        else
        {
            if (!AllowMultiSelect)
            {
                _tempSelectedCreators.Clear();
                _selectedIds.Clear();
            }
            _tempSelectedCreators.Add(creator);
            _selectedIds.Add(creator.Id);
        }

        StateHasChanged();
    }

    private static string GetCardCssClass(bool isSelected) =>
        isSelected ? "creator-card creator-card--selected" : "creator-card";

    private static string GetCardAriaLabel(Creator creator, bool isSelected) =>
        isSelected ? $"{creator.Name}，已选中" : creator.Name;

    // WAI-ARIA button 模式：Enter 和 Space 都等同于 click
    private void OnCreatorCardKeyDown(KeyboardEventArgs e, Creator creator)
    {
        if (e.Key == "Enter" || e.Key == " " || e.Key == "Spacebar")
        {
            ToggleCreator(creator);
        }
    }

    private bool IsCreatorSelected(Creator creator) => _selectedIds.Contains(creator.Id);

    // 打开时重置表单（避免上次残留），并等动画结束后把焦点移到名称输入
    private async Task ToggleAddCreatorPanel()
    {
        _showAddCreatorPanel = !_showAddCreatorPanel;

        if (_showAddCreatorPanel)
        {
            _newCreatorName = string.Empty;
            _newCreatorTypes = new List<CreatorType>();
            StateHasChanged();
            // 等 MudCollapse 动画完成后再聚焦，否则 focus 会掉到不可见的输入上
            await Task.Delay(120);
            if (_newCreatorNameField != null)
            {
                await _newCreatorNameField.FocusAsync();
            }
        }
    }

    // 名称由 TextField 的 Required + 按钮 Disabled 双重拦截，这里只做 null/trim 兜底
    private async Task CreateNewCreator()
    {
        var name = _newCreatorName?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            var newCreator = await CreatorService.CreateCreatorAsync(name, _newCreatorTypes);

            // 新建后自动加入已选；HashSet.Add 的返回值替代了去重查询
            if (_selectedIds.Add(newCreator.Id))
            {
                _tempSelectedCreators.Add(newCreator);
            }

            Snackbar.Add($"已创建创作者：{newCreator.Name}", Severity.Success);

            // 重置表单并刷新列表
            _newCreatorName = string.Empty;
            _newCreatorTypes = new List<CreatorType>();
            _showAddCreatorPanel = false;
            await LoadCreators();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建创作者失败 Name={Name} Types={@Types}", name, _newCreatorTypes);
            Snackbar.Add("创建创作者失败，请稍后重试。", Severity.Error);
        }
    }

    private void ToggleCreatorType(CreatorType type)
    {
        if (!_newCreatorTypes.Remove(type))
        {
            _newCreatorTypes.Add(type);
        }
    }

    // 多选 + 0 选时"确定"实际是"清空"，用显式文案避免用户以为点了没事
    private string GetConfirmButtonLabel()
    {
        if (!AllowMultiSelect)
        {
            return "确定";
        }

        var count = _tempSelectedCreators.Count;
        return count == 0 ? "清空并确定" : $"确定（{count} 位）";
    }

    // Confirm/Cancel 走双路径：弹窗模式用 MudDialog 的 Close/Cancel 返回结果；
    // 直接嵌入页面模式退回 EventCallback（见 MudDialog 的 nullable 注释）
    private async Task Confirm()
    {
        if (MudDialog != null)
        {
            MudDialog.Close(DialogResult.Ok(_tempSelectedCreators));
        }
        else
        {
            await OnCreatorsSelected.InvokeAsync(_tempSelectedCreators);
        }
    }

    private async Task Cancel()
    {
        if (MudDialog != null)
        {
            MudDialog.Cancel();
        }
        else
        {
            await OnCancel.InvokeAsync();
        }
    }

    private string GetCreatorTypeName(CreatorType type) => type switch
    {
        CreatorType.Author => "作者",
        CreatorType.Illustrator => "画师",
        CreatorType.Musician => "音乐",
        CreatorType.ScreenWriter => "编剧",
        CreatorType.VoiceActor => "声优",
        CreatorType.Director => "导演",
        CreatorType.Actor => "演员",
        _ => type.ToString()
    };

    private string GetCreatorTypeIcon(CreatorType type) => type switch
    {
        CreatorType.Author => Icons.Material.Filled.Create,
        CreatorType.Illustrator => Icons.Material.Filled.Brush,
        CreatorType.Musician => Icons.Material.Filled.MusicNote,
        CreatorType.ScreenWriter => Icons.Material.Filled.Edit,
        CreatorType.VoiceActor => Icons.Material.Filled.RecordVoiceOver,
        CreatorType.Director => Icons.Material.Filled.MovieFilter,
        CreatorType.Actor => Icons.Material.Filled.Person,
        _ => Icons.Material.Filled.Person
    };

    // 刻意避开 Error / Warning —— 它们是语义色，会让"演员"或"画师" chip 被误读为错误态
    private Color GetCreatorTypeColor(CreatorType type) => type switch
    {
        CreatorType.Author => Color.Primary,
        CreatorType.Illustrator => Color.Tertiary,
        CreatorType.Musician => Color.Success,
        CreatorType.ScreenWriter => Color.Info,
        CreatorType.VoiceActor => Color.Secondary,
        CreatorType.Director => Color.Primary,
        CreatorType.Actor => Color.Secondary,
        _ => Color.Default
    };
}
