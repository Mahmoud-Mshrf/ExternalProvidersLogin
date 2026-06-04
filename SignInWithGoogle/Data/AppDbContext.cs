using Microsoft.EntityFrameworkCore;
using SignInWithGoogle.Models;

namespace SignInWithGoogle.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Message> Messages => Set<Message>();

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

            model.Entity<Message>(e =>
            {
                e.HasKey(m => m.Id);
                e.Property(m => m.Id).ValueGeneratedOnAdd();
                e.Property(m => m.Content).HasMaxLength(4000).IsRequired();

                // A user can send many messages
                e.HasOne(m => m.Sender)
                 .WithMany()
                 .HasForeignKey(m => m.SenderId)
                 .OnDelete(DeleteBehavior.Restrict);

                // A user can receive many messages
                e.HasOne(m => m.Receiver)
                 .WithMany()
                 .HasForeignKey(m => m.ReceiverId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(m => new { m.SenderId, m.ReceiverId });
                e.HasIndex(m => m.SentAt);
            });
        }
    }
}
