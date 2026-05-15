using System.Collections.Generic;
using Mogul.Data;

namespace Mogul.Systems;

public class BudtenderProduct
{
    public string ProductId { get; }
    public string DisplayName { get; }

    public BudtenderProduct(string productId, string displayName)
    {
        ProductId = productId;
        DisplayName = displayName;
    }
}

public static class EmployeeProduction
{
    public const string TestBudtenderProductId = "ogkush";
    public const int BudtenderStackQuantity = 20;
    public const int BudtenderOgKushPerDay = BudtenderStackQuantity;
    public const int WorkingDayStart = 800;
    public const int WorkingDayEnd = 1700;

    public static readonly BudtenderProduct[] BudtenderProducts =
    [
        new BudtenderProduct("ogkush", "OG Kush"),
        new BudtenderProduct("sourdiesel", "Sour Diesel"),
        new BudtenderProduct("greencrack", "Green Crack"),
        new BudtenderProduct("granddaddypurple", "Granddaddy Purple"),
    ];

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
        return budtenderCount <= 0 ? 0 : budtenderCount * BudtenderStackQuantity;
    }

    public static bool TryGetBudtenderProduct(string productId, out BudtenderProduct product)
    {
        product = null;
        if (string.IsNullOrEmpty(productId)) return false;
        foreach (var candidate in BudtenderProducts)
        {
            if (candidate.ProductId != productId) continue;
            product = candidate;
            return true;
        }
        return false;
    }

    public static string GetBudtenderProductName(string productId)
    {
        return TryGetBudtenderProduct(productId, out var product) ? product.DisplayName : productId;
    }

    public static int GetReadyDay(int startDay, int startTime)
    {
        return startTime <= WorkingDayStart ? startDay : startDay + 1;
    }

    public static bool IsOrderComplete(BudtenderOrderData order, int currentDay, int currentTime)
    {
        if (order == null || order.ReadyDay < 0) return false;
        return currentDay > order.ReadyDay
            || (currentDay == order.ReadyDay && currentTime >= WorkingDayEnd);
    }
}
