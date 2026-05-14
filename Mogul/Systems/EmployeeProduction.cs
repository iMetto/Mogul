using System.Collections.Generic;
using Mogul.Data;

namespace Mogul.Systems;

public static class EmployeeProduction
{
    public const string TestBudtenderProductId = "ogkush";
    public const int BudtenderOgKushPerDay = 20;

    public static int CountRole(IEnumerable<HiredEmployeeData> employees, EmployeeRole role)
    {
        int count = 0;
        if (employees == null) return count;
        foreach (var employee in employees)
            if (employee != null && employee.Role == role)
                count++;
        return count;
    }

    public static int CalculateBudtenderYield(IEnumerable<HiredEmployeeData> employees, int elapsedDays)
    {
        if (elapsedDays <= 0) return 0;
        return GetBudtenderDailyYield(CountRole(employees, EmployeeRole.Budtender)) * elapsedDays;
    }

    public static int GetBudtenderDailyYield(int budtenderCount)
    {
        return budtenderCount <= 0 ? 0 : budtenderCount * BudtenderOgKushPerDay;
    }
}
