using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class ManualAddMediaDialog : UserControl
{
    public ManualAddMediaDialog() => InitializeComponent();

    /// <summary>
    /// ShowAsync 成功创建后的返回值。
    /// </summary>
    /// <param name="MediaId">新建媒体的数据库 Id。</param>
    /// <param name="FullyFilled">用户在对话框内填了至少一个选填字段——调用方可据此决定
    /// 是否在打开详情时附加"立即编辑"信号（FullyFilled=true 时通常直接进只读详情）。</param>
    public sealed record Result(int MediaId, bool FullyFilled);

    /// <summary>
    /// 弹出"手动添加媒体"对话框。用户确认 + 入库成功 → 返回 Result；取消或失败 → null。
    ///
    /// 入库失败时不关闭对话框，把错误显示在 InfoBar 上让用户改后重试。
    /// </summary>
    public static async Task<Result?> ShowAsync(MediaSource source, FilesService filesService)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filesService);

        var ctx = new ManualAddMediaDialogContext(source);
        var view = new ManualAddMediaDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(source),
            Content = view,
            PrimaryButtonText = ctx.ConfirmButtonText,
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = ctx.CanSubmit,
        };

        // 把 context 的派生属性同步到 dialog UI（按钮可用 / 主按钮文字）
        ctx.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ctx.CanSubmit):
                    dialog.IsPrimaryButtonEnabled = ctx.CanSubmit;
                    break;
                case nameof(ctx.ConfirmButtonText):
                    dialog.PrimaryButtonText = ctx.ConfirmButtonText;
                    break;
            }
        };

        Result? result = null;

        // 拦截主按钮点击：异步入库——成功才让 dialog 关闭，失败时取消关闭并把错误显示在 InfoBar 上
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (!ctx.CanSubmit)
            {
                args.Cancel = true;
                return;
            }

            var deferral = args.GetDeferral();
            try
            {
                var media = BuildMedia(ctx);
                await filesService.AddMediaToDatabase(media);

                Log.Information(
                    "桌面端手动创建媒体: Id={Id}, Title={Title}, TopCategory={Top}, " +
                    "FullyFilled={Full}",
                    media.Id, media.Title, ctx.SelectedTopCategory!.Value, ctx.HasOptionalContent);

                result = new Result(media.Id, ctx.HasOptionalContent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "桌面端手动创建媒体失败: Path={Path}", source.FullPath);
                ctx.ErrorMessage = "创建失败，请稍后重试。";
                args.Cancel = true; // 保持 dialog 打开让用户看到错误
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return result;
    }

    /// <summary>
    /// 把 context 里的字段组装成具体子类型的 MediaBase（VideoMedia/AudioMedia/...）。
    /// 子分类未选时 fallback 到"其他X"，简介未填时 fallback 到"暂无简介"。
    /// </summary>
    private static MediaBase BuildMedia(ManualAddMediaDialogContext ctx)
    {
        var top = ctx.SelectedTopCategory!.Value;
        var category = ctx.SelectedSubCategory ?? GetDefaultCategoryFor(top);
        var summary = string.IsNullOrWhiteSpace(ctx.Summary) ? "暂无简介" : ctx.Summary.Trim();

        var baseline = new MediaBase
        {
            Title = ctx.TitleValue.Trim(),
            Category = category,
            Source = ctx.Source,
            Summary = summary,
            Rating = ctx.Rating,
        };

        return top switch
        {
            TopCategory.Video => new VideoMedia(baseline),
            TopCategory.Audio => new AudioMedia(baseline),
            TopCategory.Picture => new PictureMedia(baseline),
            TopCategory.Text => new TextMedia(baseline),
            TopCategory.Game => new GameMedia(baseline),
            _ => throw new InvalidOperationException($"不支持的顶层分类: {top}"),
        };
    }

    private static Category GetDefaultCategoryFor(TopCategory top) => top switch
    {
        TopCategory.Video => StaticCategories.OtherVideo,
        TopCategory.Audio => StaticCategories.OtherAudio,
        TopCategory.Picture => StaticCategories.OtherPicture,
        TopCategory.Text => StaticCategories.OtherText,
        TopCategory.Game => StaticCategories.OtherGame,
        _ => StaticCategories.Unknown,
    };

    /// <summary>对齐 NineKgConfirmDialog / TagEditorDialog 的 Title slot 自渲染样式。</summary>
    private static Control BuildTitleVisual(MediaSource source)
    {
        IBrush iconBrush = Brushes.Gray;
        if (Application.Current?.Resources.TryGetResource(
                "SystemFillColorAttentionBrush",
                Application.Current.ActualThemeVariant,
                out var brushObj) == true
            && brushObj is IBrush b)
        {
            iconBrush = b;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "✚",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = source.IsFolder ? "手动添加媒体（文件夹）" : "手动添加媒体（文件）",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
