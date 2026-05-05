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
using UnityEngine.AI;

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
        public int QueueIndex;
        public Vector3 PendingWorldTarget;
        public Action PendingArrival;

        // Demand — generated at spawn, resolved to an order at arrival
        public CustomerPreferences Preferences;
        public List<SelectedProduct> Order;

        // Health/recovery state — see Tick.
        public float StateEnteredAt;
        public int Retries;
        public Vector3 CurrentNavTarget;
        public Vector3 LastSamplePos;
        public float LastSampleTime;
        public int StuckStrikes;
        public float DespawnDeadline; // hard ceiling so a leaving NPC can't linger forever
    }

    private static readonly Dictionary<NPC, CustomerEntry> _active = new();

    // Locations whose queue should advance once any leaving NPC has cleared the door area.
    private static readonly HashSet<string> _pendingAdvance = new();
    private const float DoorClearRadius = 4f; // a leaver this close to the door blocks the next advance

    // Replacement bookkeeping: when a customer is lost before reaching the counter (bug,
    // stuck, stale reference) we schedule a delayed respawn so the lost sale isn't gone.
    // Capped per location to avoid feedback loops if the building is structurally broken.
    private static readonly Dictionary<string, int> _respawnsInFlight = new();
    private static readonly List<(string locationId, float deadline)> _scheduledRespawns = new();
    private const int MaxConcurrentRespawns = 2;
    private const float RespawnDelay = 3f;

    // Per-location cap on simultaneous active (non-leaving) customers. Must not exceed
    // (1 counter + QueueSlots.MaxExterior) or late arrivals stack on the last slot.
    private const int MaxConcurrentCustomers = 10;

    private const float StuckSampleInterval = 2f;
    private const float StuckMoveThreshold = 0.5f;
    private const int StuckStrikesBeforeWarp = 4; // ~8s of no movement
    private const float StateTimeout = 30f;
    private const int MaxStateRetries = 1; // first timeout retries, second bails
    private const float DespawnDistance = 30f;
    private const float DespawnHardCeiling = 60f;

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
        SpawnForLocation(location);
    }

    public static void SpawnForLocation(MogulLocation location)
    {
        if (!MogulNetwork.IsHost) return;

        // Hard cap: never let one building hold more than MaxConcurrentCustomers active
        // (non-leaving) customers. Beyond the slot count, late arrivals stack on the same
        // point and flock/circle. Slot capacity = 1 (counter) + MaxExterior (10) = 11.
        int active = 0;
        foreach (var kvp in _active)
            if (kvp.Value.LocationId == location.Id && kvp.Value.State != CustomerState.Leaving)
                active++;
        if (active >= MaxConcurrentCustomers)
        {
            MelonLogger.Msg($"[Mogul] {location.Id} at customer cap ({active}) — skipping spawn");
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

        var spawnPos = location.GetSpawnAnchor();
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
                StateEnteredAt = Time.time,
                LastSamplePos = npc.transform.position,
                LastSampleTime = Time.time,
                Preferences = CustomerDemand.GeneratePreferences(npc.gameObject.GetInstanceID()),
            };
            _active[npc] = entry;

            MelonLogger.Msg($"[Mogul] Spawning customer queueIdx={queueIdx} for {location.Id}");
            if (queueIdx == 0)
            {
                SendToCounter(entry, worldQueueAnchor);
            }
            else
            {
                var slot = QueueSlots.Get(queueIdx, location, worldQueueAnchor, navBuilder);
                MelonLogger.Msg($"[Mogul] Queue {queueIdx}: pos={slot.Position} exterior={slot.IsExterior}");
                SendToQueueSlot(entry, slot, () =>
                {
                    SetState(entry, CustomerState.WaitingInQueue);
                    MelonLogger.Msg($"[Mogul] Customer arrived at queue position {queueIdx} in {location.Id}");
                });
            }
        });
    }

    public static void Tick(Vector3 playerPos)
    {
        // Despawn — for Leaving customers: arrived at exit, OR far from player, OR
        // hard ceiling reached (last-resort guard against stuck NPCs).
        var toRemove = new List<NPC>();
        var toFault = new List<CustomerEntry>(); // pre-counter losses needing reflow + respawn
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            // Drop entries whose NPC was destroyed (e.g. fell out of world after a bad warp).
            if (e.Npc == null || e.Npc.gameObject == null)
            {
                toRemove.Add(kvp.Key);
                toFault.Add(e); // captured state — HandleFaultyLoss decides if respawn applies
                continue;
            }
            if (e.State != CustomerState.Leaving) continue;
            bool farFromPlayer = Vector3.Distance(e.Npc.transform.position, playerPos) > DespawnDistance;
            bool ceiling = Time.time >= e.DespawnDeadline;
            if (farFromPlayer || ceiling)
            {
                CustomerSpawner.Despawn(e.Npc);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var k in toRemove) _active.Remove(k);
        foreach (var e in toFault) HandleFaultyLoss(e, alreadyDespawned: true);

        DrainScheduledRespawns();

        // Drain queued advances now that some leavers may have cleared the door.
        if (_pendingAdvance.Count > 0)
        {
            var ready = new List<string>();
            foreach (var locId in _pendingAdvance)
                if (IsDoorClear(locId)) ready.Add(locId);
            foreach (var locId in ready)
            {
                _pendingAdvance.Remove(locId);
                AdvanceQueue(locId);
            }
        }

        // Health sweep: stuck detection + per-state timeouts. Skip stationary states
        // (WaitingInQueue / WaitingAtCounter) since not moving is correct there.
        var bailed = new List<CustomerEntry>(); // collected here, processed after the loop
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            bool stationary = e.State == CustomerState.WaitingInQueue
                           || e.State == CustomerState.WaitingAtCounter;

            if (!stationary && Time.time - e.LastSampleTime >= StuckSampleInterval)
            {
                var pos = e.Npc.transform.position;
                if (Vector3.Distance(e.LastSamplePos, pos) < StuckMoveThreshold)
                {
                    e.StuckStrikes++;
                    if (e.StuckStrikes >= StuckStrikesBeforeWarp)
                    {
                        if (NavMesh.SamplePosition(e.CurrentNavTarget, out var hit, 5f, NavMesh.AllAreas))
                        {
                            e.Npc.Movement?.Warp(hit.position);
                            MelonLogger.Msg($"[Mogul] Warped stuck customer to {hit.position}");
                        }
                        e.StuckStrikes = 0;
                    }
                }
                else
                {
                    e.StuckStrikes = 0;
                }
                e.LastSamplePos = pos;
                e.LastSampleTime = Time.time;
            }

            // Per-state timeouts apply only to MOVING states. Waiting states are correct
            // when they don't move — bailing them on a timer kicks the whole queue.
            if (!stationary && Time.time - e.StateEnteredAt > StateTimeout)
            {
                if (e.State == CustomerState.Leaving)
                {
                    // Leaving is a terminal state — if the NPC hasn't made it out in 30s,
                    // give up and despawn now rather than spamming retries.
                    MelonLogger.Msg($"[Mogul] Leaving customer stuck — despawning early");
                    e.DespawnDeadline = Time.time;
                }
                else if (++e.Retries > MaxStateRetries)
                {
                    MelonLogger.Warning($"[Mogul] Customer state {e.State} exceeded retries — bailing");
                    bailed.Add(e); // deferred — _active mutation can't happen mid-foreach
                }
                else
                {
                    MelonLogger.Msg($"[Mogul] Customer state {e.State} timed out — retry {e.Retries}");
                    RetryCurrentState(e);
                    e.StateEnteredAt = Time.time;
                }
            }
        }

        foreach (var e in bailed)
        {
            if (e.Npc != null) _active.Remove(e.Npc);
            HandleFaultyLoss(e, alreadyDespawned: false);
        }

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
                    CheckoutHandler.Open(location.Id, waiting.Npc, buildingRoot, waiting.Order ?? new List<SelectedProduct>());
            }
        }
    }

    private static void OnArrived(CustomerEntry entry)
    {
        if (entry.State != CustomerState.WalkingIn) return;
        entry.PendingArrival = null;

        if (!LocationSpawner.TryGetSpawnedBuilding(entry.LocationId, out var buildingRoot))
        {
            StartLeaving(entry);
            return;
        }

        var stock = StorageScanner.Scan(buildingRoot);
        var (order, rejection) = CustomerDemand.DecidePurchases(
            entry.Preferences, stock, entry.Npc.gameObject.GetInstanceID());

        if (order.Count == 0)
        {
            string message = rejection switch
            {
                RejectionReason.EmptyShelves => "Nothing here for me...",
                RejectionReason.TooExpensive => "Way too pricey...",
                _                            => "Nothing I want...",
            };
            entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Annoyed);
            entry.Npc.DialogueHandler?.WorldspaceRend?.ShowText(message, 3f);
            MelonLogger.Msg($"[Mogul] Customer rejected ({rejection}) in {entry.LocationId}");
            StartLeaving(entry);
            return;
        }

        entry.Order = order;
        SetState(entry, CustomerState.WaitingAtCounter);
        entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Greeting);

        float total = 0f;
        foreach (var p in order) total += p.Total;
        MelonLogger.Msg($"[Mogul] Customer ordered {order.Count} item(s) ~${total:F0} in {entry.LocationId}");
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
        if (entry.Npc == null) return;

        entry.PendingArrival = null;
        SetState(entry, CustomerState.Leaving);
        entry.DespawnDeadline = Time.time + DespawnHardCeiling;

        // RecallNPC handles door-aware exit routing (S1MAPI's NavigationBuilder owns the
        // building's interior nav graph and knows the door waypoint). We don't pass a
        // world target ourselves — that would bypass the door and lead NPCs through walls.
        entry.NavBuilder.RecallNPC(entry.Npc);

        // Stuck-recovery target while leaving: the cached spawn anchor (outside the room).
        // Used only by the warp fallback in Tick if the NPC fails to make progress.
        var location = PropertySystem.Find(entry.LocationId);
        if (location != null)
            entry.CurrentNavTarget = location.GetSpawnAnchor();

        if (entry.QueueIndex == 0)
            RequestAdvance(entry.LocationId);
    }

    // Schedules a queue advance, but waits until the door corridor is clear so the next
    // customer doesn't collide with the leaving one. Tick drains _pendingAdvance.
    private static void RequestAdvance(string locationId)
    {
        if (IsDoorClear(locationId))
            AdvanceQueue(locationId);
        else
            _pendingAdvance.Add(locationId);
    }

    private static bool IsDoorClear(string locationId)
    {
        var location = PropertySystem.Find(locationId);
        if (location == null) return true;
        var doorPoint = location.ComputeDoorExterior();
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if (e.State != CustomerState.Leaving) continue;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            if (Vector3.Distance(e.Npc.transform.position, doorPoint) < DoorClearRadius)
                return false;
        }
        return true;
    }

    private static void SendToQueueSlot(CustomerEntry entry, QueueSlot slot, Action onArrival)
    {
        entry.CurrentNavTarget = slot.Position;
        if (slot.IsExterior)
        {
            entry.Npc.Movement?.SetDestination(slot.Position);
            SetState(entry, CustomerState.WaitingInQueue);
            onArrival?.Invoke();
        }
        else
        {
            entry.NavBuilder.SendNPCToPosition(entry.Npc, slot.Position, onArrival);
        }
    }

    private static void SetState(CustomerEntry entry, CustomerState next)
    {
        if (entry.State == next) return;
        entry.State = next;
        entry.StateEnteredAt = Time.time;
        entry.Retries = 0;
        entry.StuckStrikes = 0;
    }

    // Handle a customer lost before the sale: clean up the entry, reflow the queue so
    // the empty slot doesn't block, and schedule a replacement if the loss happened in a
    // pre-counter state. Caller is responsible for `_active.Remove(entry.Npc)`.
    private static void HandleFaultyLoss(CustomerEntry entry, bool alreadyDespawned)
    {
        var locId = entry.LocationId;
        var oldIndex = entry.QueueIndex;
        bool replaceable = entry.State == CustomerState.WalkingIn
                        || entry.State == CustomerState.WaitingInQueue;

        if (!alreadyDespawned && entry.Npc != null && entry.Npc.gameObject != null)
            CustomerSpawner.Despawn(entry.Npc);

        ReflowAfterRemoval(locId, oldIndex);

        if (replaceable)
            ScheduleRespawn(locId);
    }

    // After a mid-queue loss, decrement the QueueIndex of every entry behind the gap and
    // re-issue their nav so they physically move up to the new slot.
    private static void ReflowAfterRemoval(string locationId, int removedIndex)
    {
        if (!SellDesk.TryGetQueueAnchor(locationId, out var anchor)) return;
        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        var toReflow = new List<CustomerEntry>();
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if (e.State != CustomerState.WalkingIn && e.State != CustomerState.WaitingInQueue) continue;
            if (e.QueueIndex <= removedIndex) continue;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            toReflow.Add(e);
        }
        toReflow.Sort((a, b) => a.QueueIndex.CompareTo(b.QueueIndex));

        foreach (var e in toReflow)
        {
            e.QueueIndex--;
            try
            {
                if (e.QueueIndex == 0) SendToCounter(e, anchor);
                else
                {
                    var slot = QueueSlots.Get(e.QueueIndex, location, anchor, e.NavBuilder);
                    SendToQueueSlot(e, slot, null);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Mogul] Reflow failed for queueIdx {e.QueueIndex}: {ex.Message}");
            }
        }
    }

    private static void ScheduleRespawn(string locationId)
    {
        _respawnsInFlight.TryGetValue(locationId, out int n);
        if (n >= MaxConcurrentRespawns)
        {
            MelonLogger.Msg($"[Mogul] Respawn cap reached for {locationId} — skipping");
            return;
        }
        _respawnsInFlight[locationId] = n + 1;
        _scheduledRespawns.Add((locationId, Time.time + RespawnDelay));
        MelonLogger.Msg($"[Mogul] Scheduled replacement for {locationId} (in {RespawnDelay}s)");
    }

    private static void DrainScheduledRespawns()
    {
        if (_scheduledRespawns.Count == 0) return;
        for (int i = _scheduledRespawns.Count - 1; i >= 0; i--)
        {
            var item = _scheduledRespawns[i];
            if (Time.time < item.deadline) continue;
            _scheduledRespawns.RemoveAt(i);
            if (_respawnsInFlight.TryGetValue(item.locationId, out int n) && n > 0)
                _respawnsInFlight[item.locationId] = n - 1;
            var location = PropertySystem.Find(item.locationId);
            if (location == null) continue;
            SpawnForLocation(location);
        }
    }

    private static void RetryCurrentState(CustomerEntry entry)
    {
        // Re-issue the navigation command for the current state. State stays the same;
        // StateEnteredAt is reset by the caller so the timeout window restarts.
        // Only WalkingIn and Leaving are reachable here (stationary states skip the timeout).
        if (entry.State == CustomerState.WalkingIn
            && SellDesk.TryGetQueueAnchor(entry.LocationId, out var anchor))
        {
            SendToCounter(entry, anchor);
        }
        // Leaving timed out: the despawn ceiling (60s) will clean it up.
    }

    private static void SendToCounter(CustomerEntry entry, Vector3 worldQueueAnchor)
    {
        SetState(entry, CustomerState.WalkingIn);
        entry.CurrentNavTarget = worldQueueAnchor;
        if (LocationGeometry.TryGetCounterWorldPos(entry.LocationId, out var counterWorld))
        {
            entry.PendingWorldTarget = counterWorld;
            entry.PendingArrival = () => OnArrived(entry);
        }
        entry.NavBuilder.SendNPCToPosition(entry.Npc, worldQueueAnchor, onArrival: () => OnArrived(entry));
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
            // Skip dead/destroyed NPCs — passing them to S1MAPI's NavigationBuilder triggers
            // a reflection-based MemberAccessor crash that aborts the entire advance loop.
            if (e.Npc == null || e.Npc.gameObject == null) continue;
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
            // Isolate each nav call: a single bad NPC reference should not cancel the rest
            // of the queue's advancement. The catch lets the loop continue.
            try
            {
                if (e.QueueIndex == 0)
                {
                    SendToCounter(e, worldQueueAnchor);
                    MelonLogger.Msg($"[Mogul] Advancing next customer to counter in {locationId}");
                }
                else
                {
                    var slot = QueueSlots.Get(e.QueueIndex, location, worldQueueAnchor, e.NavBuilder);
                    SendToQueueSlot(e, slot, null);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Mogul] AdvanceQueue: skipping customer (queueIdx now {e.QueueIndex}): {ex.Message}");
            }
        }
    }

}
