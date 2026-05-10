using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using UnityEngine;

namespace Mogul.Systems;

// Cash register interaction surface. State lives in MogulNetwork.Data.RegisterBalances
// (synced via HostSyncVar), so every client sees the same pending balance and any of
// them can press R to collect — the host validates and credits the player who pressed.
public static class CashRegister
{
    public static void AddSale(string locationId, float amount)
    {
        if (string.IsNullOrEmpty(locationId) || amount <= 0f) return;
        var amountStr = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MogulNetwork.RequestAction(MogulActions.AddRegisterSale, $"{locationId}:{amountStr}");
    }

    public static float GetPending(string locationId) =>
        MogulNetwork.Data.RegisterBalances.TryGetValue(locationId, out var v) ? v : 0f;

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
                    MogulNetwork.RequestCollectRegister(location.Id);
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
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
