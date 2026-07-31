using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string Category { get; set; } = default!;
    public string Description { get; set; } = default!;

    public Guid PerformedByEmployeeId { get; set; }
    public Employee PerformedByEmployee { get; set; } = default!;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.InProgress;
    public string? Remarks { get; set; }
}
