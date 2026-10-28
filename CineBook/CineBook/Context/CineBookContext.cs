using System;
using System.Collections.Generic;
using System.IO;
using CineBook.Models;
using Microsoft.EntityFrameworkCore;
using Type = CineBook.Models.Type;

namespace CineBook.Context;

public partial class CineBookContext : DbContext
{
    public CineBookContext() { }

    public CineBookContext(DbContextOptions<CineBookContext> options) : base(options) { }

    public virtual DbSet<AppSetting> AppSettings { get; set; }
    public virtual DbSet<Genre> Genres { get; set; }
    public virtual DbSet<MediaItem> MediaItems { get; set; }
    public virtual DbSet<Status> Statuses { get; set; }
    public virtual DbSet<Type> Types { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={GetDatabasePath()}");
    }

    public static string GetDatabasePath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CineBook");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "cinebook.db");
    }

    public static void InitializeDatabaseFromBundledTemplate()
    {
        var databasePath = GetDatabasePath();
        if (File.Exists(databasePath))
            return;

        using var template = typeof(CineBookContext).Assembly
            .GetManifestResourceStream("CineBook.cinebook.db")
            ?? throw new InvalidOperationException("Bundled SQLite database template is missing.");
        using (var destination = File.Create(databasePath))
            template.CopyTo(destination);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.ToTable("app_settings");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<MediaItem>(entity =>
        {
            entity.ToTable("media_items");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Creator).HasColumnName("creator");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.IsFavorite).HasColumnName("is_favorite");
            entity.Property(e => e.PosterPath).HasColumnName("poster_path");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.TypeId).HasColumnName("type_id");
            entity.HasOne(e => e.Status).WithMany(e => e.MediaItems)
                .HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Type).WithMany(e => e.MediaItems)
                .HasForeignKey(e => e.TypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Genres).WithMany(e => e.MediaItems)
                .UsingEntity<Dictionary<string, object>>(
                    "MediaItemGenre",
                    r => r.HasOne<Genre>().WithMany().HasForeignKey("GenreId"),
                    l => l.HasOne<MediaItem>().WithMany().HasForeignKey("MediaItemId"),
                    j =>
                    {
                        j.HasKey("MediaItemId", "GenreId");
                        j.ToTable("media_item_genres");
                        j.IndexerProperty<int>("MediaItemId").HasColumnName("media_item_id");
                        j.IndexerProperty<int>("GenreId").HasColumnName("genre_id");
                    });
        });

        modelBuilder.Entity<Status>(entity =>
        {
            entity.ToTable("statuses");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Type>(entity =>
        {
            entity.ToTable("types");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });
    }
}
