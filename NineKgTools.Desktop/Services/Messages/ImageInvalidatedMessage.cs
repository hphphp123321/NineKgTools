namespace NineKgTools.Desktop.Services.Messages;

/// <summary>
/// 媒体封面 / 创作者头像编辑保存后广播。`ImageCacheService` 订阅后驱逐对应条目，
/// 已绑定该图片的 UI（卡片 / 详情页）重新加载即可拿到新版本。
///
/// 用 <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> 发：
///   WeakReferenceMessenger.Default.Send(new ImageInvalidatedMessage("cover-foo"));
/// </summary>
/// <param name="ImageName">被替换的图片名（与 ImageService.GetImageByNameAsync 的 key 一致）</param>
public record ImageInvalidatedMessage(string ImageName);
