using System;
using System.Collections.Generic;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.VoiceOver;
using MelonLoader;
using UnityEngine;

namespace Mogul.Systems;

public enum CheckoutResult { Sold, NoStock, Dismissed }

public static class CheckoutHandler
{
    public static bool IsOpen { get; private set; }
    public static string ActiveLocationId { get; private set; }
    public static List<StorageProduct> Products { get; private set; } = new();

    public static event Action<string, CheckoutResult> OnClosed;

    private static NPC _customer;
    private static GameObject _buildingRoot;

    public static void Open(string locationId, NPC npc, GameObject buildingRoot)
    {
        if (IsOpen) return;

        _customer = npc;
        _buildingRoot = buildingRoot;
        ActiveLocationId = locationId;
        Products = StorageScanner.Scan(buildingRoot);

        if (Products.Count == 0)
        {
            npc.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            npc.DialogueHandler?.WorldspaceRend?.ShowText("Nothing here for me...", 3f);
            MelonLogger.Msg($"[Mogul] CheckoutHandler: no stock in {locationId}");
            OnClosed?.Invoke(locationId, CheckoutResult.NoStock);
            return;
        }

        IsOpen = true;
        MelonLogger.Msg($"[Mogul] CheckoutHandler: opened for {locationId} — {Products.Count} product(s)");
    }

    // Player selected a product from the UI.
    public static void Sell(StorageProduct product)
    {
        if (!IsOpen) return;

        bool removed = StorageScanner.TakeOne(_buildingRoot, product.ProductId, product.QualityLevel);
        if (!removed)
        {
            MelonLogger.Warning("[Mogul] CheckoutHandler.Sell: TakeOne failed — stock changed between scan and sale");
            _customer?.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            _customer?.DialogueHandler?.WorldspaceRend?.ShowText("Hmm, can't find that...", 3f);
            Close(CheckoutResult.NoStock);
            return;
        }

        // Money goes to the register, not the player directly.
        CashRegister.AddSale(ActiveLocationId, product.Price);
        MelonLogger.Msg($"[Mogul] Sold {product.DisplayName} ({product.QualityName}) for ${product.Price:F0}");

        _customer?.VoiceOverEmitter?.Play(EVOLineType.Thanks);
        _customer?.DialogueHandler?.WorldspaceRend?.ShowText($"Thanks! ${product.Price:F0}", 3f);

        Close(CheckoutResult.Sold);
    }

    // Player pressed Q — UI closes, NPC stays waiting at the counter.
    public static void Dismiss()
    {
        if (!IsOpen) return;
        Close(CheckoutResult.Dismissed);
    }

    private static void Close(CheckoutResult result)
    {
        IsOpen = false;
        string locationId = ActiveLocationId;
        ActiveLocationId = null;
        _customer = null;
        _buildingRoot = null;
        Products = new();
        OnClosed?.Invoke(locationId, result);
    }
}
