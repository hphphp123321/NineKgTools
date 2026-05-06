using Avalonia.Controls;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.Views.Windows;

public partial class MediaDetailWindow : Window
{
    public MediaDetailWindow()
    {
        InitializeComponent();
        // 同类型窗口共享一份位置记忆 — 不同 mediaId 用同一 key="media"，避免 N 个 media 各占一份冗余
        this.EnableChildWindowFeatures("media");

        // 删除成功后由 ViewModel 触发本事件 → Window 关闭。订阅放在 DataContextChanged 里
        // 而不是构造函数，因为 DataContext 是构造完才赋值的。
        DataContextChanged += OnDataContextChanged;
    }

    private MediaDetailViewModel? _subscribedVm;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.DeleteCompleted -= OnDeleteCompleted;
            _subscribedVm = null;
        }
        if (DataContext is MediaDetailViewModel vm)
        {
            vm.DeleteCompleted += OnDeleteCompleted;
            _subscribedVm = vm;
        }
    }

    private void OnDeleteCompleted(object? sender, System.EventArgs e)
    {
        // 切回 UI 线程关窗（VM 的 Save/Delete 走 RelayCommand 已经在 UI 线程，但 InvokeAsync 兜底）
        Avalonia.Threading.Dispatcher.UIThread.Post(Close);
    }
}
