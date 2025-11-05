using Microsoft.EntityFrameworkCore;

namespace KaraokePlayer.Models;

/// <summary>
/// Database context for the Karaoke Player application
/// </summary>
public class KaraokeDbContext : DbContext
{
    public DbSet<MediaFile> MediaFiles { get; set; } = null!;
    public DbSet<MediaMetadata> MediaMetadata { get; set; } = null!;
    public DbSet<PlaylistItem> PlaylistItems { get; set; } = null!;
    public DbSet<AppSettings> AppSettings { get; set; } = null!;

    public KaraokeDbContext(DbContextOptions<KaraokeDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MediaFile
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.Filename);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Format).HasConversion<string>();
        });

        // Configure MediaMetadata
        modelBuilder.Entity<MediaMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MediaFileId).IsUnique();
            entity.HasIndex(e => e.Artist);
            entity.HasIndex(e => e.Title);
            
            entity.HasOne(e => e.MediaFile)
                .WithOne(m => m.Metadata)
                .HasForeignKey<MediaMetadata>(e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PlaylistItem
        modelBuilder.Entity<PlaylistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Position);
            
            entity.HasOne(e => e.MediaFile)
                .WithMany()
                .HasForeignKey(e => e.MediaFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AppSettings
        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayMode).HasConversion<string>();
        });
    }
}
