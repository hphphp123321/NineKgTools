using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Text;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<TextMedia> Texts { get; set; }
    
    private static void OnModelCreatingTexts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TextMedia>(entity =>
        {
            entity.ToTable("Texts");

            // 设置主键也是外键，指向 Medias 表
            entity.HasOne<MediaBase>()
                .WithOne()
                .HasForeignKey<TextMedia>(e => e.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.WordCount);
            entity.Property(e => e.BookNum);

            entity.HasOne(e => e.Author).WithMany().HasForeignKey("AuthorId");
            entity.HasMany(e => e.Illustrators).WithMany().UsingEntity(j => j.ToTable("TextIllustrators"));
        });
    }
}