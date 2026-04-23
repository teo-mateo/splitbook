using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;

namespace SplitBook.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Group>()
            .Property(g => g.Currency)
            .HasMaxLength(3);

        modelBuilder.Entity<Membership>()
            .HasKey(m => new { m.GroupId, m.UserId });
    }
}
