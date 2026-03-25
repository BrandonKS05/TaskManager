using Microsoft.EntityFrameworkCore;
using TaskManager.Models;

namespace TaskManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TodoList> Lists => Set<TodoList>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoList>(e =>
        {
            e.ToTable("lists");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DeviceId)
                .HasColumnName("device_id")
                .HasMaxLength(64)
                .IsRequired();
            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(500)
                .IsRequired();
            e.HasIndex(x => x.DeviceId);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.DeviceId)
                .HasColumnName("device_id")
                .HasMaxLength(64)
                .IsRequired();
            e.Property(x => x.ListId).HasColumnName("list_id");
            e.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(500)
                .IsRequired();
            e.Property(x => x.IsComplete).HasColumnName("is_complete");
            e.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            e.Property(x => x.Tag).HasColumnName("tag").HasMaxLength(64);
            e.Property(x => x.Priority).HasColumnName("priority");
            e.Property(x => x.DueDate).HasColumnName("due_date").HasMaxLength(10);

            e.HasOne<TodoList>()
                .WithMany()
                .HasForeignKey(x => x.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.DeviceId);
            e.HasIndex(x => x.ListId);
        });
    }
}
