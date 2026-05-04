using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Interaction;
using Il2CppScheduleOne.VoiceOver;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building;
using UnityEngine;

namespace Mogul.Systems;

public static class CustomerManager
{
    public enum CustomerState { WalkingIn, WaitingInQueue, WaitingAtCounter, Leaving }

    private class CustomerEntry
    {
        public NPC Npc;
        public string LocationId;
        public NavigationBuilder NavBuilder;
        public CustomerState State;
        public float DespawnTimer = -1f;
        public int QueueIndex;
        public Vector3 PendingWorldTarget;
        public Action PendingArrival;
    }

    private static readonly Dictionary<NPC, CustomerEntry> _active = new();

    public static void Initialize()
    {
        CheckoutHandler.OnClosed += OnCheckoutClosed;
    }

    public static void ClearQueueCache() => QueueSlots.ClearCache();

    public static void SpawnForNearestLocation(Vector3 playerPos)
    {
        if (!MogulNetwork.IsHost) return;

        if (!LocationGeometry.TryFindNearestLocation(playerPos, out var location))
        {
            MelonLogger.Warning("[Mogul] No owned+spawned location nearby");
            return;
        }

        if (!LocationSpawner.TryGetNavigationBuilder(location.Id, out var navBuilder))
        {
            MelonLogger.Warning($"[Mogul] NavigationBuilder not ready for {location.Id}");
            return;
        }

        if (!SellDesk.TryGetQueueAnchor(location.Id, out var worldQueueAnchor))
        {
            MelonLogger.Warning($"[Mogul] QueueAnchor not ready for {location.Id}");
            return;
        }

        var spawnPos = location.ComputeDoorExterior();
        CustomerSpawner.SpawnTestNPC(spawnPos, npc =>
        {
            int queueIdx = 0;
            foreach (var kvp in _active)
                if (kvp.Value.LocationId == location.Id && kvp.Value.State != CustomerState.Leaving)
                    queueIdx++;

            var entry = new CustomerEntry
            {
                Npc = npc,
                LocationId = location.Id,
                NavBuilder = navBuilder,
                State = CustomerState.WalkingIn,
                QueueIndex = queueIdx,
            };
            _active[npc] = entry;

            MelonLogger.Msg($"[Mogul] Spawning customer queueIdx={queueIdx} for {location.Id}");
            if (queueIdx == 0)
            {
                if (LocationGeometry.TryGetCounterWorldPos(location.Id, out var counterWorld))
                {
                    entry.PendingWorldTarget = counterWorld;
                    entry.PendingArrival = () => OnArrived(entry);
                }
                navBuilder.SendNPCToPosition(npc, worldQueueAnchor, onArrival: () => OnArrived(entry));
            }
            else
            {
                var slot = QueueSlots.Get(queueIdx, location, worldQueueAnchor, navBuilder);
                MelonLogger.Msg($"[Mogul] Queue {queueIdx}: pos={slot.Position} exterior={slot.IsExterior}");
                SendToQueueSlot(entry, slot, () =>
                {
                    entry.State = CustomerState.WaitingInQueue;
                    MelonLogger.Msg($"[Mogul] Customer arrived at queue position {queueIdx} in {location.Id}");
                });
            }
        });
    }

