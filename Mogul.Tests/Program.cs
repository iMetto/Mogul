using System;
using System.Collections.Generic;
using System.Linq;
using Mogul.Data;
using Mogul.Systems;

namespace Mogul.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Run("empty shelves rejects as EmptyShelves", EmptyShelvesRejects);
        Run("all products over per-item budget reject as TooExpensive", TooExpensiveRejects);
        Run("low appeal products reject as LowAppeal", LowAppealRejects);
        Run("selected purchases never exceed committed budget or stock", PurchasesStayWithinBudgetAndStock);
        Run("purchase decisions are deterministic for the same seed", PurchaseDecisionsAreDeterministic);
        Run("selected product quality names track current labels", SelectedProductQualityNames);
        Run("storage product quality names track current labels", StorageProductQualityNames);
        Run("budtender production is 20 OG Kush per elapsed day", BudtenderProductionScalesByElapsedDays);
        Run("non-budtender roles do not produce OG Kush", NonBudtendersDoNotProduce);
        Run("budtender daily yield is zero without budtenders", BudtenderDailyYieldRequiresBudtender);
        Run("quest progress reads owned properties and employees", QuestProgressReadsSaveData);

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failed}");

        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"FAIL {name}");
            Console.WriteLine($"     {ex.Message}");
        }
    }

    private static void EmptyShelvesRejects()
    {
        var (selected, rejection) = CustomerDemand.DecidePurchases(
            DefaultPrefs(totalBudget: 100f, maxBudgetPerItem: 60f),
            new List<StorageProduct>(),
            seed: 100);

        AssertEqual(RejectionReason.EmptyShelves, rejection);
        AssertEqual(0, selected.Count);
    }

    private static void TooExpensiveRejects()
    {
        var stock = new List<StorageProduct>
        {
            Product("premium", price: 75f, quantity: 3, quality: 4, "focused"),
            Product("standard", price: 61f, quantity: 3, quality: 2, "calming"),
        };

        var (selected, rejection) = CustomerDemand.DecidePurchases(
            DefaultPrefs(totalBudget: 100f, maxBudgetPerItem: 60f),
            stock,
            seed: 101);

        AssertEqual(RejectionReason.TooExpensive, rejection);
        AssertEqual(0, selected.Count);
    }

    private static void LowAppealRejects()
    {
        var stock = new List<StorageProduct>
        {
            Product("bad-fit", price: 10f, quantity: 5, quality: 0, "smelly"),
        };

        var prefs = new CustomerPreferences
        {
            QualityExpectation = 1.0f,
            MaxBudgetPerItem = 60f,
            TotalBudget = 100f,
            WeedAffinity = -1.0f,
            PreferredEffectIds = new[] { "focused", "calming", "euphoric" },
        };

        var (selected, rejection) = CustomerDemand.DecidePurchases(prefs, stock, seed: 102);

        AssertEqual(RejectionReason.LowAppeal, rejection);
        AssertEqual(0, selected.Count);
    }

    private static void PurchasesStayWithinBudgetAndStock()
    {
        var prefs = DefaultPrefs(totalBudget: 150f, maxBudgetPerItem: 90f);
        var stock = new List<StorageProduct>
        {
            Product("cheap-great", price: 5f, quantity: 40, quality: 4, "focused", "calming", "euphoric"),
            Product("mid", price: 25f, quantity: 2, quality: 3, "focused"),
        };

        var (selected, rejection) = CustomerDemand.DecidePurchases(prefs, stock, seed: 103);

        AssertEqual(RejectionReason.None, rejection);
        AssertTrue(selected.Count > 0, "expected at least one selected product");
        AssertTrue(selected.Sum(p => p.Total) <= prefs.TotalBudget, "selected products exceeded total budget");

        foreach (var selection in selected)
        {
            var source = stock.Single(p => p.ProductId == selection.ProductId);
            AssertTrue(selection.Quantity >= 1, $"quantity for {selection.ProductId} was below 1");
            AssertTrue(selection.Quantity <= source.TotalPackages, $"quantity for {selection.ProductId} exceeded stock");
            AssertTrue(selection.Price <= prefs.MaxBudgetPerItem, $"price for {selection.ProductId} exceeded per-item budget");
        }
    }

    private static void PurchaseDecisionsAreDeterministic()
    {
        var prefs = DefaultPrefs(totalBudget: 120f, maxBudgetPerItem: 80f);
        var stock = new List<StorageProduct>
        {
            Product("a", price: 15f, quantity: 10, quality: 4, "focused"),
            Product("b", price: 20f, quantity: 10, quality: 3, "calming"),
            Product("c", price: 30f, quantity: 10, quality: 2, "euphoric"),
        };

        var first = CustomerDemand.DecidePurchases(prefs, stock, seed: 104);
        var second = CustomerDemand.DecidePurchases(prefs, stock, seed: 104);

        AssertEqual(first.rejection, second.rejection);
        AssertEqual(first.selected.Count, second.selected.Count);

        for (int i = 0; i < first.selected.Count; i++)
        {
            AssertEqual(first.selected[i].ProductId, second.selected[i].ProductId);
            AssertEqual(first.selected[i].Quantity, second.selected[i].Quantity);
        }
    }

    private static void SelectedProductQualityNames()
    {
        AssertEqual("Trash", new SelectedProduct { QualityLevel = 0 }.QualityName);
        AssertEqual("Poor", new SelectedProduct { QualityLevel = 1 }.QualityName);
        AssertEqual("Standard", new SelectedProduct { QualityLevel = 2 }.QualityName);
        AssertEqual("Premium", new SelectedProduct { QualityLevel = 3 }.QualityName);
        AssertEqual("Heavenly", new SelectedProduct { QualityLevel = 4 }.QualityName);
        AssertEqual("?", new SelectedProduct { QualityLevel = 99 }.QualityName);
    }

    private static void StorageProductQualityNames()
    {
        AssertEqual("Trash", new StorageProduct { QualityLevel = 0 }.QualityName);
        AssertEqual("Poor", new StorageProduct { QualityLevel = 1 }.QualityName);
        AssertEqual("Standard", new StorageProduct { QualityLevel = 2 }.QualityName);
        AssertEqual("Premium", new StorageProduct { QualityLevel = 3 }.QualityName);
        AssertEqual("Heavenly", new StorageProduct { QualityLevel = 4 }.QualityName);
        AssertEqual("Unknown", new StorageProduct { QualityLevel = 99 }.QualityName);
    }

    private static void BudtenderProductionScalesByElapsedDays()
    {
        var employees = new List<HiredEmployeeData>
        {
            new() { Id = "b1", Role = EmployeeRole.Budtender, DisplayName = "Bud" },
            new() { Id = "c1", Role = EmployeeRole.Cashier, DisplayName = "Cash" },
        };

        AssertEqual(0, EmployeeProduction.CalculateBudtenderYield(employees, 0));
        AssertEqual(20, EmployeeProduction.CalculateBudtenderYield(employees, 1));
        AssertEqual(60, EmployeeProduction.CalculateBudtenderYield(employees, 3));
    }

    private static void NonBudtendersDoNotProduce()
    {
        var employees = new List<HiredEmployeeData>
        {
            new() { Id = "c1", Role = EmployeeRole.Cashier, DisplayName = "Cash" },
            new() { Id = "r1", Role = EmployeeRole.Runner, DisplayName = "Run" },
        };

        AssertEqual(0, EmployeeProduction.CalculateBudtenderYield(employees, 5));
    }

    private static void BudtenderDailyYieldRequiresBudtender()
    {
        AssertEqual(0, EmployeeProduction.GetBudtenderDailyYield(0));
        AssertEqual(20, EmployeeProduction.GetBudtenderDailyYield(1));
        AssertEqual(40, EmployeeProduction.GetBudtenderDailyYield(2));
    }

    private static void QuestProgressReadsSaveData()
    {
        var data = new MogulSaveData
        {
            RegisteredLocationIds = new List<string> { "loc_westville_01" },
            LocationEmployees = new Dictionary<string, List<HiredEmployeeData>>
            {
                ["loc_westville_01"] = new()
                {
                    new HiredEmployeeData { Id = "c1", Role = EmployeeRole.Cashier, DisplayName = "Cash" },
                },
            },
        };

        var propertyQuest = MogulQuestSystem.Find("first_property");
        var cashierQuest = MogulQuestSystem.Find("hire_cashier");
        var budtenderQuest = MogulQuestSystem.Find("hire_budtender");

        AssertEqual(1, MogulQuestSystem.GetProgress(propertyQuest, data));
        AssertEqual(1, MogulQuestSystem.GetProgress(cashierQuest, data));
        AssertEqual(0, MogulQuestSystem.GetProgress(budtenderQuest, data));
        AssertTrue(MogulQuestSystem.IsComplete(propertyQuest, data), "owned property quest should be complete");
        AssertTrue(!MogulQuestSystem.IsComplete(budtenderQuest, data), "budtender quest should not be complete");
    }


    private static CustomerPreferences DefaultPrefs(float totalBudget, float maxBudgetPerItem) => new()
    {
        QualityExpectation = 0.25f,
        MaxBudgetPerItem = maxBudgetPerItem,
        TotalBudget = totalBudget,
        WeedAffinity = 0.8f,
        PreferredEffectIds = new[] { "focused", "calming", "euphoric" },
    };

    private static StorageProduct Product(string id, float price, int quantity, int quality, params string[] effects) => new()
    {
        ProductId = id,
        DisplayName = id,
        Price = price,
        TotalPackages = quantity,
        QualityLevel = quality,
        EffectIds = effects.ToList(),
    };

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

}
