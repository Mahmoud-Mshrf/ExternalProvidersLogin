using Microsoft.EntityFrameworkCore;
using SignInWithGoogle.Models;

namespace SignInWithGoogle.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.Property(u => u.Id).ValueGeneratedOnAdd();
                e.HasIndex(u => u.GoogleId).IsUnique();
                e.Property(u => u.GoogleId).HasMaxLength(128).IsRequired();
                e.Property(u => u.Email).HasMaxLength(256).IsRequired();
                e.Property(u => u.Name).HasMaxLength(256);
                e.Property(u => u.PictureUrl).HasMaxLength(1024);
            });
        }
    }
}
