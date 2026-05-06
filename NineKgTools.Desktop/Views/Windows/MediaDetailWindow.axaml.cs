using Avalonia.Controls;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.Views.Windows;

public partial class MediaDetailWindow : Window
{
    public MediaDetailWindow()
    {
        InitializeComponent();
        // 同类型窗口共享一份位置记忆 — 不同 mediaId 用同一 key="media"，避免 N 个 media 各占一份冗余
        this.EnableChildWindowFeatures("media");
    }
}
