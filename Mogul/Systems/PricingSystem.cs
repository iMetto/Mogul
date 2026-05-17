using System;
using Mogul.Data;

namespace Mogul.Systems;

public static class PricingSystem
{
    public const float MinMultiplier = 0.1f;
    public const float MaxMultiplier = 5f;
    public const float MultiplierStep = 0.1f;
    public const float PriceStep = 5f;

    public static float GetLocationMultiplier(string locationId)
    {
        return MogulNetwork.Data?.LocationPriceMultipliers != null
            && !string.IsNullOrEmpty(locationId)
            && MogulNetwork.Data.LocationPriceMultipliers.TryGetValue(locationId, out var value)
            ? ClampMultiplier(value)
            : 1f;
    }

    public static ProductPriceData GetProductPricing(string locationId, string productId, int qualityLevel)
    {
        if (MogulNetwork.Data?.LocationProductPrices != null
            && !string.IsNullOrEmpty(locationId)
            && !string.IsNullOrEmpty(productId)
            && MogulNetwork.Data.LocationProductPrices.TryGetValue(locationId, out var map)
            && map != null
            && map.TryGetValue(Key(productId, qualityLevel), out var data))
            return data ?? new ProductPriceData();
        return new ProductPriceData();
    }

    public static float GetProductMultiplier(string locationId, string productId, int qualityLevel) =>
        ClampMultiplier(GetProductPricing(locationId, productId, qualityLevel).Multiplier);

    public static float GetManualPrice(string locationId, string productId, int qualityLevel) =>
        GetProductPricing(locationId, productId, qualityLevel).ManualPrice;

    public static float ResolvePrice(string locationId, string productId, int qualityLevel, float basePrice)
    {
        var pricing = GetProductPricing(locationId, productId, qualityLevel);
        if (pricing.ManualPrice >= 0f)
            return Math.Max(0f, pricing.ManualPrice);
        return Math.Max(0f, basePrice * GetLocationMultiplier(locationId) * ClampMultiplier(pricing.Multiplier));
    }

    public static void RequestSetLocationMultiplier(string locationId, float multiplier)
    {
        if (string.IsNullOrEmpty(locationId)) return;
        MogulNetwork.RequestAction(MogulActions.SetLocationPriceMultiplier,
            $"{locationId}:{ClampMultiplier(multiplier).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    public static void RequestSetProductMultiplier(string locationId, string productId, int qualityLevel, float multiplier)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(productId)) return;
        MogulNetwork.RequestAction(MogulActions.SetProductPriceMultiplier,
            $"{locationId}:{productId}:{qualityLevel}:{ClampMultiplier(multiplier).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    public static void RequestSetManualPrice(string locationId, string productId, int qualityLevel, float price)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(productId)) return;
        MogulNetwork.RequestAction(MogulActions.SetProductManualPrice,
            $"{locationId}:{productId}:{qualityLevel}:{Math.Max(0f, price).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    public static void RequestClearManualPrice(string locationId, string productId, int qualityLevel)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(productId)) return;
        MogulNetwork.RequestAction(MogulActions.ClearProductManualPrice,
            $"{locationId}:{productId}:{qualityLevel}");
    }

    public static void SetLocationMultiplier(MogulSaveData data, string locationId, float multiplier)
    {
        data.LocationPriceMultipliers[locationId] = ClampMultiplier(multiplier);
    }

    public static void SetProductMultiplier(MogulSaveData data, string locationId, string productId, int qualityLevel, float multiplier)
    {
        GetOrCreate(data, locationId, productId, qualityLevel).Multiplier = ClampMultiplier(multiplier);
    }

    public static void SetManualPrice(MogulSaveData data, string locationId, string productId, int qualityLevel, float price)
    {
        GetOrCreate(data, locationId, productId, qualityLevel).ManualPrice = Math.Max(0f, price);
    }

    public static void ClearManualPrice(MogulSaveData data, string locationId, string productId, int qualityLevel)
    {
        GetOrCreate(data, locationId, productId, qualityLevel).ManualPrice = -1f;
    }

    public static string Key(string productId, int qualityLevel) => $"{productId}|{qualityLevel}";

    public static float ClampMultiplier(float value)
    {
        if (value < MinMultiplier) return MinMultiplier;
        if (value > MaxMultiplier) return MaxMultiplier;
        return (float)Math.Round(value, 1);
    }

    private static ProductPriceData GetOrCreate(MogulSaveData data, string locationId, string productId, int qualityLevel)
    {
        if (!data.LocationProductPrices.TryGetValue(locationId, out var map))
        {
            map = new System.Collections.Generic.Dictionary<string, ProductPriceData>();
            data.LocationProductPrices[locationId] = map;
        }

        var key = Key(productId, qualityLevel);
        if (!map.TryGetValue(key, out var pricing) || pricing == null)
        {
            pricing = new ProductPriceData();
            map[key] = pricing;
        }

        return pricing;
    }
}
