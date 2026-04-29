using NineKgTools.Core.Models.Media.Source;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.DbContexts;

public partial class MediaDbContext
{
    public DbSet<MediaSource> MediaSources { get; set; } = null!;

    public DbSet<PendingIdentification> PendingIdentifications { get; set; } = null!;

    private void OnModelCreatingMediaSources(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaSource>(entity =>
        {
            entity.ToTable("MediaSources");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.FullPath).IsUnique();
            entity.Property(e => e.IsFolder).IsRequired();
            entity.Property(e => e.PossibleTopCategory).HasConversion<int>();
            entity.Property(e => e.EntryFilePath);
            entity.Property(e => e.Identified);
            entity.Property(e => e.InDatabase);

            entity.HasOne(e => e.MediaBase)
                .WithOne(e => e.Source)
                .HasForeignKey<MediaSource>("MediaBaseId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PendingIdentification>(entity =>
        {
            entity.ToTable("PendingIdentifications");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.MediaTypeName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.MediaBaseJson).IsRequired();
            entity.Property(e => e.IdentifiedAt).IsRequired();

            entity.HasOne(e => e.MediaSource)
                .WithOne()
                .HasForeignKey<PendingIdentification>(e => e.MediaSourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.MediaSourceId).IsUnique();
            entity.HasIndex(e => e.IdentifiedAt);
        });
    }
}
