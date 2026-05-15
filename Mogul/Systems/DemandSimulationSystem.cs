using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mogul.Systems;

public sealed class DemandSimulationConfig
{
    public int Reach = 3500;
    public int CustomerCount = 10000;
    public int Seed = 1337;
    public bool DepleteInventory = true;
    public List<StorageProduct> Inventory = new List<StorageProduct>();
}

public sealed class DemandSimulationProductResult
{
    public string ProductId;
    public string DisplayName;
    public int QualityLevel;
    public float Price;
    public int StartingPackages;
    public int EndingPackages;
    public int Customers;
    public int PackagesSold;
    public float Revenue;

    public int PackagesRemaining => EndingPackages;
    public float SellThrough => StartingPackages <= 0 ? 0f : PackagesSold / (float)StartingPackages;
}

public sealed class DemandSimulationReport
{
    public int Reach;
    public ReachTier Tier;
    public int CustomerCount;
    public int Seed;
    public bool DepleteInventory;
    public int FulfilledCustomers;
    public int Orders;
    public int PackagesSold;
    public float Revenue;
    public Dictionary<RejectionReason, int> Rejections = new Dictionary<RejectionReason, int>();
    public List<DemandSimulationProductResult> Products = new List<DemandSimulationProductResult>();

    public float FulfillmentRate => CustomerCount <= 0 ? 0f : FulfilledCustomers / (float)CustomerCount;
    public float AverageRevenuePerCustomer => CustomerCount <= 0 ? 0f : Revenue / CustomerCount;
    public float AverageRevenuePerFulfilledCustomer => FulfilledCustomers <= 0 ? 0f : Revenue / FulfilledCustomers;

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mogul demand simulation");
        sb.AppendLine($"Reach: {Reach} ({ReachSystem.GetTierName(Tier)})");
        sb.AppendLine($"Customers: {CustomerCount:N0}");
        sb.AppendLine($"Seed: {Seed}");
        sb.AppendLine($"Inventory mode: {(DepleteInventory ? "depleting" : "reusable")}");
        sb.AppendLine();
        sb.AppendLine($"Fulfilled customers: {FulfilledCustomers:N0} ({FulfillmentRate:P1})");
        sb.AppendLine($"Orders: {Orders:N0}");
        sb.AppendLine($"Packages sold: {PackagesSold:N0}");
        sb.AppendLine($"Revenue: ${Revenue:N0}");
        sb.AppendLine($"Avg revenue/customer: ${AverageRevenuePerCustomer:N2}");
        sb.AppendLine($"Avg revenue/buyer: ${AverageRevenuePerFulfilledCustomer:N2}");

        if (Rejections.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Rejections");
            foreach (var entry in Rejections.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"- {entry.Key}: {entry.Value:N0}");
        }

        if (Products.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Products");
            foreach (var product in Products.OrderByDescending(p => p.Revenue))
            {
                sb.AppendLine(
                    $"- {product.DisplayName} q{product.QualityLevel} ${product.Price:N0}: " +
                    $"{product.PackagesSold:N0} sold, ${product.Revenue:N0}, " +
                    $"{product.Customers:N0} buyers, {product.PackagesRemaining:N0} left");
            }
        }

        return sb.ToString();
    }
}

