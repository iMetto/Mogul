using System;
using System.Collections.Generic;
using Il2CppFishNet;
using Il2CppFishNet.Object;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI;
using MelonLoader;
using UnityEngine;

namespace Mogul.Systems;

public static class MogulDropZoneSpawner
{
    private const float ContainerDespawnDelay = 3f;
    private const float InteractRadius        = 2.5f;

    private static readonly Dictionary<string, GameObject>         _spawned      = new();
    private static readonly Dictionary<string, InteractableObject> _interactables = new();
    private static readonly HashSet<string>                        _deposited     = new();
    private static readonly Dictionary<string, float>             _pendingDestroy = new();
    private static readonly HashSet<string>                       _inZone          = new();
    private static readonly Dictionary<string, float>            _lastHintTime    = new();
    private const float HintRefreshInterval = 1f;

    public static void Tick()
    {
        if (!MogulNetwork.IsHost) return;
        var data = MogulNetwork.Data;
        if (data == null) return;

        foreach (var quest in MogulQuestSystem.All)
        {
            if (quest.Event != MogulObjectiveEvent.DropOffDrug) continue;
            if (string.IsNullOrEmpty(quest.TargetId)) continue;
            if (quest.WorldPosition == Vector3.zero) continue;

            bool claimed   = MogulQuestSystem.IsClaimed(quest, data);
            bool available = quest.IsAvailable(data);
            bool accepted  = MogulQuestSystem.IsAccepted(quest, data);

            if (claimed || !available || !accepted) continue;

            if (_spawned.TryGetValue(quest.Id, out var existing) && existing != null) continue;
            if (_spawned.ContainsKey(quest.Id)) continue; // spawn in progress

            SpawnDropZone(quest);
        }

        // Dynamic hover label — update message each frame when player is looking at the zone
        var hovered = Singleton<InteractionManager>.Instance?.HoveredInteractableObject;
        if (hovered != null)
        {
            foreach (var kvp in _interactables)
            {
                if (kvp.Value != hovered) continue;
                if (_deposited.Contains(kvp.Key)) break;

                var quest = MogulQuestSystem.Find(kvp.Key);
                if (quest == null) break;

                int have     = CountItemInInventory(quest.TargetId);
                int required = quest.Target;

                string msg = have >= required
                    ? $"[Q] Deposit {required} OG Kush ({have} in hand)"
                    : $"Need {required} OG Kush — you have {have}";

                hovered.SetMessage(msg);
                break;
            }
        }

        // Proximity hint + Q key trigger
        var playerPos = S1API.Entities.Player.Local?.Position;
        bool qDown    = Input.GetKeyDown(KeyCode.Q) && !GameInput.IsTyping;

        if (playerPos.HasValue)
        {
            var nowInZone = new HashSet<string>();

            foreach (var kvp in new Dictionary<string, GameObject>(_spawned))
            {
                if (kvp.Value == null) continue;
                if (_deposited.Contains(kvp.Key)) continue;
                if (Vector3.Distance(playerPos.Value, kvp.Value.transform.position) > InteractRadius) continue;

                nowInZone.Add(kvp.Key);

                var quest = MogulQuestSystem.Find(kvp.Key);
                if (quest != null)
                {
                    int have     = CountItemInInventory(quest.TargetId);
                    int required = quest.Target;
                    string msg   = have >= required
                        ? $"[Q] Deposit {required} OG Kush ({have} in hand)"
                        : $"Need {required} OG Kush — you have {have}";

                    if (!_lastHintTime.TryGetValue(kvp.Key, out float last) || Time.time - last >= HintRefreshInterval)
                    {
                        Singleton<HintDisplay>.Instance?.ShowHint(msg, 1.5f);
                        _lastHintTime[kvp.Key] = Time.time;
                    }
                }

                if (qDown) OnDropZoneInteract(kvp.Key);
            }

            foreach (var id in new List<string>(_inZone))
                if (!nowInZone.Contains(id)) { _inZone.Remove(id); _lastHintTime.Remove(id); }
            foreach (var id in nowInZone) _inZone.Add(id);
        }

        // Pending destroy timers
        foreach (var kvp in new Dictionary<string, float>(_pendingDestroy))
        {
            if (Time.time >= kvp.Value)
            {
                _pendingDestroy.Remove(kvp.Key);
                DestroyContainer(kvp.Key);
            }
        }
    }

    public static void OnQuestClaimed(string questId)
    {
        if (!MogulNetwork.IsHost) return;
        _pendingDestroy[questId] = Time.time + ContainerDespawnDelay;
        MelonLogger.Msg($"[Mogul] DropZone: {questId} claimed — container despawns in {ContainerDespawnDelay}s");
    }

    public static void DespawnAll()
    {
        foreach (var kvp in _spawned)
        {
            if (kvp.Value != null)
                DestroyGo(kvp.Value);
        }
        _spawned.Clear();
        _interactables.Clear();
        _deposited.Clear();
        _pendingDestroy.Clear();
        _inZone.Clear();
        _lastHintTime.Clear();
    }

