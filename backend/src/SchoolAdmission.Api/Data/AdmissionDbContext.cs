using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();

    public DbSet<AdmissionHold> Holds => Set<AdmissionHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("classes", table =>
            {
                table.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0");
                table.HasCheckConstraint("ck_classes_name_length", $"char_length(name) BETWEEN 1 AND {SchoolClass.MaxNameLength}");
            });

            entity.HasKey(c => c.Id).HasName("pk_classes");
            entity.Property(c => c.Id).HasColumnName("id").HasMaxLength(40);
            entity.Property(c => c.Name).HasColumnName("name").IsRequired();
            entity.Property(c => c.NormalizedName).HasColumnName("normalized_name").IsRequired();
            entity.Property(c => c.TotalSeats).HasColumnName("total_seats");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(c => c.NormalizedName).IsUnique().HasDatabaseName("ux_classes_normalized_name");
            entity.HasIndex(c => c.CreatedAt).HasDatabaseName("ix_classes_created_at");
        });

        modelBuilder.Entity<AdmissionHold>(entity =>
        {
            entity.ToTable("holds", table =>
            {
                table.HasCheckConstraint("ck_holds_status", "status IN ('HELD', 'CONFIRMED', 'EXPIRED', 'CANCELLED')");
                table.HasCheckConstraint("ck_holds_student_id_format", "student_id ~ '^[0-9]{17}$'");
            });

            entity.HasKey(h => h.Id).HasName("pk_holds");
            entity.Property(h => h.Id).HasColumnName("id").HasMaxLength(40);
            entity.Property(h => h.ClassId).HasColumnName("class_id").HasMaxLength(40);
            entity.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(17).IsFixedLength();
            entity.Property(h => h.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion(status => status.ToApiValue(), value => HoldStatusExtensions.FromApiValue(value));
            entity.Property(h => h.CreatedAt).HasColumnName("created_at");
            entity.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            entity.Property(h => h.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(h => h.CancelledAt).HasColumnName("cancelled_at");

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .HasConstraintName("fk_holds_classes_class_id")
                .OnDelete(DeleteBehavior.Restrict);

            // Seat counting per class.
            entity.HasIndex(h => new { h.ClassId, h.Status }).HasDatabaseName("ix_holds_class_id_status");

            // "My applications".
            entity.HasIndex(h => new { h.StudentId, h.CreatedAt }).HasDatabaseName("ix_holds_student_id_created_at");

            // Rule: one active application or confirmed admission per student per class.
            // Safety net behind the row lock taken when a hold is created.
            entity.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasFilter("status IN ('HELD', 'CONFIRMED')")
                .HasDatabaseName("ux_holds_active_student_per_class");

            // Expiry sweeper.
            entity.HasIndex(h => h.ExpiresAt)
                .HasFilter("status = 'HELD'")
                .HasDatabaseName("ix_holds_expires_at_held");
        });
    }
}
