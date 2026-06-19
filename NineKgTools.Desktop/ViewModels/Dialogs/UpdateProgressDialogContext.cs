using CommunityToolkit.Mvvm.ComponentModel;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// 更新下载进度对话框 VM。下载期由 Velopack 的 progress 回调（IO 线程）经 Dispatcher 投递到
/// <see cref="Progress"/>；下载完成后调用方会 ApplyUpdatesAndRestart 退出进程，对话框无需关闭逻辑。
/// </summary>
public partial class UpdateProgressDialogContext : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    private int _progress;

    /// <summary>下载阶段提示文案（如"正在下载更新…" / "正在准备重启…"）。</summary>
    [ObservableProperty]
    private string _message = "正在下载更新…";

    public string ProgressPercentText => $"{Progress}%";

    public UpdateProgressDialogContext(string targetVersion)
    {
        TargetVersion = targetVersion;
    }

    /// <summary>目标版本号（标题展示用）。</summary>
    public string TargetVersion { get; }
}
