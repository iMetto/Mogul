using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using MelonLoader;
using S1API.Money;
using UnityEngine;

namespace Mogul.Systems;

public static class CashRegister
{
    private static readonly Dictionary<string, float> _pending = new();

    public static void AddSale(string locationId, float amount)
    {
        if (!_pending.ContainsKey(locationId))
            _pending[locationId] = 0f;
        _pending[locationId] += amount;
        MelonLogger.Msg($"[Mogul] Register +${amount:F2} for {locationId} (pending: ${_pending[locationId]:F2})");
    }

    public static float GetPending(string locationId) =>
        _pending.TryGetValue(locationId, out var v) ? v : 0f;

    public static void Tick()
    {
        var hovered = Singleton<InteractionManager>.Instance?.HoveredInteractableObject;

        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            if (!SellDesk.TryGetRegisterInteractable(location.Id, out var interactable)) continue;

            float balance = GetPending(location.Id);
            bool isHovered = hovered != null && hovered == interactable;

            if (balance > 0f && isHovered && !CheckoutHandler.IsOpen)
            {
                interactable.SetMessage($"[R] Collect ${balance:F2}");
                if (interactable._interactionState != InteractableObject.EInteractableState.Label)
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Label);

                if (Input.GetKeyDown(KeyCode.R) && !GameInput.IsTyping)
                {
                    _pending[location.Id] = 0f;
                    Money.ChangeCashBalance(balance, visualizeChange: true, playCashSound: true);
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
                    MelonLogger.Msg($"[Mogul] Collected ${balance:F2} from {location.Id}");
                }
            }
            else if (balance > 0f)
            {
                interactable.SetMessage($"Balance: ${balance:F2}");
                if (interactable._interactionState != InteractableObject.EInteractableState.Label)
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Label);
            }
            else
            {
                if (interactable._interactionState != InteractableObject.EInteractableState.Disabled)
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            }
        }
    }
}
