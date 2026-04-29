using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Game;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<GameMedia> Games { get; set; }


    private static void OnModelCreatingGames(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameMedia>(entity =>
        {
            entity.ToTable("Games");

            // 设置主键也是外键，指向 Medias 表
            entity.HasOne<MediaBase>()
                .WithOne()
                .HasForeignKey<GameMedia>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Platforms)
                .HasConversion(
                    v => string.Join(';', v.Select(p => p.ToString())),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(str => Enum.Parse<Platform>(str))
                        .ToList())
                .Metadata.SetValueComparer(new ValueComparer<List<Platform>>(
                    (l1, l2) => l1.SequenceEqual(l2),
                    l => l.Aggregate(0, (a, p) => HashCode.Combine(a, p.GetHashCode())),
                    l => l.ToList()));
            
            entity.HasMany(e => e.ScreenWriters)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GameScreenWriter",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("ScreenWriterId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<GameMedia>()
                        .WithMany()
                        .HasForeignKey("GameMediaId") // 左表的外键，自定义为 "GameMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Illustrators)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GameIllustrator",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("IllustratorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<GameMedia>()
                        .WithMany()
                        .HasForeignKey("GameMediaId") // 左表的外键，自定义为 "GameMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.VoiceActors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GameVoiceActor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("VoiceActorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<GameMedia>()
                        .WithMany()
                        .HasForeignKey("GameMediaId") // 左表的外键，自定义为 "GameMediaId"
                        .OnDelete(DeleteBehavior.Cascade));


            entity.HasMany(e => e.Musicians)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GameMusician",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("MusicianId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<GameMedia>()
                        .WithMany()
                        .HasForeignKey("GameMediaId") // 左表的外键，自定义为 "GameMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Authors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GameAuthor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("AuthorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<GameMedia>()
                        .WithMany()
                        .HasForeignKey("GameMediaId") // 左表的外键，自定义为 "GameMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
        });
    }
}