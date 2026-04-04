using Microsoft.EntityFrameworkCore;
using TGitApi.Data.Entities;

namespace TGitApi.Data;

public class TGitDbContext : DbContext
{
    public TGitDbContext(DbContextOptions<TGitDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RepoActivityEntity> RepoActivities => Set<RepoActivityEntity>();
    public DbSet<FileEditEntity> FileEdits => Set<FileEditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasMaxLength(450);
            e.Property(u => u.UserName).HasMaxLength(256);
            e.Property(u => u.UserEmail).HasMaxLength(256);
            e.Property(u => u.LastActivity).HasMaxLength(64);
            e.Property(u => u.Tenant).HasMaxLength(128);
            e.HasIndex(u => u.Tenant);
            e.HasIndex(u => u.LastActivity).IsDescending();
        });

        modelBuilder.Entity<RepoActivityEntity>(e =>
        {
            e.ToTable("RepoActivities");
            e.HasKey(r => r.Id);
            e.Property(r => r.UserId).HasMaxLength(450);
            e.Property(r => r.ActivityKey).HasMaxLength(512);
            e.Property(r => r.RepoName).HasMaxLength(256);
            e.Property(r => r.Branch).HasMaxLength(256);
            e.Property(r => r.RemoteUrl).HasMaxLength(1024);
            e.Property(r => r.LastUpdated).HasMaxLength(64);
            e.Property(r => r.MachineName).HasMaxLength(256);
            e.HasIndex(r => new { r.UserId, r.ActivityKey }).IsUnique();
            e.HasOne(r => r.User)
             .WithMany(u => u.Activities)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FileEditEntity>(e =>
        {
            e.ToTable("FileEdits");
            e.HasKey(f => f.Id);
            e.Property(f => f.FilePath).HasMaxLength(1024);
            e.Property(f => f.Status).HasMaxLength(64);
            e.HasOne(f => f.RepoActivity)
             .WithMany(r => r.ModifiedFiles)
             .HasForeignKey(f => f.RepoActivityId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
