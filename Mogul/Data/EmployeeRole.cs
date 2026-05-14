namespace Mogul.Data;

public enum EmployeeRole
{
    Cashier,
    Budtender,
    Runner,
}

public class HiredEmployeeData
{
    public string Id { get; set; } = "";
    public EmployeeRole Role { get; set; }
    public string DisplayName { get; set; } = "";
}
