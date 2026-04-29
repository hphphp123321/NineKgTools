using NineKgTools.Core.Models.Auth;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Models.Favorites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public DbSet<MediaBase> Medias { get; set; } = null!;

    // 用户表
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // 添加 Creator 同步拦截器
        optionsBuilder.AddInterceptors(new CreatorSyncInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingTags(modelBuilder); // 配置标签数据库
        OnModelCreatingCategories(modelBuilder); // 配置分类数据库
        OnModelCreatingFavorites(modelBuilder); // 配置收藏夹数据库
        OnModelCreatingImages(modelBuilder); // 配置图片数据库
        OnModelCreatingCircles(modelBuilder); // 配置社团数据库
        OnModelCreatingCreators(modelBuilder); // 配置创作者数据库

        OnModelCreatingMediaSources(modelBuilder); // 配置媒体源数据库
        OnModelCreatingMediaBase(modelBuilder); // 配置媒体基类数据库
        OnModelCreatingVideos(modelBuilder); // 配置视频类别的媒体数据库
        OnModelCreatingAudios(modelBuilder); // 配置音频类别的媒体数据库
        OnModelCreatingGames(modelBuilder); // 配置游戏类别的媒体数据库
        OnModelCreatingPictures(modelBuilder); // 配置图片类别的媒体数据库
        OnModelCreatingTexts(modelBuilder); // 配置文本类别的媒体数据库

        OnModelCreatingUsers(modelBuilder); // 配置用户数据库
    }

    private static void OnModelCreatingMediaBase(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaBase>(entity =>
        {
            entity.ToTable("Medias");

            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Title).IsUnique();

            entity.HasOne(e => e.Circle)
                .WithMany(c => c.Medias)
                .HasForeignKey("CircleId");

            // 设置别名的转换
            entity.Property(e => e.AliasTitles)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode())),
                    l => l.ToList()));

            entity.Property(e => e.ReleaseDate);
            entity.Property(e => e.StoreDate);
            entity.Property(e => e.Summary).HasMaxLength(4096);
            entity.Property(e => e.SummaryTranslated);
            entity.Property(e => e.Description);
            entity.Property(e => e.DescriptionTranslated);
            entity.Property(e => e.Size);
            entity.Property(e => e.Rating).HasColumnType("float");

            // 设置Uri类型的Links转换
            entity.Property(e => e.Links)
                .HasConversion(
                    v => string.Join(';', v.Select(uri => uri.ToString())),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(str => new Uri(str)).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<Uri>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, uri) => HashCode.Combine(a, uri.GetHashCode())),
                    l => l.ToList()));

            // 设置Dictionary类型的Infos转换
            entity.Property(e => e.Infos)
                .HasConversion(
                    v => string.Join(';', v.Select(kv => $"{kv.Key}={kv.Value}")),
                    v => v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(str => str.Split(new[] { '=' }, 2))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => parts[1]))
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                    (d1, d2) => d1.SequenceEqual(d2),
                    d => d.Aggregate(0,
                        (a, p) => HashCode.Combine(a, HashCode.Combine(p.Key.GetHashCode(), p.Value.GetHashCode()))),
                    d => d.ToDictionary(p => p.Key, p => p.Value)));

            entity.HasOne(e => e.Category).WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Cascade);

            // entity.HasMany(e => e.Tags).WithMany(t => t.Medias).UsingEntity(j => j.ToTable("MediaTag"));
            entity.HasMany(m => m.Tags)
                .WithMany(t => t.Medias)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaTag", // 连接表的名称
                    j => j
                        .HasOne<Tag>()
                        .WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("FK_MediaTag_Tags_TagId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j
                        .HasOne<MediaBase>()
                        .WithMany()
                        .HasForeignKey("MediaBaseId")
                        .HasConstraintName("FK_MediaTag_MediaBases_MediaBaseId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("MediaBaseId", "TagId"); // 复合主键
                        j.ToTable("MediaTag"); // 指定连接表名称
                    });

            // 将Favorite单对多关系改为多对多关系
            entity.HasMany(e => e.Favorites)
                .WithMany(f => f.Medias)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaFavorite", // 连接表的名称
                    j => j
                        .HasOne<Favorite>()
                        .WithMany()
                        .HasForeignKey("FavoriteId")
                        .HasConstraintName("FK_MediaFavorite_Favorites_FavoriteId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j
                        .HasOne<MediaBase>()
                        .WithMany()
                        .HasForeignKey("MediaBaseId")
                        .HasConstraintName("FK_MediaFavorite_MediaBases_MediaBaseId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("MediaBaseId", "FavoriteId"); // 复合主键
                        j.ToTable("MediaFavorite"); // 指定连接表名称
                    });

            // 配置Creator多对多关系
            entity.HasMany(m => m.Creators)
                .WithMany(c => c.Medias)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaCreator", // 连接表的名称
                    j => j
                        .HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("CreatorId")
                        .HasConstraintName("FK_MediaCreator_Creators_CreatorId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j
                        .HasOne<MediaBase>()
                        .WithMany()
                        .HasForeignKey("MediaBaseId")
                        .HasConstraintName("FK_MediaCreator_MediaBases_MediaBaseId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("MediaBaseId", "CreatorId"); // 复合主键
                        j.ToTable("MediaCreator"); // 指定连接表名称
                    });

            entity.HasOne(e => e.Poster);
            entity.HasMany(e => e.Pictures).WithOne(i => i.Media).OnDelete(DeleteBehavior.Cascade);

            // 设置关联的媒体
            entity.HasMany(e => e.RelatedMedias)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "MediaRelationship",
                    right => right.HasOne<MediaBase>()
                        .WithMany()
                        .HasForeignKey("RelatedMediaId")
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<MediaBase>()
                        .WithMany()
                        .HasForeignKey("MediaBaseId")
                        .OnDelete(DeleteBehavior.Cascade)
                );
        });
    }

    /// <summary>
    /// 配置用户表
    /// </summary>
    private static void OnModelCreatingUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();
        });
    }
}