public static class DemandSimulationSystem
{
    public static DemandSimulationReport Run(DemandSimulationConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (config.CustomerCount < 0) throw new ArgumentOutOfRangeException(nameof(config.CustomerCount));

        var stock = CloneInventory(config.Inventory);
        var report = new DemandSimulationReport
        {
            Reach = config.Reach,
            Tier = ReachSystem.GetTier(config.Reach),
            CustomerCount = config.CustomerCount,
            Seed = config.Seed,
            DepleteInventory = config.DepleteInventory,
        };

        var products = stock.ToDictionary(ProductKey, p => new DemandSimulationProductResult
        {
            ProductId = p.ProductId,
            DisplayName = p.DisplayName,
            QualityLevel = p.QualityLevel,
            Price = p.Price,
            StartingPackages = p.TotalPackages,
            EndingPackages = p.TotalPackages,
        });

        for (int i = 0; i < config.CustomerCount; i++)
        {
            var prefs = CustomerDemand.GeneratePreferences(CombineSeed(config.Seed, i, 0x51D), config.Reach);
            var available = config.DepleteInventory
                ? stock.Where(p => p.TotalPackages > 0).ToList()
                : CloneInventory(stock);
            var (selected, rejection) = CustomerDemand.DecidePurchases(prefs, available, CombineSeed(config.Seed, i, 0xA71));

            if (rejection != RejectionReason.None || selected.Count == 0)
            {
                AddRejection(report, rejection);
                continue;
            }

            report.FulfilledCustomers++;
            report.Orders += selected.Count;

            foreach (var selection in selected)
            {
                var key = ProductKey(selection.ProductId, selection.QualityLevel);
                if (!products.TryGetValue(key, out var product))
                    continue;

                product.Customers++;
                product.PackagesSold += selection.Quantity;
                product.Revenue += selection.Total;
                report.PackagesSold += selection.Quantity;
                report.Revenue += selection.Total;

                if (!config.DepleteInventory) continue;

                var stockProduct = stock.FirstOrDefault(p => ProductKey(p) == key);
                if (stockProduct != null)
                {
                    stockProduct.TotalPackages = Math.Max(0, stockProduct.TotalPackages - selection.Quantity);
                    product.EndingPackages = stockProduct.TotalPackages;
                }
            }
        }

        report.Products = products.Values
            .OrderByDescending(p => p.Revenue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return report;
    }

    public static List<StorageProduct> CreateStarterScenarioInventory()
        => new List<StorageProduct>
        {
            Product("ogkush", "OG Kush", 35f, 240, 2, "calming"),
            Product("sourdiesel", "Sour Diesel", 50f, 180, 2, "energizing"),
            Product("greencrack", "Green Crack", 65f, 120, 3, "focused", "energizing"),
            Product("granddaddypurple", "Grand Daddy Purple", 80f, 80, 3, "sedating", "calming"),
        };

    public static List<StorageProduct> CreateMixedScenarioInventory()
        => new List<StorageProduct>
        {
            Product("ogkush", "OG Kush", 35f, 180, 2, "calming"),
            Product("ogkush_cuke", "OG Kush + Cuke", 55f, 120, 3, "calming", "refreshing"),
            Product("sourdiesel_energy", "Sour Diesel + Energy", 70f, 120, 3, "energizing", "focused"),
            Product("greencrack_gasoline", "Green Crack + Gasoline", 95f, 80, 4, "energizing", "toxic"),
            Product("granddaddypurple_horse", "Grand Daddy Purple + Horse", 110f, 60, 4, "sedating", "euphoric"),
        };

    public static StorageProduct Product(string id, string name, float price, int packages, int quality, params string[] effects)
        => new StorageProduct
        {
            ProductId = id,
            DisplayName = name,
            Price = price,
            TotalPackages = packages,
            QualityLevel = quality,
            EffectIds = effects?.Select(e => e.ToLowerInvariant()).ToList() ?? new List<string>(),
        };

    private static List<StorageProduct> CloneInventory(IEnumerable<StorageProduct> inventory)
        => inventory?.Select(p => Product(
            p.ProductId,
            p.DisplayName,
            p.Price,
            p.TotalPackages,
            p.QualityLevel,
            p.EffectIds?.ToArray() ?? Array.Empty<string>())).ToList() ?? new List<StorageProduct>();

    private static void AddRejection(DemandSimulationReport report, RejectionReason reason)
    {
        if (!report.Rejections.ContainsKey(reason))
            report.Rejections[reason] = 0;
        report.Rejections[reason]++;
    }

    private static string ProductKey(StorageProduct product) => ProductKey(product.ProductId, product.QualityLevel);
    private static string ProductKey(string productId, int qualityLevel) => $"{productId}:{qualityLevel}";

    private static int CombineSeed(int seed, int index, int salt)
    {
        unchecked
        {
            int hash = seed;
            hash = (hash * 397) ^ index;
            hash = (hash * 397) ^ salt;
            return hash;
        }
    }
}
