using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Websites;
using Serilog;

namespace NineKgTools.Pages.Websites;

public partial class WebsitePage : ComponentBase
{
    [Inject] private Config Config { get; set; }
    [Inject] private WebsiteService WebsiteService { get; set; }

    private WebsiteConfig _websiteConfig = new();

    #region 拖拽标识符常量

    private const string SitesIdentifier = "websites";
    private const string VideoIdentifier = "video";
    private const string AudioIdentifier = "audio";
    private const string PictureIdentifier = "image";
    private const string TextIdentifier = "text";
    private const string GameIdentifier = "game";
    private const string UnknownIdentifier = "unknown";

    #endregion

    #region 分类拖拽区域定义

    private record CategoryZoneInfo(
        string Label, string Icon, Color Color, string Identifier,
        TopCategory Category, string BorderClass);

    private static readonly CategoryZoneInfo[] CategoryZones =
    [
        new("视频", Icons.Material.Filled.SmartDisplay, Color.Primary, VideoIdentifier, TopCategory.Video, "card-bordered-primary"),
        new("音频", Icons.Material.Filled.Mic, Color.Secondary, AudioIdentifier, TopCategory.Audio, "card-bordered-secondary"),
        new("图片", Icons.Material.Filled.Image, Color.Warning, PictureIdentifier, TopCategory.Picture, "card-bordered-warning"),
        new("文字", Icons.Material.Filled.TextFields, Color.Tertiary, TextIdentifier, TopCategory.Text, "card-bordered-tertiary"),
        new("游戏", Icons.Material.Filled.SportsEsports, Color.Info, GameIdentifier, TopCategory.Game, "card-bordered-info"),
        new("未知", Icons.Material.Filled.Help, Color.Surface, UnknownIdentifier, TopCategory.Unknown, "card-bordered-default"),
    ];

    private readonly Dictionary<string, Color> _categoryColors = new()
    {
        { VideoIdentifier, Color.Primary },
        { AudioIdentifier, Color.Secondary },
        { PictureIdentifier, Color.Warning },
        { TextIdentifier, Color.Tertiary },
        { GameIdentifier, Color.Info },
        { UnknownIdentifier, Color.Default },
    };

    #endregion

    #region 拖拽数据模型

    private class SiteDropItem
    {
        public string Name { get; init; }
        public string Identifier { get; set; }
        public int Order { get; set; }
        public Color Color { get; set; }

        public SiteDropItem Clone() => new()
        {
            Name = Name,
            Identifier = Identifier,
            Order = Order,
            Color = Color,
        };
    }

    private readonly List<SiteDropItem> _sitesItems =
    [
        new() { Name = "DLsite", Identifier = SitesIdentifier, Order = 0, Color = Color.Primary },
        new() { Name = "Bangumi", Identifier = SitesIdentifier, Order = 1, Color = Color.Secondary },
        new() { Name = "Steam", Identifier = SitesIdentifier, Order = 2, Color = Color.Info },
    ];

    #endregion

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _websiteConfig = Config.Website.Copy();
        InitSitesItems();
    }

    private void InitSitesItems()
    {
        var priorityMap = new (string Identifier, Color Color, List<string> Sites)[]
        {
            (VideoIdentifier, Color.Primary, _websiteConfig.Priority.Video),
            (AudioIdentifier, Color.Secondary, _websiteConfig.Priority.Audio),
            (PictureIdentifier, Color.Warning, _websiteConfig.Priority.Picture),
            (TextIdentifier, Color.Tertiary, _websiteConfig.Priority.Text),
            (GameIdentifier, Color.Info, _websiteConfig.Priority.Game),
            (UnknownIdentifier, Color.Default, _websiteConfig.Priority.Unknown),
        };

        var indexOffset = _sitesItems.Count(x => x.Identifier == SitesIdentifier);

        foreach (var (identifier, color, sites) in priorityMap)
        {
            for (var i = 0; i < sites.Count; i++)
            {
                _sitesItems.Add(new SiteDropItem
                {
                    Name = sites[i],
                    Identifier = identifier,
                    Order = i + indexOffset,
                    Color = color,
                });
            }

            indexOffset += sites.Count;
        }
    }

    private void SaveSitePriority()
    {
        List<string> GetOrderedNames(string identifier) =>
            _sitesItems
                .Where(x => x.Identifier == identifier)
                .OrderBy(x => x.Order)
                .Select(x => x.Name)
                .ToList();

        _websiteConfig.Priority.Video = GetOrderedNames(VideoIdentifier);
        _websiteConfig.Priority.Audio = GetOrderedNames(AudioIdentifier);
        _websiteConfig.Priority.Picture = GetOrderedNames(PictureIdentifier);
        _websiteConfig.Priority.Text = GetOrderedNames(TextIdentifier);
        _websiteConfig.Priority.Game = GetOrderedNames(GameIdentifier);
        _websiteConfig.Priority.Unknown = GetOrderedNames(UnknownIdentifier);
    }

    private async Task _saveConfig()
    {
        _saveProcessing = true;

        try
        {
            Log.Debug("保存网站配置项");

            SaveSitePriority();
            Config.Website = _websiteConfig.Copy();
            await Config.SaveConfig();

            Snackbar.Add("保存网站配置成功", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存网站配置失败");
            Snackbar.Add("保存失败，请检查配置后重试", Severity.Error);
        }
        finally
        {
            _saveProcessing = false;
        }
    }

    private void ItemUpdated(MudItemDropInfo<SiteDropItem> dropItem)
    {
        var newZone = dropItem.DropzoneIdentifier;
        var oldZone = dropItem.Item.Identifier;

        if (oldZone == SitesIdentifier && newZone != SitesIdentifier)
        {
            var clone = dropItem.Item.Clone();
            clone.Identifier = oldZone;
            _sitesItems.Add(clone);
        }

        dropItem.Item.Identifier = newZone;

        var indexOffset = 0;

        if (newZone != SitesIdentifier)
        {
            var identifiersOrder = new[]
            {
                SitesIdentifier,
                VideoIdentifier,
                AudioIdentifier,
                PictureIdentifier,
                TextIdentifier,
                GameIdentifier,
                UnknownIdentifier,
            };

            var counts = identifiersOrder
                .ToDictionary(id => id, id => _sitesItems.Count(x => x.Identifier == id));

            var index = Array.IndexOf(identifiersOrder, newZone);

            indexOffset = identifiersOrder
                .Take(index)
                .Sum(id => counts[id]);

            if (_categoryColors.TryGetValue(newZone, out var color))
            {
                dropItem.Item.Color = color;
            }
        }

        _sitesItems.UpdateOrder(dropItem, item => item.Order, indexOffset);
        RefreshDragContainer();
    }

    private void OnSiteChipClose(SiteDropItem item)
    {
        _sitesItems.Remove(item);
        RefreshDragContainer();
    }

    private bool CanSiteDrop(SiteDropItem siteItem, TopCategory category, string identifier)
    {
        if (siteItem.Identifier != SitesIdentifier && siteItem.Identifier != identifier)
            return false;

        var alreadyExists = _sitesItems.Any(x => x.Identifier == identifier && x.Name == siteItem.Name);
        if (alreadyExists && siteItem.Identifier == SitesIdentifier)
            return false;

        if (category == TopCategory.Unknown)
            return true;

        if (!WebsiteService.WebsiteNameMap.TryGetValue(siteItem.Name, out var site))
            return false;

        return site.TopCategories.Contains(category);
    }
}
