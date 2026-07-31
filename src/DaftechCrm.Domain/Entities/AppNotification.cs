using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationRecipientType RecipientType { get; set; }

    /// <summary>Employee/Client Id, or a role broadcast key like "ALL_ADMIN" / "ALL_IT_SUPPORT".</summary>
    public string RecipientId { get; set; } = default!;

    public string EventType { get; set; } = default!;
    public string Message { get; set; } = default!;
    public DateTimeOffset DateSent { get; set; } = DateTimeOffset.UtcNow;
    public bool ReadStatus { get; set; }
}
