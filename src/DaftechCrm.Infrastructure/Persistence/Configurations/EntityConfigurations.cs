using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaftechCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// MySQL has no native array/list column type, so List&lt;string&gt; and
/// List&lt;EmployeeRole&gt; properties are stored as delimited strings via
/// value converters, with a value comparer so EF's change tracking sees
/// mutations to the list contents (Add/Remove), not just reference changes.
/// </summary>
internal static class ValueConverters
{
    public static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => string.Join('|', v),
        v => v == string.Empty ? new List<string>() : v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
    );

    public static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
        v => v.ToList()
    );

    public static readonly ValueConverter<List<EmployeeRole>, string> RoleListConverter = new(
        v => string.Join('|', v.Select(r => r.ToString())),
        v => v == string.Empty ? new List<EmployeeRole>() : v.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<EmployeeRole>).ToList()
    );

    public static readonly ValueComparer<List<EmployeeRole>> RoleListComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (hash, r) => HashCode.Combine(hash, r.GetHashCode())),
        v => v.ToList()
    );
}

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.IdNumber).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Office).HasMaxLength(200);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.KycType).HasMaxLength(100);
        b.Property(x => x.KycContact).HasMaxLength(200);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.Username).HasMaxLength(50);
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(200);
        b.HasMany(x => x.Agreements).WithOne(a => a.Client).HasForeignKey(a => a.ClientId);
        b.HasMany(x => x.Tickets).WithOne(t => t.Client).HasForeignKey(t => t.ClientId);
    }
}

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> b)
    {
        b.ToTable("agreements");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.DocumentNumber).IsUnique();
        b.Property(x => x.ScannedFileUrl).HasMaxLength(500);
        b.Property(x => x.AgreementPlace).HasMaxLength(200);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.HasOne(x => x.Agreement).WithMany(a => a.Tickets).HasForeignKey(x => x.AgreementId);
        b.HasOne(x => x.AssignedEmployee).WithMany(e => e.AssignedTickets).HasForeignKey(x => x.AssignedEmployeeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.ForwardedByEmployee).WithMany().HasForeignKey(x => x.ForwardedByEmployeeId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.AuditTrail).WithOne(a => a.Ticket).HasForeignKey(a => a.TicketId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ClientConfirmationDeadline);
    }
}

public class TicketAuditEntryConfiguration : IEntityTypeConfiguration<TicketAuditEntry>
{
    public void Configure(EntityTypeBuilder<TicketAuditEntry> b)
    {
        b.ToTable("ticket_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Action).HasColumnType("text").IsRequired();
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employees");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Specialization).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisabledReason).HasMaxLength(500);
        b.Property(x => x.Username).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

        b.Property(x => x.Roles)
            .HasConversion(ValueConverters.RoleListConverter)
            .HasColumnType("varchar(200)")
            .Metadata.SetValueComparer(ValueConverters.RoleListComparer);

        b.Property(x => x.AllowedIpAddresses)
            .HasConversion(ValueConverters.StringListConverter)
            .HasColumnType("varchar(1000)")
            .Metadata.SetValueComparer(ValueConverters.StringListComparer);

        b.HasMany(x => x.DeviceSessions).WithOne(d => d.Employee).HasForeignKey(d => d.EmployeeId);
        b.HasMany(x => x.LoginRecords).WithOne(l => l.Employee).HasForeignKey(l => l.EmployeeId);
        b.HasMany(x => x.TimeLogs).WithOne(t => t.Employee).HasForeignKey(t => t.EmployeeId);
    }
}

public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> b)
    {
        b.ToTable("device_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceIdentifier).HasMaxLength(200).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired(); // 45 = max IPv6 literal length
    }
}

public class LoginRecordConfiguration : IEntityTypeConfiguration<LoginRecord>
{
    public void Configure(EntityTypeBuilder<LoginRecord> b)
    {
        b.ToTable("login_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
        b.Property(x => x.DeviceIdentifier).HasMaxLength(200).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => x.Timestamp);
    }
}

public class TimeLogConfiguration : IEntityTypeConfiguration<TimeLog>
{
    public void Configure(EntityTypeBuilder<TimeLog> b)
    {
        b.ToTable("time_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Date });
    }
}

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("maintenance_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.Property(x => x.Remarks).HasColumnType("text");
        b.HasOne(x => x.PerformedByEmployee).WithMany(e => e.MaintenanceRecords).HasForeignKey(x => x.PerformedByEmployeeId);
    }
}

public class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipientId).HasMaxLength(100).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Message).HasColumnType("text").IsRequired();
        b.HasIndex(x => new { x.RecipientType, x.RecipientId, x.ReadStatus });
    }
}

public class SatisfactionSurveyConfiguration : IEntityTypeConfiguration<SatisfactionSurvey>
{
    public void Configure(EntityTypeBuilder<SatisfactionSurvey> b)
    {
        b.ToTable("satisfaction_surveys");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImprovementFeedback).HasColumnType("text");
        b.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // One survey per ticket — the portal only offers the form once per resolved ticket.
        b.HasIndex(x => x.TicketId).IsUnique();
    }
}

public class LoginSessionConfiguration : IEntityTypeConfiguration<LoginSession>
{
    public void Configure(EntityTypeBuilder<LoginSession> b)
    {
        b.ToTable("login_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
        b.HasIndex(x => new { x.AccountType, x.AccountId, x.OnlineStatus });
        b.HasIndex(x => x.LastSeen);
    }
}
