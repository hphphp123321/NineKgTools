// SeedDemoDb — 一次性 build & run 的脚本，生成 docs/assets/seed/demo.db
// 让 CI 截图 (.github/workflows/screenshots.yml) 拿到一份非空、脱敏、确定性的数据库。
//
// 用法：
//   dotnet run --project scripts/seed/SeedDemoDb
//   dotnet run --project scripts/seed/SeedDemoDb -- --output some/other/path.db
//
// 设计原则：
//   - 复用 NineKgTools.Core 的 EnsureCreated + InitializeXxxDb 服务，schema 始终跟主项目同步
//   - 业务数据（25 条媒体、5 个社团、10 个创作者、3 个收藏夹、8 条媒体源、4 条 PendingIdentification、5 条标签映射）
//     全部用 `new Random(42)` 确定性生成，重复跑产出逐字节相同的 demo.db
//   - 海报图用 ImageSharp 现场画纯色 JPEG（按媒体类别分配主题色），不依赖外网

using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Categories;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Tags;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

// 给 ImageSharp 的 Image 起别名，避免与 NineKgTools.Core.Models.Media.Image 冲突
using ImageSharpImage = SixLabors.ImageSharp.Image;
using DomainImage = NineKgTools.Core.Models.Media.Image;

const int posterWidth = 600;
const int posterHeight = 800;

// ───────── 1. 解析 args ─────────
var outputArg = "docs/assets/seed/demo.db";
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--output" && i + 1 < args.Length)
    {
        outputArg = args[++i];
    }
}

// ───────── 2. 切到仓库根 ─────────
// 让 Config.FindConfigFile("tags.yaml") 找到 Config/tags.yaml
var assemblyDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
var repoRoot = FindRepoRoot(assemblyDir)
    ?? throw new InvalidOperationException("找不到仓库根（沿父目录找 NineKgTools.sln 失败）");
Directory.SetCurrentDirectory(repoRoot);

var outputAbs = Path.IsPathRooted(outputArg) ? outputArg : Path.Combine(repoRoot, outputArg);
Directory.CreateDirectory(Path.GetDirectoryName(outputAbs)!);

Console.WriteLine($">> repo root: {repoRoot}");
Console.WriteLine($">> output:    {outputAbs}");

// ───────── 3. 删旧 sqlite 文件（含 wal/shm）─────────
foreach (var ext in new[] { string.Empty, "-wal", "-shm" })
{
    var p = outputAbs + ext;
    if (File.Exists(p))
    {
        File.Delete(p);
    }
}

// ───────── 4. 建库 + 跑 InitializeXxxDb ─────────
var options = new DbContextOptionsBuilder<MediaDbContext>()
    .UseSqlite($"Data Source={outputAbs}")
    .Options;

await using var db = new MediaDbContext(options);
await db.Database.EnsureCreatedAsync();

await new TagService(db).InitializeTagsDbFromYaml();
await new CategoryService(db).InitializeCategoriesDb();
await new FavoriteService(db).InitializeFavoritesDb();

Console.WriteLine($">> 标签 / 分类 / 收藏夹 初始化完毕（{await db.Tags.CountAsync()} 个标签）");

// ───────── 5. 业务数据 ─────────
var rng = new Random(42);
var trackedTags = await db.Tags.Take(60).ToListAsync();
var categories = await db.Categories.ToListAsync();
Category Cat(int id) => categories.First(c => c.Id == id);

// Circles
var circles = new[]
{
    "彩月工房", "星海制作所", "夜光社团", "晨曦工坊", "流萤工作室",
}
.Select(name => new Circle { Name = name, Description = $"虚构演示社团 — {name}（仅占位）" })
.ToList();
await db.Circles.AddRangeAsync(circles);

// Creators
var creators = new[]
{
    new Creator { Name = "演示作者 朝霞", Types = [CreatorType.Author] },
    new Creator { Name = "演示作者 蓝鹿", Types = [CreatorType.Author, CreatorType.ScreenWriter] },
    new Creator { Name = "演示作者 雾影", Types = [CreatorType.Author] },
    new Creator { Name = "演示画师 林夕", Types = [CreatorType.Illustrator] },
    new Creator { Name = "演示画师 七星", Types = [CreatorType.Illustrator] },
    new Creator { Name = "演示作曲 莫白", Types = [CreatorType.Musician] },
    new Creator { Name = "演示声优 樱井", Types = [CreatorType.VoiceActor] },
    new Creator { Name = "演示声优 月见", Types = [CreatorType.VoiceActor, CreatorType.Actor] },
    new Creator { Name = "演示编剧 银河", Types = [CreatorType.ScreenWriter] },
    new Creator { Name = "演示监督 小川", Types = [CreatorType.Director] },
};
await db.Creators.AddRangeAsync(creators);

// Favorites（默认收藏夹由 InitializeFavoritesDb 创建）
var favPlaying = new Favorite { Name = "正在游玩" };
var favWaiting = new Favorite { Name = "等更新" };
var favPicks = new Favorite { Name = "个人精选" };
await db.Favorites.AddRangeAsync(favPlaying, favWaiting, favPicks);
await db.SaveChangesAsync();

