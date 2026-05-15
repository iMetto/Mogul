using System;
using System.Collections.Generic;
using MelonLoader;
using Mogul.Data;
using UnityEngine;

namespace Mogul.Systems;

public static class OnlineOrderSystem
{
    private const int MaxOpenOrders = 6;
    private const float GenerateIntervalSeconds = 90f;
    private static float _nextGenerateAt;

    private static readonly string[] BuyerNames =
    {
        "M. Carter", "Rico", "Sable", "Nina", "Dante", "Ash", "Voss", "Keene",
        "Mercer", "Juno", "Knox", "Vale",
    };

    public static void Tick()
    {
        if (!MogulNetwork.IsHost) return;
        if (Time.time < _nextGenerateAt) return;
        _nextGenerateAt = Time.time + GenerateIntervalSeconds;
        if (CountOpenOrders() >= MaxOpenOrders) return;
        MogulNetwork.RequestAction(MogulActions.GenerateOnlineOrder);
    }

    public static IReadOnlyList<OnlineOrderData> GetOrders() => MogulNetwork.Data.OnlineOrders;

    public static bool TryGenerateOrder(MogulSaveData data, out OnlineOrderData order)
    {
        order = null;
        if (data == null || data.RegisteredLocationIds == null || data.RegisteredLocationIds.Count == 0)
            return false;

        var candidates = new List<(MogulLocation location, GameObject root, List<StorageProduct> stock)>();
        foreach (var locId in data.RegisteredLocationIds)
        {
            var location = PropertySystem.Find(locId);
            if (location == null) continue;
            if (!LocationSpawner.TryGetSpawnedBuilding(locId, out var root) || root == null) continue;
            var stock = StorageScanner.Scan(root);
            if (stock.Count > 0)
                candidates.Add((location, root, stock));
        }
        if (candidates.Count == 0) return false;

        int seed = Environment.TickCount ^ (data.Reach * 397) ^ (data.OnlineOrderSequence * 7919);
        var rng = new System.Random(seed);
        var target = candidates[rng.Next(candidates.Count)];
        var profile = CustomerTypes.PickForReach(data.Reach, rng);
        var prefs = CustomerDemand.GeneratePreferences(seed, data.Reach);
        prefs = CustomerTypes.ApplyToPreferences(prefs, profile, rng);

        var (selected, rejection) = CustomerDemand.DecidePurchases(prefs, target.stock, seed ^ 0x7137);
        if (selected.Count == 0)
        {
            MelonLogger.Msg($"[Mogul] Online order skipped ({rejection}) for {target.location.Id}");
            return false;
        }

        var lines = new List<OnlineOrderLineData>();
        float total = 0f;
        float tip = 0f;
        foreach (var product in selected)
        {
            int qty = Math.Max(1, Mathf.RoundToInt(product.Quantity * profile.QuantityMultiplier));
            qty = Math.Min(qty, FindAvailable(target.stock, product.ProductId, product.QualityLevel));
            if (qty <= 0) continue;
            lines.Add(new OnlineOrderLineData
            {
                ProductId = product.ProductId,
                DisplayName = product.DisplayName,
                QualityLevel = product.QualityLevel,
                Quantity = qty,
                Price = product.Price,
            });
            total += product.Price * qty;
            tip += product.Price * qty * Math.Max(0f, product.EnjoyScale - 1f) * profile.TipMultiplier;
        }

        if (lines.Count == 0) return false;

        int createdDay = EmployeeSystem.GetCurrentGameDay();
        int createdTime = EmployeeSystem.GetCurrentGameTime();
        var (deadlineDay, deadlineTime) = AddHours(createdDay, createdTime, profile.DeadlineHours);

        data.OnlineOrderSequence++;
        order = new OnlineOrderData
        {
            Id = $"order_{createdDay}_{data.OnlineOrderSequence}",
            LocationId = target.location.Id,
            CustomerTypeId = profile.Id,
            CustomerName = BuildCustomerName(profile, rng),
            Lines = lines,
            Total = total,
            Tip = tip,
            CreatedDay = createdDay,
            CreatedTime = createdTime,
            DeadlineDay = deadlineDay,
            DeadlineTime = deadlineTime,
            Status = "Open",
        };
        data.OnlineOrders.Add(order);
        return true;
    }

    public static bool TryFulfillOrder(MogulSaveData data, string orderId, out string error)
    {
        error = null;
        var order = FindOrder(data, orderId);
        if (order == null) { error = "order not found"; return false; }
        if (order.Status != "Open") { error = "order not open"; return false; }
        if (!LocationSpawner.TryGetSpawnedBuilding(order.LocationId, out var root) || root == null)
        {
            error = "building not spawned";
            return false;
        }

        foreach (var line in order.Lines)
        {
            int available = CountAvailable(root, line.ProductId, line.QualityLevel);
            if (available < line.Quantity)
            {
                error = $"missing {line.DisplayName}";
                return false;
            }
        }

        foreach (var line in order.Lines)
            for (int i = 0; i < line.Quantity; i++)
                StorageScanner.TakeOne(root, line.ProductId, line.QualityLevel);

        data.RegisterBalances.TryGetValue(order.LocationId, out float existingSale);
        data.RegisterBalances[order.LocationId] = existingSale + order.Total + order.Tip;
        foreach (var line in order.Lines)
            MogulQuestSystem.AddProgress(data,
                MogulQuestSystem.BuildEventKey(MogulObjectiveEvent.SellDrug, line.ProductId),
                line.Quantity);
        order.Status = "Fulfilled";
        return true;
    }

    public static bool TryDismissOrder(MogulSaveData data, string orderId)
    {
        var order = FindOrder(data, orderId);
        if (order == null || order.Status != "Open") return false;
        order.Status = "Dismissed";
        return true;
    }

    private static OnlineOrderData FindOrder(MogulSaveData data, string orderId)
    {
        if (data?.OnlineOrders == null || string.IsNullOrEmpty(orderId)) return null;
        foreach (var order in data.OnlineOrders)
            if (order.Id == orderId)
                return order;
        return null;
    }

    private static int CountOpenOrders()
    {
        int count = 0;
        foreach (var order in MogulNetwork.Data.OnlineOrders)
            if (order.Status == "Open") count++;
        return count;
    }

    private static int FindAvailable(List<StorageProduct> stock, string productId, int quality)
    {
        foreach (var item in stock)
            if (item.ProductId == productId && item.QualityLevel == quality)
                return item.TotalPackages;
        return 0;
    }

    private static int CountAvailable(GameObject root, string productId, int quality)
    {
        int total = 0;
        foreach (var item in StorageScanner.Scan(root))
            if (item.ProductId == productId && item.QualityLevel == quality)
                total += item.TotalPackages;
        return total;
    }

    private static string BuildCustomerName(MogulCustomerTypeProfile profile, System.Random rng)
    {
        string name = BuyerNames[rng.Next(BuyerNames.Length)];
        return profile.Type switch
        {
            MogulCustomerType.BulkBuyer  => "Bulk: " + name,
            MogulCustomerType.GangLeader => "Gang: " + name,
            MogulCustomerType.Importer   => "Importer: " + name,
            _                            => name,
        };
    }

    private static (int day, int time) AddHours(int day, int time, int hours)
    {
        int minutes = (time / 100) * 60 + (time % 100) + hours * 60;
        day += minutes / (24 * 60);
        minutes %= 24 * 60;
        return (day, (minutes / 60) * 100 + minutes % 60);
    }
}
