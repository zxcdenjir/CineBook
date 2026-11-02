using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CineBook.Models;
using Microsoft.EntityFrameworkCore;
using Type = CineBook.Models.Type;

namespace CineBook.Context;

public partial class CineBookContext : DbContext
{
    public CineBookContext()
    {
    }

    public CineBookContext(DbContextOptions<CineBookContext> options)
        : base(options)
    {
    }

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
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CineBook");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "cinebook.db");
    }

    public static void InitializeDatabaseFromBundledTemplate()
    {
        var databasePath = GetDatabasePath();
        if (File.Exists(databasePath))
            return;

        var initialTemplatePath = databasePath + "." + Guid.NewGuid().ToString("N") + ".template";
        try
        {
            CopyBundledTemplateTo(initialTemplatePath);
            try
            {
                File.Move(initialTemplatePath, databasePath);
            }
            catch (IOException) when (File.Exists(databasePath))
            {
                // ignore
            }
        }
        finally
        {
            if (File.Exists(initialTemplatePath))
                File.Delete(initialTemplatePath);
        }
    }

    private static void CopyBundledTemplateTo(string destinationPath)
    {
        using var template = typeof(CineBookContext).Assembly
            .GetManifestResourceStream("CineBook.cinebook.db")
            ?? throw new InvalidOperationException("Bundled SQLite database template is missing.");
        using var destination = File.Create(destinationPath);
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

            entity.HasIndex(e => e.Name, "IX_genres_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<MediaItem>(entity =>
        {
            entity.ToTable("media_items");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AdditionDateTime).HasColumnName("addition_date_time");
            entity.Property(e => e.Creator).HasColumnName("creator");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.IsFavorite)
                .HasColumnType("BOOLEAN")
                .HasColumnName("is_favorite");
            entity.Property(e => e.PosterPath).HasColumnName("poster_path");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.TypeId).HasColumnName("type_id");

            entity.HasOne(d => d.Status).WithMany(p => p.MediaItems)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Type).WithMany(p => p.MediaItems)
                .HasForeignKey(d => d.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(d => d.Genres).WithMany(p => p.MediaItems)
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

            entity.HasIndex(e => e.Name, "IX_statuses_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Type>(entity =>
        {
            entity.ToTable("types");

            entity.HasIndex(e => e.Name, "IX_types_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