var defaultFav = await db.Favorites.FirstAsync(f => f.Id == StaticFavorites.DefaultFavorite.Id);
Console.WriteLine($">> 社团 / 创作者 / 收藏夹 写入完毕（{circles.Count} circles, {creators.Length} creators, 4 favorites）");

// ───────── 6. Posters（按类别分配主题色，ImageSharp 现场画）─────────
var paletteAudio = new Rgba32(76, 161, 255);    // 蓝
var paletteVideo = new Rgba32(229, 91, 91);     // 红
var paletteGame = new Rgba32(81, 207, 102);     // 绿
var palettePicture = new Rgba32(252, 196, 25);  // 黄
var paletteText = new Rgba32(174, 113, 230);    // 紫

byte[] PosterBytes(Rgba32 color)
{
    // 用构造器自带的填充色——避免依赖 ImageSharp.Drawing 包
    using var img = new SixLabors.ImageSharp.Image<Rgba32>(posterWidth, posterHeight, color);
    using var ms = new MemoryStream();
    img.Save(ms, new JpegEncoder { Quality = 78 });
    return ms.ToArray();
}

DomainImage MakePoster(int idx, Rgba32 color)
{
    var bytes = PosterBytes(color);
    return new DomainImage(bytes, $"demo-poster-{idx:D3}.jpg")
    {
        Width = posterWidth,
        Height = posterHeight,
    };
}

List<Tag> PickTags(int count)
{
    var picked = new HashSet<Tag>();
    var max = Math.Min(count * 3, trackedTags.Count);
    for (var i = 0; i < max && picked.Count < count; i++)
    {
        picked.Add(trackedTags[rng.Next(trackedTags.Count)]);
    }
    return picked.ToList();
}

DateTime RandomDate(int year) =>
    new(year, rng.Next(1, 13), rng.Next(1, 28));

void Decorate(MediaBase m, int idx, Rgba32 color)
{
    m.Summary = $"虚构演示作品 {idx:D3} 的简介。所有内容仅占位，无实际版权或意义。";
    m.Description = $"<p><strong>演示作品 {idx:D3}</strong></p><p>这是一段用于截图占位的描述文字，不指向任何真实作品。</p>";
    m.Rating = (float)Math.Round(rng.NextDouble() * 4 + 1, 1);
    m.ReleaseDate = RandomDate(2024);
    m.StoreDate = RandomDate(2025);
    m.Size = (long)(rng.NextDouble() * 2_400_000_000L + 100_000_000L);
    m.Poster = MakePoster(idx, color);
    m.Tags = PickTags(rng.Next(3, 7));

    if (rng.NextDouble() < 0.30) m.Favorites.Add(defaultFav);
    if (rng.NextDouble() < 0.20) m.Favorites.Add(favPlaying);
    if (rng.NextDouble() < 0.25) m.Favorites.Add(favPicks);
}

// ───────── Audio (5) ─────────
for (var i = 1; i <= 5; i++)
{
    var idx = i;
    var cat = i <= 3 ? StaticCategories.Asmr : StaticCategories.OtherAudio;
    var media = new AudioMedia
    {
        Title = $"演示音频 {idx:D3}",
        Category = Cat(cat.Id),
        Circle = circles[(idx - 1) % circles.Count],
        VoiceActors = [creators[6], creators[7]],
        Musicians = i % 2 == 0 ? [creators[5]] : [],
    };
    Decorate(media, idx, paletteAudio);
    db.Medias.Add(media);
}

// ───────── Video (5) ─────────
for (var i = 1; i <= 5; i++)
{
    var idx = 5 + i;
    var cat = i <= 2 ? StaticCategories.HAnime
        : i == 3 ? StaticCategories.AdultVideo
        : StaticCategories.OtherVideo;
    var media = new VideoMedia
    {
        Title = $"演示视频 {idx:D3}",
        Category = Cat(cat.Id),
        Circle = circles[i % circles.Count],
        Episodes = rng.Next(1, 13),
        Directors = [creators[9]],
        Actors = [creators[7]],
        ScreenWriters = [creators[8]],
    };
    Decorate(media, idx, paletteVideo);
    db.Medias.Add(media);
}

// ───────── Game (5) ─────────
for (var i = 1; i <= 5; i++)
{
    var idx = 10 + i;
    var cat = i switch
    {
        1 or 2 => StaticCategories.RpgGame,
        3 => StaticCategories.AvgGame,
        4 => StaticCategories.ActGame,
        _ => StaticCategories.OtherGame,
    };
    var media = new GameMedia
    {
        Title = $"演示游戏 {idx:D3}",
        Category = Cat(cat.Id),
        Circle = circles[(i + 1) % circles.Count],
        Platforms = i % 2 == 0
            ? [Platform.Windows]
            : [Platform.Windows, Platform.Android],
        Authors = [creators[0]],
        Illustrators = [creators[3]],
        Musicians = [creators[5]],
        VoiceActors = [creators[6]],
        ScreenWriters = [creators[8]],
    };
    Decorate(media, idx, paletteGame);
    db.Medias.Add(media);
}