    private static void SpawnDropZone(MogulQuestDefinition quest)
    {
        _spawned[quest.Id] = null; // lock against re-entry this frame

        try
        {
            var prefab = new S1MAPI.Core.PrefabRef("Dumpster_Built");
            var prefabGo = prefab.Find();
            if (prefabGo == null)
            {
                MelonLogger.Warning("[Mogul] DropZone: Dumpster_Built prefab not found");
                _spawned.Remove(quest.Id);
                return;
            }

            var go = UnityEngine.Object.Instantiate(prefabGo);
            go.name = "Mogul_DropZone_" + quest.Id;
            go.transform.position = quest.WorldPosition;
            go.SetActive(true);

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                InstanceFinder.ServerManager.Spawn(netObj);

            _spawned[quest.Id] = go;

            // Strip vanilla trash storage so it can't intercept interaction
            try
            {
                var trash = go.GetComponent<TrashContainerItem>();
                if (trash != null) UnityEngine.Object.Destroy(trash);
            }
            catch (Exception ex) { MelonLogger.Warning("[Mogul] DropZone: trash strip: " + ex.Message); }

            // Repurpose the dumpster's own InteractableObject for the hover label.
            // It sits on a mesh child that already has colliders, so hover works naturally.
            var interactable = go.GetComponentInChildren<InteractableObject>();
            if (interactable != null)
            {
                try { interactable.onInteractStart?.RemoveAllListeners(); } catch { }
                try { interactable.onInteractEnd?.RemoveAllListeners(); } catch { }
                interactable.SetMessage($"[Q] Deposit {quest.Target} OG Kush");
                interactable.SetInteractableState(InteractableObject.EInteractableState.Label);
                _interactables[quest.Id] = interactable;
            }

            MelonLogger.Msg($"[Mogul] DropZone: spawned '{quest.Id}' at {quest.WorldPosition}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Mogul] DropZone: spawn failed: " + ex);
            _spawned.Remove(quest.Id);
        }
    }

    private static void OnDropZoneInteract(string questId)
    {
        var quest = MogulQuestSystem.Find(questId);
        if (quest == null) return;

        var data = MogulNetwork.Data;
        if (MogulQuestSystem.IsClaimed(quest, data)) return;
        if (MogulQuestSystem.IsComplete(quest, data)) return;

        int required = quest.Target;
        string itemId = quest.TargetId;

        int found = CountItemInInventory(itemId);

        if (found < required)
        {
            Singleton<NotificationsManager>.Instance?.SendNotification(
                "Dead Drop",
                $"Need {required} OG Kush — you have {found}",
                null, 3f, false);
            MelonLogger.Msg($"[Mogul] DropZone: need {required} {itemId}, player has {found}");
            return;
        }

        // Consume items from inventory
        var inv = Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerInventory>.Instance;
        if (inv == null) return;

        int remaining = required;
        foreach (var slot in inv.GetAllInventorySlots())
        {
            if (remaining <= 0) break;
            if (slot?.ItemInstance == null) continue;
            var product = slot.ItemInstance.TryCast<ProductItemInstance>();
            if (product == null) continue;
            var def = product.Definition?.TryCast<ProductDefinition>();
            if (def?.ID != itemId) continue;

            int take = Math.Min(remaining, slot.Quantity);
            remaining -= take;
            if (take >= slot.Quantity)
                slot.ClearStoredInstance();
            else
                slot.ChangeQuantity(-take);
        }

        _deposited.Add(questId);
        MogulQuestSystem.RequestRecordEvent(MogulObjectiveEvent.DropOffDrug, itemId, required);
        MelonLogger.Msg($"[Mogul] DropZone: {questId} — deposited {required}x {itemId}");

        Singleton<NotificationsManager>.Instance?.SendNotification(
            "Dead Drop",
            "Delivered. Claim your reward in the app.",
            null, 4f, true);

        if (_interactables.TryGetValue(questId, out var interactable) && interactable != null)
        {
            interactable.SetMessage("Delivered — claim reward in the app");
            interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
        }
    }

    private static int CountItemInInventory(string itemId)
    {
        var inv = Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerInventory>.Instance;
        if (inv == null) return 0;

        int count = 0;
        foreach (var slot in inv.GetAllInventorySlots())
        {
            if (slot?.ItemInstance == null) continue;
            var product = slot.ItemInstance.TryCast<ProductItemInstance>();
            if (product == null) continue;
            var def = product.Definition?.TryCast<ProductDefinition>();
            if (def?.ID == itemId)
                count += slot.Quantity;
        }
        return count;
    }

    private static void DestroyContainer(string questId)
    {
        if (!_spawned.TryGetValue(questId, out var go)) return;
        _spawned.Remove(questId);
        _interactables.Remove(questId);
        DestroyGo(go);
        MelonLogger.Msg($"[Mogul] DropZone: container for '{questId}' removed");
    }

    private static void DestroyGo(GameObject go)
    {
        if (go == null) return;
        try
        {
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned && InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.Despawn(netObj);
        }
        catch (Exception ex) { MelonLogger.Warning("[Mogul] DropZone: network despawn error: " + ex.Message); }
        try { UnityEngine.Object.Destroy(go); }
        catch (Exception ex) { MelonLogger.Warning("[Mogul] DropZone: Destroy error: " + ex.Message); }
    }
}
