using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体详情页"相关媒体" section 里每张卡的 VM。
/// 与 <see cref="MediaDetailViewModel.RelatedMedias"/> 集合一对一绑定。
/// 与 Web 端 <c>SimpleMediaCard</c> 等价的精简形态——只展示 Id / 标题 / 封面 / 类别。
/// </summary>
public partial class RelatedMediaItemViewModel : ObservableObject
{
    public int Id { get; }
    public string Title { get; }
    public string? CategoryName { get; }
    public bool HasCategory => !string.IsNullOrEmpty(CategoryName);

    [ObservableProperty] private Bitmap? _poster;
    [ObservableProperty] private bool _hasPoster;

    private RelatedMediaItemViewModel(int id, string title, string? categoryName)
    {
        Id = id;
        Title = title;
        CategoryName = categoryName;
    }

    public static RelatedMediaItemViewModel From(MediaBase media, ImageCacheService imageCache)
    {
        var vm = new RelatedMediaItemViewModel(media.Id, media.Title, media.Category?.Name);
        var posterName = media.Poster?.Name;
        if (!string.IsNullOrEmpty(posterName))
            _ = LoadPosterAsync(vm, imageCache, posterName);
        return vm;
    }

    private static async System.Threading.Tasks.Task LoadPosterAsync(
        RelatedMediaItemViewModel vm,
        ImageCacheService imageCache,
        string name)
    {
        try
        {
            var bmp = await imageCache.GetOrLoadAsync(name);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.Poster = bmp;
                vm.HasPoster = bmp != null;
            });
        }
        catch (System.Exception ex)
        {
            Log.Debug(ex, "RelatedMediaItemViewModel 封面加载失败 Name={Name}", name);
        }
    }
}
