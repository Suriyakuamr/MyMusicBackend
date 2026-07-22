using Microsoft.EntityFrameworkCore;
using MusicPlatform.API.Models;

namespace MusicPlatform.API.Data
{
    public class MusicDbContext : DbContext
    {
        public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options)
        {
        }

        public DbSet<Song> Songs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<Song>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Artist).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Album).HasMaxLength(200);
                entity.Property(e => e.FileKey).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ArtworkUri).IsRequired(false);
            });
        }
    }
}
