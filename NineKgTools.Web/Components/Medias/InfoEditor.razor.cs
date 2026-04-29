using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Medias;

public partial class InfoEditor : ComponentBase
{
    [Parameter] public Dictionary<string, string> InfoDictionary { get; set; } = new();
    [Parameter] public EventCallback<Dictionary<string, string>> InfoDictionaryChanged { get; set; }
    [Parameter] public bool IsEditable { get; set; } = true;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isAddingNew;
    private string _newKey = string.Empty;
    private string _newValue = string.Empty;

    private string? _editingKey;
    private string _tempKey = string.Empty;
    private string _tempValue = string.Empty;

    private void AddNewInfo()
    {
        _isAddingNew = true;
        _newKey = string.Empty;
        _newValue = string.Empty;
    }


    private async Task ConfirmAddNew()
    {
        if (string.IsNullOrWhiteSpace(_newKey) || string.IsNullOrWhiteSpace(_newValue))
        {
            Snackbar.Add("键和值都不能为空", Severity.Warning);
            return;
        }

        if (InfoDictionary.ContainsKey(_newKey))
        {
            Snackbar.Add("该键已存在", Severity.Warning);
            return;
        }

        InfoDictionary[_newKey] = _newValue;
        await InfoDictionaryChanged.InvokeAsync(InfoDictionary);
        
        _isAddingNew = false;
        _newKey = string.Empty;
        _newValue = string.Empty;
        
        Snackbar.Add($"已添加信息：{_newKey}", Severity.Success);
    }

    private void CancelAddNew()
    {
        _isAddingNew = false;
        _newKey = string.Empty;
        _newValue = string.Empty;
    }

    private void StartEdit(string key, string value)
    {
        _editingKey = key;
        _tempKey = key;
        _tempValue = value;
    }

    private async Task SaveEdit(string originalKey)
    {
        if (string.IsNullOrWhiteSpace(_tempKey) || string.IsNullOrWhiteSpace(_tempValue))
        {
            Snackbar.Add("键和值都不能为空", Severity.Warning);
            return;
        }

        // 如果键发生了变化，需要先删除原键，再添加新键
        if (_tempKey != originalKey)
        {
            if (InfoDictionary.ContainsKey(_tempKey))
            {
                Snackbar.Add("该键已存在", Severity.Warning);
                return;
            }
            InfoDictionary.Remove(originalKey);
        }

        InfoDictionary[_tempKey] = _tempValue;
        await InfoDictionaryChanged.InvokeAsync(InfoDictionary);
        
        CancelEdit();
        Snackbar.Add($"已更新信息：{_tempKey}", Severity.Success);
    }

    private void CancelEdit()
    {
        _editingKey = null;
        _tempKey = string.Empty;
        _tempValue = string.Empty;
    }

    private async Task RemoveInfo(string key)
    {
        InfoDictionary.Remove(key);
        await InfoDictionaryChanged.InvokeAsync(InfoDictionary);
        Snackbar.Add($"已删除信息：{key}", Severity.Info);
    }

    private string FormatDuration(int seconds)
    {
        if (seconds <= 0) return "未知";
        
        var timeSpan = TimeSpan.FromSeconds(seconds);
        
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}小时{timeSpan.Minutes}分钟";
        }
        
        return $"{timeSpan.Minutes}分钟{timeSpan.Seconds}秒";
    }
}