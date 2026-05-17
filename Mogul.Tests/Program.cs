using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mogul.Data;
using Mogul.Systems;

namespace Mogul.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main(string[] args)
    {
        if (args.Contains("--simulate-demand"))
            return RunDemandSimulation(args);

        Run("empty shelves rejects as EmptyShelves", EmptyShelvesRejects);
        Run("all products over per-item budget reject as TooExpensive", TooExpensiveRejects);
        Run("low appeal products reject as LowAppeal", LowAppealRejects);
        Run("selected purchases never exceed committed budget or stock", PurchasesStayWithinBudgetAndStock);
        Run("purchase decisions are deterministic for the same seed", PurchaseDecisionsAreDeterministic);
        Run("selected product quality names track current labels", SelectedProductQualityNames);
        Run("storage product quality names track current labels", StorageProductQualityNames);
        Run("budtender production is one 20 pkg stack per elapsed day", BudtenderProductionScalesByElapsedDays);
        Run("non-budtender roles do not produce OG Kush", NonBudtendersDoNotProduce);
        Run("budtender daily yield is zero without budtenders", BudtenderDailyYieldRequiresBudtender);
        Run("budtender order timing follows working day", BudtenderOrderTimingFollowsWorkingDay);
        Run("budtender product catalog has starter weed", BudtenderProductCatalogHasStarterWeed);
        Run("strain recipes encode ordered ingredients", StrainRecipesEncodeOrderedIngredients);
        Run("strain recipe ingredients allow duplicate ordered slots", StrainRecipeIngredientsAllowDuplicateOrderedSlots);
        Run("strain recipe ingredient removal compacts slots", StrainRecipeIngredientRemovalCompactsSlots);
        Run("strain ingredient slots unlock by reach", StrainIngredientSlotsUnlockByReach);
        Run("demand simulation is deterministic and depletes inventory", DemandSimulationIsDeterministicAndDepletesInventory);
        Run("quest progress reads owned properties and employees", QuestProgressReadsSaveData);

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failed}");

        return _failed == 0 ? 0 : 1;
    }

    private static int RunDemandSimulation(string[] args)
    {
        var config = new DemandSimulationConfig
        {
            Inventory = DemandSimulationSystem.CreateMixedScenarioInventory(),
        };
        var customInventory = new List<StorageProduct>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario":
                    var scenario = ReadArg(args, ref i, "--scenario");
                    config.Inventory = scenario.Equals("starter", StringComparison.OrdinalIgnoreCase)
                        ? DemandSimulationSystem.CreateStarterScenarioInventory()
                        : DemandSimulationSystem.CreateMixedScenarioInventory();
                    break;
                case "--reach":
                    config.Reach = int.Parse(ReadArg(args, ref i, "--reach"));
                    break;
                case "--customers":
                    config.CustomerCount = int.Parse(ReadArg(args, ref i, "--customers"));
                    break;
                case "--seed":
                    config.Seed = int.Parse(ReadArg(args, ref i, "--seed"));
                    break;
                case "--no-deplete":
                    config.DepleteInventory = false;
                    break;
                case "--stock":
                    customInventory.Add(ParseStock(ReadArg(args, ref i, "--stock")));
                    break;
            }
        }

        if (customInventory.Count > 0)
            config.Inventory = customInventory;

        Console.WriteLine(DemandSimulationSystem.Run(config).ToText());
        return 0;
    }

    private static string ReadArg(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
            throw new InvalidOperationException($"{flag} requires a value");
        index++;
        return args[index];
    }

    private static StorageProduct ParseStock(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 6)
            throw new InvalidOperationException("--stock format is id|name|price|packages|quality|effect,effect");

        return DemandSimulationSystem.Product(
            parts[0],
            parts[1],
            float.Parse(parts[2], CultureInfo.InvariantCulture),
            int.Parse(parts[3], CultureInfo.InvariantCulture),
            int.Parse(parts[4], CultureInfo.InvariantCulture),
            parts[5].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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
        };

        AssertEqual(0, EmployeeProduction.CalculateBudtenderYield(employees, 5));
    }

    private static void BudtenderDailyYieldRequiresBudtender()
    {
        AssertEqual(0, EmployeeProduction.GetBudtenderDailyYield(0));
        AssertEqual(20, EmployeeProduction.GetBudtenderDailyYield(1));
        AssertEqual(40, EmployeeProduction.GetBudtenderDailyYield(2));
    }

    private static void BudtenderOrderTimingFollowsWorkingDay()
    {
        AssertEqual(12, EmployeeProduction.GetReadyDay(12, 800));
        AssertEqual(13, EmployeeProduction.GetReadyDay(12, 801));
        AssertEqual(0, EmployeeProduction.GetReadyDay(0, 800));

        var order = new BudtenderOrderData
        {
            ProductId = "ogkush",
            Quantity = EmployeeProduction.BudtenderStackQuantity,
            StartDay = 12,
            StartTime = 900,
            ReadyDay = 13,
        };

        AssertTrue(!EmployeeProduction.IsOrderComplete(order, 13, 1659), "order completed before working day end");
        AssertTrue(EmployeeProduction.IsOrderComplete(order, 13, 1700), "order did not complete at working day end");
        AssertTrue(EmployeeProduction.IsOrderComplete(order, 14, 800), "order did not remain complete after ready day");
    }

    private static void BudtenderProductCatalogHasStarterWeed()
    {
        AssertEqual(4, EmployeeProduction.BudtenderProducts.Length);
        AssertTrue(EmployeeProduction.TryGetBudtenderProduct("ogkush", out var og), "missing ogkush");
        AssertEqual("OG Kush", og.DisplayName);
        AssertTrue(EmployeeProduction.TryGetBudtenderProduct("sourdiesel", out _), "missing sourdiesel");
        AssertTrue(EmployeeProduction.TryGetBudtenderProduct("greencrack", out _), "missing greencrack");
        AssertTrue(EmployeeProduction.TryGetBudtenderProduct("granddaddypurple", out _), "missing granddaddypurple");
    }

    private static void StrainRecipesEncodeOrderedIngredients()
    {
        var payload = StrainMixingSystem.BuildOrderPayload("loc_westville_01", "ogkush", new[] { "cuke", "gasoline" });

        AssertTrue(StrainMixingSystem.TryParseOrderPayload(payload, out var request), "payload did not parse");
        AssertEqual("loc_westville_01", request.LocationId);
        AssertEqual("ogkush", request.BaseProductId);
        AssertEqual(2, request.IngredientIds.Count);
        AssertEqual("cuke", request.IngredientIds[0]);
        AssertEqual("gasoline", request.IngredientIds[1]);
        AssertEqual("OG Kush + Cuke + Gasoline", StrainMixingSystem.BuildRecipeName(request.BaseProductId, request.IngredientIds));
    }

    private static void StrainRecipeIngredientsAllowDuplicateOrderedSlots()
    {
        var ingredients = new List<string>();

        AssertTrue(StrainMixingSystem.TryAddIngredient(ingredients, "cuke", 3), "first ingredient was not added");
        AssertTrue(StrainMixingSystem.TryAddIngredient(ingredients, "cuke", 3), "duplicate ingredient was not added");
        AssertTrue(StrainMixingSystem.TryAddIngredient(ingredients, "gasoline", 3), "third ingredient was not added");
        AssertTrue(!StrainMixingSystem.TryAddIngredient(ingredients, "viagra", 3), "ingredient added past slot limit");

        AssertEqual(3, ingredients.Count);
        AssertEqual("cuke", ingredients[0]);
        AssertEqual("cuke", ingredients[1]);
        AssertEqual("gasoline", ingredients[2]);
    }

    private static void StrainRecipeIngredientRemovalCompactsSlots()
    {
        var ingredients = new List<string> { "cuke", "gasoline", "viagra" };

        AssertTrue(StrainMixingSystem.TryRemoveIngredientAt(ingredients, 1), "ingredient was not removed");

        AssertEqual(2, ingredients.Count);
        AssertEqual("cuke", ingredients[0]);
        AssertEqual("viagra", ingredients[1]);
        AssertTrue(!StrainMixingSystem.TryRemoveIngredientAt(ingredients, 5), "out-of-range removal should fail");
    }

    private static void StrainIngredientSlotsUnlockByReach()
    {
        AssertEqual(0, StrainMixingSystem.GetUnlockedIngredientSlots(0));
        AssertEqual(1, StrainMixingSystem.GetUnlockedIngredientSlots(750));
        AssertEqual(2, StrainMixingSystem.GetUnlockedIngredientSlots(3500));
        AssertEqual(3, StrainMixingSystem.GetUnlockedIngredientSlots(6000));
        AssertEqual(4, StrainMixingSystem.GetUnlockedIngredientSlots(10000));
        AssertEqual(5, StrainMixingSystem.GetUnlockedIngredientSlots(14000));
        AssertEqual(6, StrainMixingSystem.GetUnlockedIngredientSlots(20000));
        AssertEqual(7, StrainMixingSystem.GetUnlockedIngredientSlots(25000));
        AssertEqual(8, StrainMixingSystem.GetUnlockedIngredientSlots(35000));
    }

    private static void DemandSimulationIsDeterministicAndDepletesInventory()
    {
        var config = new DemandSimulationConfig
        {
            Reach = 3500,
            CustomerCount = 100,
            Seed = 9001,
            Inventory = DemandSimulationSystem.CreateStarterScenarioInventory(),
        };

        var first = DemandSimulationSystem.Run(config);
        var second = DemandSimulationSystem.Run(config);

        AssertEqual(first.FulfilledCustomers, second.FulfilledCustomers);
        AssertEqual(first.PackagesSold, second.PackagesSold);
        AssertEqual(first.Revenue, second.Revenue);
        AssertTrue(first.Products.Any(p => p.PackagesRemaining < p.StartingPackages), "expected at least one product to sell down");
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

        MogulQuestSystem.AddProgress(data, "cash_threshold_3000", 1);

        var firstQuest = MogulQuestSystem.Find("westville_statement");
        var task = MogulQuestSystem.Find("move_ogkush_12");

        AssertEqual(1, MogulQuestSystem.GetProgress(firstQuest, data));
        AssertTrue(MogulQuestSystem.IsComplete(firstQuest, data), "first quest should be complete after cash threshold");

        MogulQuestSystem.Claim(firstQuest, data);
        AssertTrue(MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockPropertiesTab), "properties tab should unlock");
        AssertTrue(MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockWestvillePurchase), "westville purchase should unlock");
        AssertTrue(task.IsAvailable(data), "tasks should unlock after first quest");
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
