using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Picture;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<PictureMedia> Pictures { get; set; }
    
    private static void OnModelCreatingPictures(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PictureMedia>(entity =>
        {
            entity.ToTable("Pictures");

            // 设置主键也是外键，指向 Medias 表
            entity.HasOne<MediaBase>()
                .WithOne()
                .HasForeignKey<PictureMedia>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(e => e.Illustrators)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "PictureIllustrator",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("IllustratorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<PictureMedia>()
                        .WithMany()
                        .HasForeignKey("PictureMediaId") // 左表的外键，自定义为 "PictureMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Actors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "PictureActor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("ActorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<PictureMedia>()
                        .WithMany()
                        .HasForeignKey("PictureMediaId") // 左表的外键，自定义为 "PictureMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
            
            entity.HasMany(e => e.Authors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "PictureAuthor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("AuthorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<PictureMedia>()
                        .WithMany()
                        .HasForeignKey("PictureMediaId") // 左表的外键，自定义为 "PictureMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
        });
    }
}