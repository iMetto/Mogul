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
    public static List<SelectedProduct> CustomerOrder { get; private set; } = new();

    public static event Action<string, CheckoutResult> OnClosed;

    private static NPC _customer;
    private static GameObject _buildingRoot;

    public static void Open(string locationId, NPC npc, GameObject buildingRoot, List<SelectedProduct> order)
    {
        if (IsOpen) return;

        _customer = npc;
        _buildingRoot = buildingRoot;
        ActiveLocationId = locationId;
        CustomerOrder = order;

        IsOpen = true;
        MelonLogger.Msg($"[Mogul] CheckoutHandler: opened for {locationId} — {order.Count} item(s)");
    }

    // Removes ordered packages from storage and deposits the total to the register.
    // Partial fulfillment is accepted (stock may have changed since DecidePurchases ran).
    public static void FulfillOrder()
    {
        if (!IsOpen) return;

        float total = 0f;
        float tip   = 0f;
        foreach (var item in CustomerOrder)
        {
            int fulfilled = 0;
            for (int i = 0; i < item.Quantity; i++)
            {
                if (StorageScanner.TakeOne(_buildingRoot, item.ProductId, item.QualityLevel))
                {
                    total += item.Price;
                    fulfilled++;
                }
                else break;
            }
            tip += fulfilled * item.Price * Mathf.Max(0f, item.EnjoyScale - 1f);
        }

        if (total <= 0f)
        {
            MelonLogger.Msg($"[Mogul] CheckoutHandler: stock ran out during fulfillment");
            _customer?.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            _customer?.DialogueHandler?.WorldspaceRend?.ShowText("Stock ran out...", 3f);
            Close(CheckoutResult.NoStock);
            return;
        }

        CashRegister.AddSale(ActiveLocationId, total);
        if (tip > 0.01f)
            CashRegister.AddSale(ActiveLocationId, tip);

        string saleMsg = tip > 0.01f
            ? $"[Mogul] Order fulfilled — ${total:F0} + ${tip:F2} tip"
            : $"[Mogul] Order fulfilled — ${total:F0}";
        MelonLogger.Msg(saleMsg);

        string npcMsg = tip > 0.01f ? $"Thanks! ${total:F0} + ${tip:F0} tip" : $"Thanks! ${total:F0}";
        _customer?.VoiceOverEmitter?.Play(EVOLineType.Thanks);
        _customer?.DialogueHandler?.WorldspaceRend?.ShowText(npcMsg, 3f);
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
        CustomerOrder = new List<SelectedProduct>();
        OnClosed?.Invoke(locationId, result);
    }
}
