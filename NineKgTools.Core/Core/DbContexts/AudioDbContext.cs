using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<AudioMedia> Audios { get; set; }

    private static void OnModelCreatingAudios(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioMedia>(entity =>
        {
            entity.ToTable("Audios");

            // 设置主键也是外键，指向 Medias 表
            entity.HasOne<MediaBase>()
                .WithOne()
                .HasForeignKey<AudioMedia>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(e => e.ScreenWriters)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AudioScreenWriter",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("ScreenWriterId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<AudioMedia>()
                        .WithMany()
                        .HasForeignKey("AudioMediaId") // 左表的外键，自定义为 "AudioMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Illustrators)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AudioIllustrator",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("IllustratorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<AudioMedia>()
                        .WithMany()
                        .HasForeignKey("AudioMediaId") // 左表的外键，自定义为 "AudioMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.VoiceActors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AudioVoiceActor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("VoiceActorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<AudioMedia>()
                        .WithMany()
                        .HasForeignKey("AudioMediaId") // 左表的外键，自定义为 "AudioMediaId"
                        .OnDelete(DeleteBehavior.Cascade));


            entity.HasMany(e => e.Musicians)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AudioMusician",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("MusicianId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<AudioMedia>()
                        .WithMany()
                        .HasForeignKey("AudioMediaId") // 左表的外键，自定义为 "AudioMediaId"
                        .OnDelete(DeleteBehavior.Cascade));

            entity.HasMany(e => e.Authors)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AudioAuthor",
                    right => right.HasOne<Creator>()
                        .WithMany()
                        .HasForeignKey("AuthorId") // 右表的外键
                        .OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<AudioMedia>()
                        .WithMany()
                        .HasForeignKey("AudioMediaId") // 左表的外键，自定义为 "AudioMediaId"
                        .OnDelete(DeleteBehavior.Cascade));
        });
    }
}