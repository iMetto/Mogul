using System;
using System.Collections.Generic;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.VoiceOver;
using MelonLoader;
using UnityEngine;


namespace Mogul.Systems;

public enum CheckoutResult { Sold, NoStock, Dismissed, Denied }

public static class CheckoutHandler
{
    public static bool IsOpen { get; private set; }
    public static string ActiveLocationId { get; private set; }
    public static List<SelectedProduct> CustomerOrder { get; private set; } = new();

    public static event Action<string, CheckoutResult> OnClosed;

    private static NPC _customer;
    private static GameObject _buildingRoot;
    private static bool _savedCanMove = true;
    private static bool _playerLocked;

    public static void Open(string locationId, NPC npc, GameObject buildingRoot, List<SelectedProduct> order)
    {
        if (IsOpen) return;

        _customer = npc;
        _buildingRoot = buildingRoot;
        ActiveLocationId = locationId;
        CustomerOrder = order;

        IsOpen = true;
        LockPlayerOnCustomer(npc);
        MelonLogger.Msg($"[Mogul] CheckoutHandler: opened for {locationId} — {order.Count} item(s)");
    }

    // Removes ordered packages from storage and deposits the total to the register.
    // Partial fulfillment is accepted (stock may have changed since DecidePurchases ran).
    public static void FulfillOrder()
    {
        if (!IsOpen) return;

        var result = FulfillOrderDirect(ActiveLocationId, _customer, _buildingRoot, CustomerOrder);
        Close(result);
    }

    public static CheckoutResult FulfillOrderDirect(string locationId, NPC customer, GameObject buildingRoot, List<SelectedProduct> order)
    {
        float total = 0f;
        float tip   = 0f;
        foreach (var item in order ?? new List<SelectedProduct>())
        {
            int fulfilled = 0;
            for (int i = 0; i < item.Quantity; i++)
            {
                if (StorageScanner.TakeOne(buildingRoot, item.ProductId, item.QualityLevel))
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
            customer?.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            customer?.DialogueHandler?.WorldspaceRend?.ShowText("Stock ran out...", 3f);
            return CheckoutResult.NoStock;
        }

        CashRegister.AddSale(locationId, total);
        if (tip > 0.01f)
            CashRegister.AddSale(locationId, tip);

        string saleMsg = tip > 0.01f
            ? $"[Mogul] Order fulfilled — ${total:F0} + ${tip:F2} tip"
            : $"[Mogul] Order fulfilled — ${total:F0}";
        MelonLogger.Msg(saleMsg);

        string npcMsg = tip > 0.01f ? $"Thanks! ${total:F0} + ${tip:F0} tip" : $"Thanks! ${total:F0}";
        customer?.VoiceOverEmitter?.Play(EVOLineType.Thanks);
        customer?.DialogueHandler?.WorldspaceRend?.ShowText(npcMsg, 3f);
        return CheckoutResult.Sold;
    }

    // Player pressed Q — UI closes, NPC stays waiting at the counter.
    public static void Dismiss()
    {
        if (!IsOpen) return;
        Close(CheckoutResult.Dismissed);
    }

    public static void Deny()
    {
        if (!IsOpen) return;
        _customer?.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
        _customer?.DialogueHandler?.WorldspaceRend?.ShowText("Forget it.", 2f);
        Close(CheckoutResult.Denied);
    }

    private static void Close(CheckoutResult result)
    {
        IsOpen = false;
        string locationId = ActiveLocationId;
        ActiveLocationId = null;
        _customer = null;
        _buildingRoot = null;
        CustomerOrder = new List<SelectedProduct>();
        UnlockPlayer();
        OnClosed?.Invoke(locationId, result);
    }

    private static void LockPlayerOnCustomer(NPC customer)
    {
        if (_playerLocked) return;

        try
        {
            var movement = PlayerMovement.Instance;
            if (movement != null)
            {
                _savedCanMove = movement.CanMove;
                movement.CanMove = false;
            }

            var camera = PlayerCamera.Instance;
            if (camera != null)
            {
                if (customer != null && customer.transform != null)
                    camera.FocusCameraOnTarget(customer.transform);
                camera.SetCanLook(false);
            }

            _playerLocked = true;
        }
        catch { }
    }

    private static void UnlockPlayer()
    {
        if (!_playerLocked) return;

        try
        {
            PlayerCamera.Instance?.StopFocus();
            PlayerCamera.Instance?.SetCanLook(true);

            var movement = PlayerMovement.Instance;
            if (movement != null)
                movement.CanMove = _savedCanMove;
        }
        catch { }

        _playerLocked = false;
    }
}
