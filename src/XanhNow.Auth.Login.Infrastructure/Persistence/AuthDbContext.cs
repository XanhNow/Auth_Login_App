using Microsoft.EntityFrameworkCore;

namespace XanhNow.Auth.Login.Infrastructure.Persistence;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<UserRecord> Users => Set<UserRecord>();

    public DbSet<UserPhoneHistoryRecord> UserPhoneHistories => Set<UserPhoneHistoryRecord>();

    public DbSet<LoginAttemptRecord> LoginAttempts => Set<LoginAttemptRecord>();

    public DbSet<AuthAuditLogRecord> AuthAuditLogs => Set<AuthAuditLogRecord>();

    public DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.UserId).HasName("pk_users");
            entity.Property(user => user.UserId).HasColumnName("user_id");
            entity.Property(user => user.PhoneNumber).HasColumnName("phone_number").IsRequired();
            entity.Property(user => user.PhoneNumberNormalized).HasColumnName("phone_number_normalized").IsRequired();
            entity.Property(user => user.PhoneNumberMasked).HasColumnName("phone_number_masked").IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(user => user.PasswordAlgorithm).HasColumnName("password_algorithm").IsRequired();
            entity.Property(user => user.PasswordPepperVersion).HasColumnName("password_pepper_version").IsRequired();
            entity.Property(user => user.Status).HasColumnName("status").IsRequired();
            entity.Property(user => user.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue(0);
            entity.Property(user => user.LockedUntil).HasColumnName("locked_until");
            entity.Property(user => user.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.HasIndex(user => user.PhoneNumberNormalized).IsUnique().HasDatabaseName("ux_users_phone_number_normalized");
            entity.HasIndex(user => user.Status).HasDatabaseName("ix_users_status");
            entity.HasIndex(user => user.LastLoginAt).HasDatabaseName("ix_users_last_login_at");
        });

        modelBuilder.Entity<UserPhoneHistoryRecord>(entity =>
        {
            entity.ToTable("user_phone_histories");
            entity.HasKey(history => history.Id).HasName("pk_user_phone_histories");
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.UserId).HasColumnName("user_id");
            entity.Property(history => history.OldPhoneNumberMasked).HasColumnName("old_phone_number_masked").IsRequired();
            entity.Property(history => history.OldPhoneNumberHash).HasColumnName("old_phone_number_hash").IsRequired();
            entity.Property(history => history.NewPhoneNumberMasked).HasColumnName("new_phone_number_masked").IsRequired();
            entity.Property(history => history.NewPhoneNumberHash).HasColumnName("new_phone_number_hash").IsRequired();
            entity.Property(history => history.ChangedAt).HasColumnName("changed_at").IsRequired();
            entity.Property(history => history.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(history => history.ReasonCode).HasColumnName("reason_code").IsRequired();
            entity.Property(history => history.CorrelationId).HasColumnName("correlation_id").IsRequired();
            entity.HasOne<UserRecord>().WithMany().HasForeignKey(history => history.UserId);
        });

        modelBuilder.Entity<LoginAttemptRecord>(entity =>
        {
            entity.ToTable("login_attempts");
            entity.HasKey(attempt => attempt.Id).HasName("pk_login_attempts");
            entity.Property(attempt => attempt.Id).HasColumnName("id");
            entity.Property(attempt => attempt.UserId).HasColumnName("user_id");
            entity.Property(attempt => attempt.PhoneNumberHash).HasColumnName("phone_number_hash").IsRequired();
            entity.Property(attempt => attempt.PhoneNumberMasked).HasColumnName("phone_number_masked").IsRequired();
            entity.Property(attempt => attempt.IpHash).HasColumnName("ip_hash").IsRequired();
            entity.Property(attempt => attempt.ClientInfoHash).HasColumnName("client_info_hash");
            entity.Property(attempt => attempt.Result).HasColumnName("result").IsRequired();
            entity.Property(attempt => attempt.FailureReasonCode).HasColumnName("failure_reason_code");
            entity.Property(attempt => attempt.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(attempt => attempt.CorrelationId).HasColumnName("correlation_id").IsRequired();
            entity.HasIndex(attempt => attempt.CreatedAt).HasDatabaseName("ix_login_attempts_created_at");
        });

        modelBuilder.Entity<AuthAuditLogRecord>(entity =>
        {
            entity.ToTable("auth_audit_logs");
            entity.HasKey(audit => audit.Id).HasName("pk_auth_audit_logs");
            entity.Property(audit => audit.Id).HasColumnName("id");
            entity.Property(audit => audit.UserId).HasColumnName("user_id");
            entity.Property(audit => audit.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(audit => audit.Severity).HasColumnName("severity").IsRequired();
            entity.Property(audit => audit.PhoneNumberMasked).HasColumnName("phone_number_masked");
            entity.Property(audit => audit.SessionIdHash).HasColumnName("session_id_hash");
            entity.Property(audit => audit.IpHash).HasColumnName("ip_hash");
            entity.Property(audit => audit.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb").IsRequired();
            entity.Property(audit => audit.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(audit => audit.CorrelationId).HasColumnName("correlation_id").IsRequired();
            entity.HasIndex(audit => audit.CorrelationId).HasDatabaseName("ix_auth_audit_logs_correlation_id");
        });

        modelBuilder.Entity<OutboxEventRecord>(entity =>
        {
            entity.ToTable("outbox_events");
            entity.HasKey(outbox => outbox.EventId).HasName("pk_outbox_events");
            entity.Property(outbox => outbox.EventId).HasColumnName("event_id");
            entity.Property(outbox => outbox.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(outbox => outbox.AggregateType).HasColumnName("aggregate_type").IsRequired();
            entity.Property(outbox => outbox.AggregateId).HasColumnName("aggregate_id").IsRequired();
            entity.Property(outbox => outbox.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(outbox => outbox.Status).HasColumnName("status").IsRequired();
            entity.Property(outbox => outbox.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(outbox => outbox.AvailableAt).HasColumnName("available_at").IsRequired();
            entity.Property(outbox => outbox.PublishedAt).HasColumnName("published_at");
            entity.Property(outbox => outbox.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(outbox => outbox.CorrelationId).HasColumnName("correlation_id").IsRequired();
            entity.HasIndex(outbox => new { outbox.Status, outbox.AvailableAt }).HasDatabaseName("ix_outbox_status_available_at");
            entity.HasIndex(outbox => outbox.CreatedAt).HasDatabaseName("ix_outbox_created_at");
            entity.HasIndex(outbox => outbox.CorrelationId).HasDatabaseName("ix_outbox_correlation_id");
        });
    }
}
