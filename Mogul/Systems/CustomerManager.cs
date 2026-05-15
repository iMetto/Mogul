using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Economy;
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
    public enum CustomerState { WalkingIn, Browsing, JoiningQueue, WaitingInQueue, WaitingAtCounter, BeingServed, Leaving }

    private class CustomerEntry
    {
        public NPC Npc;
        public Customer CustomerComp;
        public string LocationId;
        public NavigationBuilder NavBuilder;
        public CustomerState State;
        public int QueueIndex;
        public Vector3 PendingWorldTarget;
        public Action PendingArrival;

        // Demand — generated at spawn, resolved to an order at arrival
        public CustomerPreferences Preferences;
        public List<SelectedProduct> Order;

        // Browsing/checkout pacing.
        public List<Vector3> BrowseTargets;
        public List<Vector3> BrowseLookTargets;
        public int BrowseIndex;
        public float BrowsePauseUntil;
        public float ServiceCompleteAt;

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

    // Per-location cap on simultaneous active (non-leaving) customers. Browsing customers
    // share this budget with the checkout line so the store feels busier without flooding nav.
    private const int MaxConcurrentCustomers = 12;

    private const float StuckSampleInterval = 2f;
    private const float StuckMoveThreshold = 0.5f;
    private const int StuckStrikesBeforeWarp = 4; // ~8s of no movement
    private const float StateTimeout = 30f;
    private const int MaxStateRetries = 1; // first timeout retries, second bails
    private const float DespawnDistance = 30f;
    private const float DespawnHardCeiling = 60f;
    private const float BrowsePauseMin = 4f;
    private const float BrowsePauseMax = 6f;
    private const float ServiceDelayMin = 3f;
    private const float ServiceDelayMax = 4.5f;
    private const int MaxBrowseTargets = 2;

    public static void Initialize()
    {
        CheckoutHandler.OnClosed += OnCheckoutClosed;
    }

    public static void ClearQueueCache() => QueueSlots.ClearCache();

    public static void EvictFromLocation(string locationId)
    {
        var toRemove = new List<NPC>();
        foreach (var kvp in _active)
        {
            if (kvp.Value.LocationId != locationId) continue;
            if (kvp.Key != null && kvp.Key.gameObject != null)
                CustomerSpawner.Despawn(kvp.Key);
            toRemove.Add(kvp.Key);
        }
        foreach (var k in toRemove) _active.Remove(k);
        _pendingAdvance.Remove(locationId);
        _respawnsInFlight.Remove(locationId);
        _scheduledRespawns.RemoveAll(t => t.locationId == locationId);
    }

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
            var customerComp = npc.gameObject.GetComponent<Customer>();
            var entry = new CustomerEntry
            {
                Npc = npc,
                CustomerComp = customerComp,
                LocationId = location.Id,
                NavBuilder = navBuilder,
                State = CustomerState.WalkingIn,
                QueueIndex = -1,
                StateEnteredAt = Time.time,
                LastSamplePos = npc.transform.position,
                LastSampleTime = Time.time,
                Preferences = CustomerDemand.GeneratePreferences(npc.gameObject.GetInstanceID()),
            };
            _active[npc] = entry;

            StartBrowsingOrQueue(entry, location);
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
                if (e.State != CustomerState.Leaving)
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
        TickBrowsing();
        TickCashierService();

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
                           || e.State == CustomerState.WaitingAtCounter
                           || e.State == CustomerState.BeingServed
                           || (e.State == CustomerState.Browsing && e.BrowsePauseUntil > 0f);

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
                    e.DespawnDeadline = Time.time;
                }
                else if (++e.Retries > MaxStateRetries)
                {
                    MelonLogger.Warning($"[Mogul] Customer state {e.State} exceeded retries — bailing");
                    bailed.Add(e); // deferred — _active mutation can't happen mid-foreach
                }
                else
                {
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

        // Proximity arrival detection for SetDestination and callback misses.
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.PendingArrival != null)
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
                    CheckoutHandler.Open(location.Id, waiting.Npc, buildingRoot, waiting.Order ?? []);
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
        entry.Npc.Movement?.Stop();
        FaceCounter(entry);
        entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Greeting);

        float total = 0f;
        foreach (var p in order) total += p.Total;
        MelonLogger.Msg($"[Mogul] Customer ordered {order.Count} item(s) ~${total:F0} in {entry.LocationId}");

        if (EmployeeSystem.HasRole(entry.LocationId, EmployeeRole.Cashier))
        {
            entry.Npc.DialogueHandler?.WorldspaceRend?.ShowText("Cashier's got it.", 2f);
            SetState(entry, CustomerState.BeingServed);
            entry.ServiceCompleteAt = Time.time + UnityEngine.Random.Range(ServiceDelayMin, ServiceDelayMax);
        }
    }

    private static void StartBrowsingOrQueue(CustomerEntry entry, MogulLocation location)
    {
        if (TryBuildBrowseTargets(location, entry.NavBuilder, entry.Npc.gameObject.GetInstanceID(),
                out var browseTargets, out var lookTargets))
        {
            entry.BrowseTargets = browseTargets;
            entry.BrowseLookTargets = lookTargets;
            entry.BrowseIndex = 0;
            entry.BrowsePauseUntil = 0f;
            SendToBrowseTarget(entry, 0);
            return;
        }

        JoinQueue(entry);
    }

    private static bool TryBuildBrowseTargets(
        MogulLocation location,
        NavigationBuilder nav,
        int seed,
        out List<Vector3> standTargets,
        out List<Vector3> lookTargets)
    {
        standTargets = new List<Vector3>();
        lookTargets = new List<Vector3>();
        if (nav == null || location == null) return false;

        if (LocationSpawner.TryGetRackObjects(location.Id, out var racks) && racks != null)
        {
            for (int i = 0; i < racks.Count; i++)
            {
                var rack = racks[i];
                if (rack == null) continue;
                var look = rack.transform.localPosition;
                var stand = look + rack.transform.localRotation * Vector3.forward * 0.85f;
                stand.y = 0f;
                AddBrowseTarget(nav, stand, look, standTargets, lookTargets);
            }
        }

        if (EmployeeSystem.HasRole(location.Id, EmployeeRole.Budtender))
        {
            var look = GetGrowTentLocalPosition(location);
            var room = BuildingPreview.GetEffectiveRoomSize(location);
            float zOffset = look.z <= room.z * 0.5f ? 1.3f : -1.3f;
            var stand = new Vector3(
                Mathf.Clamp(look.x, 0.5f, Mathf.Max(0.5f, room.x - 0.5f)),
                0f,
                Mathf.Clamp(look.z + zOffset, 0.5f, Mathf.Max(0.5f, room.z - 0.5f)));
            AddBrowseTarget(nav, stand, look, standTargets, lookTargets);
        }

        if (standTargets.Count == 0) return false;
        ShuffleBrowseTargets(standTargets, lookTargets, seed);
        int keep = Mathf.Min(MaxBrowseTargets, standTargets.Count);
        if (standTargets.Count > keep)
        {
            standTargets.RemoveRange(keep, standTargets.Count - keep);
            lookTargets.RemoveRange(keep, lookTargets.Count - keep);
        }
        return true;
    }

    private static void AddBrowseTarget(
        NavigationBuilder nav,
        Vector3 stand,
        Vector3 look,
        List<Vector3> standTargets,
        List<Vector3> lookTargets)
    {
        if (!nav.IsWalkable(stand))
            stand = nav.NearestWalkableCell(stand);
        if (!nav.IsWalkable(stand)) return;
        standTargets.Add(stand);
        lookTargets.Add(look);
    }

    private static void ShuffleBrowseTargets(List<Vector3> stands, List<Vector3> looks, int seed)
    {
        var rng = new System.Random(seed ^ 0x5eed);
        for (int i = stands.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (stands[i], stands[j]) = (stands[j], stands[i]);
            (looks[i], looks[j]) = (looks[j], looks[i]);
        }
    }

    private static Vector3 GetGrowTentLocalPosition(MogulLocation location)
    {
        return MogulPlacementSystem.TryGetPlacement(location.Id, MogulPlacementSystem.GrowTent, out var pos, out _)
            ? pos
            : EmployeeSystem.GetDefaultGrowTentLocalPosition(location);
    }

    private static void SendToBrowseTarget(CustomerEntry entry, int index)
    {
        if (entry.BrowseTargets == null || index < 0 || index >= entry.BrowseTargets.Count)
        {
            JoinQueue(entry);
            return;
        }

        entry.BrowseIndex = index;
        entry.BrowsePauseUntil = 0f;
        SetState(entry, CustomerState.Browsing);
        entry.CurrentNavTarget = entry.BrowseTargets[index];
        entry.PendingWorldTarget = entry.Npc.transform.position;
        entry.PendingArrival = null;
        entry.NavBuilder.SendNPCToPosition(entry.Npc, entry.BrowseTargets[index], onArrival: () => OnBrowseArrived(entry));
    }

    private static void OnBrowseArrived(CustomerEntry entry)
    {
        if (entry.State != CustomerState.Browsing) return;
        entry.Npc.Movement?.Stop();
        if (entry.BrowseLookTargets != null && entry.BrowseIndex < entry.BrowseLookTargets.Count
            && LocationSpawner.TryGetSpawnedBuilding(entry.LocationId, out var buildingRoot))
        {
            FaceWorldTarget(entry.Npc, buildingRoot.transform.TransformPoint(entry.BrowseLookTargets[entry.BrowseIndex]));
        }

        entry.BrowsePauseUntil = Time.time + UnityEngine.Random.Range(BrowsePauseMin, BrowsePauseMax);
        if ((entry.Npc.gameObject.GetInstanceID() + entry.BrowseIndex) % 3 == 0)
            entry.Npc.VoiceOverEmitter?.Play(EVOLineType.Think);
    }

    private static void TickBrowsing()
    {
        foreach (var kvp in _active)
        {
            var entry = kvp.Value;
            if (entry.State != CustomerState.Browsing) continue;
            if (entry.BrowsePauseUntil <= 0f || Time.time < entry.BrowsePauseUntil) continue;

            entry.BrowsePauseUntil = 0f;
            int next = entry.BrowseIndex + 1;
            if (entry.BrowseTargets != null && next < entry.BrowseTargets.Count)
                SendToBrowseTarget(entry, next);
            else
                JoinQueue(entry);
        }
    }

    private static void TickCashierService()
    {
        var ready = new List<CustomerEntry>();
        foreach (var kvp in _active)
        {
            var entry = kvp.Value;
            if (entry.State == CustomerState.BeingServed && Time.time >= entry.ServiceCompleteAt)
                ready.Add(entry);
        }

        foreach (var entry in ready)
            CompleteCashierService(entry);
    }

    private static void CompleteCashierService(CustomerEntry entry)
    {
        if (entry.State != CustomerState.BeingServed) return;
        if (!LocationSpawner.TryGetSpawnedBuilding(entry.LocationId, out var buildingRoot))
        {
            StartLeaving(entry);
            return;
        }

        var result = CheckoutHandler.FulfillOrderDirect(entry.LocationId, entry.Npc, buildingRoot, entry.Order);
        if (result != CheckoutResult.Dismissed)
            StartLeaving(entry);
    }

    private static void OnCheckoutClosed(string locationId, CheckoutResult result)
    {
        // Dismissed = player cancelled UI, NPC stays waiting — do nothing
        if (result == CheckoutResult.Dismissed) return;

        // Sold, denied, or no stock = NPC leaves (voice/text already handled by CheckoutHandler)
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

    private static void JoinQueue(CustomerEntry entry)
    {
        if (!SellDesk.TryGetQueueAnchor(entry.LocationId, out var worldQueueAnchor))
        {
            StartLeaving(entry);
            return;
        }
        var location = PropertySystem.Find(entry.LocationId);
        if (location == null)
        {
            StartLeaving(entry);
            return;
        }

        int queueIdx = 0;
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e == entry) continue;
            if (e.LocationId != entry.LocationId || e.State == CustomerState.Leaving) continue;
            if (e.QueueIndex >= 0) queueIdx++;
        }

        if (queueIdx >= QueueSlots.Capacity(location))
        {
            entry.Npc.DialogueHandler?.WorldspaceRend?.ShowText("Too crowded...", 2f);
            StartLeaving(entry);
            return;
        }

        entry.QueueIndex = queueIdx;
        if (queueIdx == 0)
        {
            SendToCounter(entry, worldQueueAnchor);
            return;
        }

        var slot = QueueSlots.Get(queueIdx, location, worldQueueAnchor, entry.NavBuilder);
        SendToQueueSlot(entry, slot, () => ArriveQueueSlot(entry));
    }

    private static void ArriveQueueSlot(CustomerEntry entry)
    {
        if (entry.State != CustomerState.JoiningQueue) return;
        entry.PendingArrival = null;
        entry.Npc.Movement?.Stop();
        SetState(entry, CustomerState.WaitingInQueue);
        FaceQueueTarget(entry);
    }

    private static void SendToQueueSlot(CustomerEntry entry, QueueSlot slot, Action onArrival)
    {
        entry.CurrentNavTarget = slot.Position;
        SetState(entry, CustomerState.JoiningQueue);
        if (slot.IsExterior)
        {
            entry.PendingWorldTarget = slot.Position;
            entry.PendingArrival = onArrival;
            entry.Npc.Movement?.SetDestination(slot.Position);
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
                        || entry.State == CustomerState.Browsing
                        || entry.State == CustomerState.JoiningQueue
                        || entry.State == CustomerState.WaitingInQueue;

        if (!alreadyDespawned && entry.Npc != null && entry.Npc.gameObject != null)
            CustomerSpawner.Despawn(entry.Npc);

        if (oldIndex >= 0)
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
            if (e.State != CustomerState.WalkingIn
                && e.State != CustomerState.JoiningQueue
                && e.State != CustomerState.WaitingInQueue) continue;
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
                    SendToQueueSlot(e, slot, () => ArriveQueueSlot(e));
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
            return;
        }
        _respawnsInFlight[locationId] = n + 1;
        _scheduledRespawns.Add((locationId, Time.time + RespawnDelay));
    }

    private static void DrainScheduledRespawns()
    {
        if (_scheduledRespawns.Count == 0) return;
        for (int i = _scheduledRespawns.Count - 1; i >= 0; i--)
        {
            var (locationId, deadline) = _scheduledRespawns[i];
            if (Time.time < deadline) continue;
            _scheduledRespawns.RemoveAt(i);
            if (_respawnsInFlight.TryGetValue(locationId, out int n) && n > 0)
                _respawnsInFlight[locationId] = n - 1;
            var location = PropertySystem.Find(locationId);
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
        else if (entry.State == CustomerState.JoiningQueue)
        {
            ResendQueuePosition(entry);
        }
        else if (entry.State == CustomerState.Browsing)
        {
            SendToBrowseTarget(entry, entry.BrowseIndex);
        }
        // Leaving timed out: the despawn ceiling (60s) will clean it up.
    }

    private static void ResendQueuePosition(CustomerEntry entry)
    {
        if (!SellDesk.TryGetQueueAnchor(entry.LocationId, out var anchor)) return;
        var location = PropertySystem.Find(entry.LocationId);
        if (location == null) return;
        if (entry.QueueIndex <= 0)
            SendToCounter(entry, anchor);
        else
        {
            var slot = QueueSlots.Get(entry.QueueIndex, location, anchor, entry.NavBuilder);
            SendToQueueSlot(entry, slot, () => ArriveQueueSlot(entry));
        }
    }

    private static void FaceCounter(CustomerEntry entry)
    {
        if (SellDesk.TryGetCounterObject(entry.LocationId, out var counter) && counter != null)
            FaceWorldTarget(entry.Npc, counter.transform.position);
    }

    private static void FaceQueueTarget(CustomerEntry entry)
    {
        CustomerEntry ahead = null;
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != entry.LocationId) continue;
            if (e.QueueIndex != entry.QueueIndex - 1) continue;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            ahead = e;
            break;
        }

        if (ahead != null)
            FaceWorldTarget(entry.Npc, ahead.Npc.transform.position);
        else
            FaceCounter(entry);
    }

    private static void FaceWorldTarget(NPC npc, Vector3 worldTarget)
    {
        if (npc == null || npc.gameObject == null) return;
        var dir = worldTarget - npc.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        npc.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
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
            if (e.LocationId == locationId
                && (e.State == CustomerState.WaitingInQueue || e.State == CustomerState.JoiningQueue))
                waiting.Add(e);
        }
        if (waiting.Count == 0) return;

        foreach (var kvp in _active)
            if (kvp.Value.LocationId == locationId
                && (kvp.Value.State == CustomerState.WaitingAtCounter || kvp.Value.State == CustomerState.BeingServed))
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
                }
                else
                {
                    var slot = QueueSlots.Get(e.QueueIndex, location, worldQueueAnchor, e.NavBuilder);
                    SendToQueueSlot(e, slot, () => ArriveQueueSlot(e));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Mogul] AdvanceQueue: skipping customer (queueIdx now {e.QueueIndex}): {ex.Message}");
            }
        }
    }

}
