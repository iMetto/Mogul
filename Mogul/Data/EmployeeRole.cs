namespace Mogul.Data;

public enum EmployeeRole
{
    Cashier,
    Budtender,
    // Legacy save value. Runner hiring/spawning is intentionally disabled.
    Runner,
}

public class HiredEmployeeData
{
    public string Id { get; set; } = "";
    public EmployeeRole Role { get; set; }
    public string DisplayName { get; set; } = "";
}
