using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Video;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<VideoMedia> Videos { get; set; }

    private static void OnModelCreatingVideos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoMedia>(entity =>
        {
            entity.ToTable("Videos");

            // 设置主键也是外键，指向 Medias 表
            entity.HasOne<MediaBase>()
                .WithOne()
                .HasForeignKey<VideoMedia>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Episodes);
            
            entity.HasMany(e => e.Makers)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoMaker",
                    right => right.HasOne<Circle>()
                        .WithMany()
                        .HasForeignKey("MakerId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.ScreenWriters)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoScreenWriter",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("ScreenWriterId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Illustrators)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoIllustrator",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("IllustratorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Actors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoActor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("ActorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Musicians)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoMusician",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("MusicianId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
            
            entity.HasMany(e => e.Directors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VideoDirector",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("DirectorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<VideoMedia>()
                        .WithMany()
                        .HasForeignKey("VideoMediaId") // 左表的外键，自定义为 "VideoMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
                        
        });
    }
}