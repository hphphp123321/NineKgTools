using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<TopTag> TopTags { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<TagMapping> TagMappings { get; set; } = null!;
    public DbSet<Category> Categories { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Image> Images { get; set; } = null!;
    public DbSet<Circle> Circles { get; set; }
    public DbSet<Creator> Creators { get; set; }

    private void OnModelCreatingTags(ModelBuilder modelBuilder)
    {
        // 配置TopTag和Tag的一对多关系
        modelBuilder.Entity<TopTag>(entity =>
        {
            entity.HasMany(t => t.Tags) // TopTag有多个Tag
                .WithOne(t => t.TopTag) // 每个Tag有一个TopTag
                .HasForeignKey("TopTagId") // 使用影子属性作为外键
                .OnDelete(DeleteBehavior.Cascade); // 删除级联行为
            
            entity.Property(t => t.Name)
                .IsRequired(); // 名称为必填项
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            // 配置Tag的主键
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired(); // 名称为必填项
        });
        
        // 配置TagMapping实体
        modelBuilder.Entity<TagMapping>(entity =>
        {
            // 配置主键
            entity.HasKey(tm => tm.Id);
            
            // 配置源名称索引（唯一且不区分大小写）
            entity.HasIndex(tm => tm.SourceName)
                .IsUnique();
            
            // 配置源名称属性
            entity.Property(tm => tm.SourceName)
                .IsRequired()
                .HasMaxLength(200);
            
            // 配置与Tag的关系
            entity.HasOne(tm => tm.TargetTag)
                .WithMany()
                .HasForeignKey(tm => tm.TargetTagId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // 配置默认值
            entity.Property(tm => tm.IsActive)
                .HasDefaultValue(true);
            
            entity.Property(tm => tm.Priority)
                .HasDefaultValue(100);
            
            entity.Property(tm => tm.CreatedAt)
                .HasDefaultValueSql("datetime('now')");
            
            entity.Property(tm => tm.HitCount)
                .HasDefaultValue(0);
        });
    }

    private void OnModelCreatingCategories(ModelBuilder modelBuilder)
    {
        // 配置 Category 实体
        modelBuilder.Entity<Category>(entity =>
        {
            // 设置 Id 为主键
            entity.HasKey(e => e.Id);

            // 设置 PossibleTopCategory 枚举存储为整数
            entity.Property(e => e.TopCategory).HasConversion<int>();


            // 确保名称为非空
            entity.Property(e => e.Name).IsRequired();
        });
    }

    private void OnModelCreatingFavorites(ModelBuilder modelBuilder)
    {
        // 配置 Favorite 实体
        modelBuilder.Entity<Favorite>(entity =>
        {
            // 设置 Id 为主键
            entity.HasKey(e => e.Id);

            // 确保名称为非空
            entity.Property(e => e.Name).IsRequired();
        });
    }

    private void OnModelCreatingImages(ModelBuilder modelBuilder)
    {
        // 配置 Images 实体
        modelBuilder.Entity<Image>(entity =>
        {
            // 设置 Id 为主键
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.Name).IsUnique();
            
            // 为Hash字段添加索引以加快查询速度
            entity.HasIndex(e => e.Hash);

            entity.Property(e => e.Url)
                .HasConversion(
                    v => v == null ? null : v.ToString(), // 当 Uri 为 null 时返回 null，否则返回 Uri 的字符串表示
                    v => v == null ? null : new Uri(v), // 当字符串为 null 时返回 null，否则尝试构造 Uri
                    new ValueComparer<Uri>(
                        (u1, u2) => u1 == u2, // 比较两个 Uri 是否相等
                        u => u.GetHashCode(), // 获取 Uri 的哈希码
                        u => new Uri(u.ToString()) // 构造一个新的 Uri
                    )
                );

            entity.Property(e => e.File)
                .HasConversion(
                    v => v == null ? null : v.FullName, // 当 FileInfo 为 null 时返回 null，否则返回 FileInfo 的全路径
                    v => v == null ? null : new FileInfo(v), // 当字符串为 null 时返回 null，否则尝试构造 FileInfo
                    new ValueComparer<FileInfo>(
                        (f1, f2) => f1.FullName == f2.FullName, // 比较两个 FileInfo 的全路径是否相等
                        f => f.FullName.GetHashCode(), // 获取 FileInfo 的哈希码
                        f => new FileInfo(f.FullName) // 构造一个新的 FileInfo
                    )
                );
                
            // 配置 Content 属性为二进制数据
            entity.Property(e => e.Content).HasColumnType("BLOB");
            
            // 配置 Hash 属性
            entity.Property(e => e.Hash).HasMaxLength(64);
            
            // 配置图片尺寸属性
            entity.Property(e => e.Width).HasDefaultValue(0);
            entity.Property(e => e.Height).HasDefaultValue(0);
        });
    }

    private static void OnModelCreatingCircles(ModelBuilder modelBuilder)
    {
        // 配置 Circle 实体
        modelBuilder.Entity<Circle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.AliasNames)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode())),
                    l => l.ToList()));
            entity.HasOne(e => e.Avatar);
        });
    }

    private static void OnModelCreatingCreators(ModelBuilder modelBuilder)
    {
        // 配置 Creator 实体
        modelBuilder.Entity<Creator>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.AliasNames)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode())),
                    l => l.ToList()));
            entity.HasOne(e => e.Avatar);
            
            entity.Property(e => e.Types)
                .HasConversion(
                    v => string.Join(';', v.Select(p => p.ToString())),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<CreatorType>)
                        .ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<CreatorType>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, p) => HashCode.Combine(a, p.GetHashCode())),
                    l => l.ToList()));
        });
    }
}