    public static void Tick(Vector3 playerPos)
    {
        // Despawn timers
        var toRemove = new List<NPC>();
        foreach (var kvp in _active)
        {
            if (kvp.Value.DespawnTimer > 0f)
            {
                kvp.Value.DespawnTimer -= Time.deltaTime;
                if (kvp.Value.DespawnTimer <= 0f)
                {
                    CustomerSpawner.Despawn(kvp.Value.Npc);
                    toRemove.Add(kvp.Key);
                }
            }
        }
        foreach (var k in toRemove) _active.Remove(k);

        // Proximity arrival detection for NPCs walking to counter
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.State == CustomerState.WalkingIn && e.PendingArrival != null)
            {
                if (Vector3.Distance(e.Npc.transform.position, e.PendingWorldTarget) < 1.5f)
                {
                    e.Npc.Movement?.Stop();
                    var cb = e.PendingArrival;
                    e.PendingArrival = null;
                    cb.Invoke();
                }
            }
        }

        CashRegister.Tick();

        // Don't handle counter interaction while checkout UI is open
        if (CheckoutHandler.IsOpen) return;

        var hovered = Singleton<InteractionManager>.Instance?.HoveredInteractableObject;
        bool eDown = Input.GetKeyDown(KeyCode.E) && !GameInput.IsTyping;

        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            if (!SellDesk.TryGetCounterInteractable(location.Id, out var counterInteractable)) continue;

            CustomerEntry waiting = null;
            foreach (var kvp in _active)
                if (kvp.Value.State == CustomerState.WaitingAtCounter && kvp.Value.LocationId == location.Id)
                { waiting = kvp.Value; break; }

            if (waiting == null)
            {
                if (counterInteractable._interactionState != InteractableObject.EInteractableState.Disabled)
                    counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
                continue;
            }

            counterInteractable.SetMessage("[E] Take order");
            if (counterInteractable._interactionState != InteractableObject.EInteractableState.Label)
                counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Label);

            if (hovered == counterInteractable && eDown)
            {
                if (LocationSpawner.TryGetSpawnedBuilding(location.Id, out var buildingRoot))
                    CheckoutHandler.Open(location.Id, waiting.Npc, buildingRoot);
            }
        }
    }

    private static void OnArrived(CustomerEntry entry)
    {
        if (entry.State != CustomerState.WalkingIn) return;
        entry.PendingArrival = null;

        // Quick stock check on arrival — NPC leaves immediately if shelves are empty
        if (!LocationSpawner.TryGetSpawnedBuilding(entry.LocationId, out var buildingRoot) ||
            StorageScanner.Scan(buildingRoot).Count == 0)
        {
            entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            entry.Npc.DialogueHandler?.WorldspaceRend?.ShowText("Nothing here for me...", 3f);
            StartLeaving(entry);
            return;
        }

        entry.State = CustomerState.WaitingAtCounter;
        entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Greeting);
        MelonLogger.Msg($"[Mogul] Customer waiting at counter in {entry.LocationId}");
    }

    private static void OnCheckoutClosed(string locationId, CheckoutResult result)
    {
        // Dismissed = player closed UI with Q, NPC stays waiting — do nothing
        if (result == CheckoutResult.Dismissed) return;

        // Sold or NoStock = NPC leaves (voice/text already handled by CheckoutHandler)
        CustomerEntry entry = null;
        foreach (var kvp in _active)
            if (kvp.Value.State == CustomerState.WaitingAtCounter && kvp.Value.LocationId == locationId)
            { entry = kvp.Value; break; }

        if (entry == null) return;
        StartLeaving(entry);
    }

    private static void StartLeaving(CustomerEntry entry)
    {
        entry.PendingArrival = null;
        entry.State = CustomerState.Leaving;
        entry.NavBuilder.RecallNPC(entry.Npc);
        entry.DespawnTimer = 6f;

        if (entry.QueueIndex == 0)
            AdvanceQueue(entry.LocationId);
    }

    private static void SendToQueueSlot(CustomerEntry entry, QueueSlot slot, Action onArrival)
    {
        if (slot.IsExterior)
        {
            entry.Npc.Movement?.SetDestination(slot.Position);
            entry.State = CustomerState.WaitingInQueue;
            onArrival?.Invoke();
        }
        else
        {
            entry.NavBuilder.SendNPCToPosition(entry.Npc, slot.Position, onArrival);
        }
    }

    private static void AdvanceQueue(string locationId)
    {
        if (!SellDesk.TryGetQueueAnchor(locationId, out var worldQueueAnchor)) return;
        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        var waiting = new List<CustomerEntry>();
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId == locationId && e.State == CustomerState.WaitingInQueue)
                waiting.Add(e);
        }
        if (waiting.Count == 0) return;

        foreach (var kvp in _active)
            if (kvp.Value.LocationId == locationId && kvp.Value.State == CustomerState.WaitingAtCounter)
                return;

        waiting.Sort((a, b) => a.QueueIndex.CompareTo(b.QueueIndex));

        foreach (var e in waiting)
        {
            e.QueueIndex--;
            if (e.QueueIndex == 0)
            {
                e.State = CustomerState.WalkingIn;
                if (LocationGeometry.TryGetCounterWorldPos(locationId, out var counterWorld))
                {
                    e.PendingWorldTarget = counterWorld;
                    e.PendingArrival = () => OnArrived(e);
                }
                e.NavBuilder.SendNPCToPosition(e.Npc, worldQueueAnchor, onArrival: () => OnArrived(e));
                MelonLogger.Msg($"[Mogul] Advancing next customer to counter in {locationId}");
            }
            else
            {
                var slot = QueueSlots.Get(e.QueueIndex, location, worldQueueAnchor, e.NavBuilder);
                SendToQueueSlot(e, slot, null);
            }
        }
    }

}