// ───────── Picture (5) ─────────
for (var i = 1; i <= 5; i++)
{
    var idx = 15 + i;
    var cat = i <= 3 ? StaticCategories.Manga
        : i == 4 ? StaticCategories.PictureSet
        : StaticCategories.OtherPicture;
    var media = new PictureMedia
    {
        Title = $"演示画册 {idx:D3}",
        Category = Cat(cat.Id),
        Circle = circles[(i + 2) % circles.Count],
        PageNum = rng.Next(20, 250),
        Illustrators = [creators[3], creators[4]],
        Authors = [creators[1]],
    };
    Decorate(media, idx, palettePicture);
    db.Medias.Add(media);
}

// ───────── Text (5) ─────────
for (var i = 1; i <= 5; i++)
{
    var idx = 20 + i;
    var cat = i <= 4 ? StaticCategories.Novel : StaticCategories.OtherText;
    var media = new TextMedia
    {
        Title = $"演示小说 {idx:D3}",
        Category = Cat(cat.Id),
        Circle = circles[(i + 3) % circles.Count],
        WordCount = rng.Next(50_000, 300_000),
        BookNum = rng.Next(1, 6),
        Author = creators[2],
        Illustrators = [creators[4]],
    };
    Decorate(media, idx, paletteText);
    db.Medias.Add(media);
}

await db.SaveChangesAsync();
Console.WriteLine($">> 25 条 demo 媒体写入完毕（{await db.Medias.CountAsync()} medias）");

// ───────── 7. MediaSources（4 待识别 + 4 待入库）─────────
var mediaSources = new List<MediaSource>();
var topCats = new[] { TopCategory.Audio, TopCategory.Video, TopCategory.Game, TopCategory.Picture, TopCategory.Text };

for (var i = 0; i < 4; i++)
{
    mediaSources.Add(new MediaSource
    {
        FullPath = $"/demo/未识别/未识别作品 {i + 1:D2}",
        IsFolder = i % 2 == 0,
        PossibleTopCategory = topCats[i % topCats.Length],
        Identified = false,
        InDatabase = false,
    });
}
for (var i = 0; i < 4; i++)
{
    mediaSources.Add(new MediaSource
    {
        FullPath = $"/demo/已识别待入库/作品 {i + 1:D2}",
        IsFolder = true,
        PossibleTopCategory = topCats[(i + 2) % topCats.Length],
        Identified = true,
        InDatabase = false,
    });
}
await db.MediaSources.AddRangeAsync(mediaSources);
await db.SaveChangesAsync();

// ───────── 8. PendingIdentification（让"待入库"tab 有内容）─────────
foreach (var (src, idx) in mediaSources.Skip(4).Select((s, i) => (s, i)))
{
    db.PendingIdentifications.Add(new PendingIdentification
    {
        MediaSourceId = src.Id,
        MediaTypeName = nameof(GameMedia),
        MediaBaseJson = "{\"$type\":\"game\",\"Title\":\"待入库演示作品\",\"Summary\":\"占位简介\"}",
        IdentifiedAt = DateTime.UtcNow.AddHours(-idx * 6),
    });
}
await db.SaveChangesAsync();

// ───────── 9. TagMappings（让 tag-mappings 页面有内容）─────────
var mappingSamples = trackedTags.Take(5).ToList();
for (var i = 0; i < mappingSamples.Count; i++)
{
    db.TagMappings.Add(new TagMapping
    {
        SourceName = $"识别源原始标签{i + 1}",
        TargetTag = mappingSamples[i],
        TargetTagId = mappingSamples[i].Id,
        IsActive = true,
        Priority = 100 - i * 10,
    });
}
await db.SaveChangesAsync();

// ───────── 10. 输出汇总 ─────────
var fileSize = new FileInfo(outputAbs).Length;
Console.WriteLine();
Console.WriteLine(">> SEED DONE");
Console.WriteLine($"   path:     {outputAbs}");
Console.WriteLine($"   size:     {fileSize / 1024.0:F1} KB");
Console.WriteLine($"   medias:   {await db.Medias.CountAsync()}");
Console.WriteLine($"   circles:  {await db.Circles.CountAsync()}");
Console.WriteLine($"   creators: {await db.Creators.CountAsync()}");
Console.WriteLine($"   favs:     {await db.Favorites.CountAsync()}");
Console.WriteLine($"   sources:  {await db.MediaSources.CountAsync()}");
Console.WriteLine($"   pending:  {await db.PendingIdentifications.CountAsync()}");
Console.WriteLine($"   mappings: {await db.TagMappings.CountAsync()}");

return;

static string? FindRepoRoot(string startDir)
{
    var d = new DirectoryInfo(startDir);
    while (d != null)
    {
        if (File.Exists(Path.Combine(d.FullName, "NineKgTools.sln")))
        {
            return d.FullName;
        }
        d = d.Parent;
    }
    return null;
}
