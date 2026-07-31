namespace DaftechCrm.Domain.Entities;

public class TimeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DateOnly Date { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? FinishTime { get; set; }
    public double? TotalHours { get; set; }
}
