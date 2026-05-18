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
        public int BrowseSlot;
        public int BrowseSpotId;
        public float BrowsePauseUntil;
        public bool ReadyToOrder;
        public bool BrowsingFromQueue;
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
    private static float _nextFlowMaintenanceAt;

    private readonly struct BrowseSpot
    {
        public readonly Vector3 Stand;
        public readonly Vector3 Look;

        public BrowseSpot(Vector3 stand, Vector3 look)
        {
            Stand = stand;
            Look = look;
        }
    }

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
    private const float FlowMaintenanceInterval = 2f;
    private const int MaxBrowseTargets = 1;
    private const int RackBrowseSpotCount = 3;
    private const int GrowTentBrowseSpotCount = 2;
    private const int MaxSmallStoreIndoorCustomers = 1;
    private const int MaxMediumStoreIndoorCustomers = 2;
    private const int MaxLargeStoreIndoorCustomers = 3;
    private const string CustomerInteractableName = "Mogul_OrderInteractable";

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
                NpcSpawner.Despawn(kvp.Key);
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
        NpcSpawner.SpawnTestNPC(spawnPos, npc =>
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
                BrowseSlot = -1,
                BrowseSpotId = -1,
                StateEnteredAt = Time.time,
                LastSamplePos = npc.transform.position,
                LastSampleTime = Time.time,
                Preferences = CustomerDemand.GeneratePreferences(npc.gameObject.GetInstanceID()),
            };
            _active[npc] = entry;

            StartBrowsingOrQueue(entry, location, promotedFromQueue: false);
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
                NpcSpawner.Despawn(e.Npc);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var k in toRemove) _active.Remove(k);
        foreach (var e in toFault) HandleFaultyLoss(e, alreadyDespawned: true);

        DrainScheduledRespawns();
        TickBrowsing();
        TickCashierService();
        DrainPendingArrivals();
        MaintainCustomerFlow();

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
                           || (e.State == CustomerState.Browsing && (e.BrowsePauseUntil > 0f || e.ReadyToOrder));

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
                else if (e.State == CustomerState.Browsing)
                {
                    MelonLogger.Warning("[Mogul] Customer browse route timed out — marking ready to order");
                    CompleteBrowse(entry: e);
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

        DrainPendingArrivals();

        CashRegister.Tick();

        // Don't handle counter interaction while checkout UI is open
        if (CheckoutHandler.IsOpen) return;

        var hovered = Singleton<InteractionManager>.Instance?.HoveredInteractableObject;
        bool qDown = Input.GetKeyDown(KeyCode.Q) && !GameInput.IsTyping;

        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            if (SellDesk.TryGetCounterInteractable(location.Id, out var counterInteractable)
                && counterInteractable._interactionState != InteractableObject.EInteractableState.Disabled)
                counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);

            CustomerEntry waiting = null;
            foreach (var kvp in _active)
                if (kvp.Value.State == CustomerState.WaitingAtCounter && kvp.Value.LocationId == location.Id)
                { waiting = kvp.Value; break; }

            SetCustomerInteractables(location.Id, waiting);
            if (waiting == null)
                continue;

            var customerInteractable = GetOrCreateCustomerInteractable(waiting.Npc);
            if (hovered == customerInteractable && qDown)
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
            StartLeaving(entry, requestAdvance: false);
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

    private static void SetCustomerInteractables(string locationId, CustomerEntry waiting)
    {
        foreach (var kvp in _active)
        {
            var entry = kvp.Value;
            if (entry.LocationId != locationId) continue;
            if (entry.Npc == null || entry.Npc.gameObject == null) continue;
            DisableVanillaCustomerInteractable(entry.Npc);

            var interactable = entry == waiting
                ? GetOrCreateCustomerInteractable(entry.Npc)
                : TryGetCustomerInteractable(entry.Npc);
            if (interactable == null) continue;

            if (entry == waiting)
            {
                interactable.SetMessage("[Q] Take order");
                if (interactable._interactionState != InteractableObject.EInteractableState.Label)
                    interactable.SetInteractableState(InteractableObject.EInteractableState.Label);
            }
            else if (interactable._interactionState != InteractableObject.EInteractableState.Disabled)
            {
                interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            }
        }
    }

    private static InteractableObject GetOrCreateCustomerInteractable(NPC npc)
    {
        if (npc == null || npc.gameObject == null) return null;

        DisableVanillaCustomerInteractable(npc);
        var interactable = TryGetCustomerInteractable(npc);
        if (interactable == null)
        {
            var obj = new GameObject(CustomerInteractableName);
            obj.transform.SetParent(npc.transform, false);
            obj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            obj.layer = npc.gameObject.layer;
            var col = obj.AddComponent<SphereCollider>();
            col.radius = 0.65f;
            col.isTrigger = true;
            interactable = obj.AddComponent<InteractableObject>();
            interactable.displayLocationCollider = col;
            interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
        }

        interactable.MaxInteractionRange = 3f;
        if (interactable.displayLocationCollider == null)
        {
            var col = interactable.gameObject.GetComponent<Collider>()
                      ?? interactable.gameObject.GetComponentInChildren<Collider>(true);
            if (col != null) interactable.displayLocationCollider = col;
        }
        return interactable;
    }

    private static InteractableObject TryGetCustomerInteractable(NPC npc)
    {
        if (npc == null || npc.gameObject == null) return null;
        var child = npc.transform.Find(CustomerInteractableName);
        return child != null ? child.GetComponent<InteractableObject>() : null;
    }

    private static void DisableCustomerInteractable(NPC npc)
    {
        var interactable = TryGetCustomerInteractable(npc);
        if (interactable != null
            && interactable._interactionState != InteractableObject.EInteractableState.Disabled)
            interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
        DisableVanillaCustomerInteractable(npc);
    }

    private static void DisableVanillaCustomerInteractable(NPC npc)
    {
        var vanilla = npc?.intObj;
        if (vanilla != null && vanilla._interactionState != InteractableObject.EInteractableState.Disabled)
            vanilla.SetInteractableState(InteractableObject.EInteractableState.Disabled);
    }

    private static void DrainPendingArrivals()
    {
        var arrivals = new List<(CustomerEntry entry, Action callback)>();
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            if (e.PendingArrival == null) continue;
            if (Vector3.Distance(e.Npc.transform.position, e.PendingWorldTarget) >= 1.5f) continue;

            var cb = e.PendingArrival;
            e.PendingArrival = null;
            arrivals.Add((e, cb));
        }

        foreach (var arrival in arrivals)
        {
            arrival.entry.Npc?.Movement?.Stop();
            arrival.callback.Invoke();
        }
    }

    private static bool HasSellableStock(string locationId)
    {
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null)
            return false;

        var stock = StorageScanner.Scan(buildingRoot);
        for (int i = 0; i < stock.Count; i++)
            if (stock[i] != null && stock[i].TotalPackages > 0)
                return true;
        return false;
    }

    private static void DismissCustomerNoStock(CustomerEntry entry, bool advance = true)
    {
        if (entry == null) return;
        string locationId = entry.LocationId;
        int oldIndex = entry.QueueIndex;

        if (entry.Npc != null && entry.Npc.gameObject != null)
        {
            entry.Npc.DialogueHandler?.WorldspaceRend?.ShowText("Nothing here...", 2f);
            StartLeaving(entry);
        }

        if (oldIndex > 0)
            ReflowAfterRemoval(locationId, oldIndex);
        if (advance)
            AdvanceQueue(locationId);
    }

    private static void DismissPreCounterCustomersNoStock(string locationId)
    {
        var dismiss = new List<CustomerEntry>();
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if (e.State == CustomerState.WaitingAtCounter || e.State == CustomerState.BeingServed) continue;
            if (e.State == CustomerState.Leaving) continue;
            dismiss.Add(e);
        }

        foreach (var entry in dismiss)
            DismissCustomerNoStock(entry, advance: false);
    }

    private static void StartBrowsingOrQueue(CustomerEntry entry, MogulLocation location, bool promotedFromQueue)
    {
        if (!HasSellableStock(location.Id))
        {
            DismissCustomerNoStock(entry);
            return;
        }

        int browseSlot = FindOpenBrowseSlot(location.Id);
        if (browseSlot >= 0
            && CanEnterStoreForBrowse(location)
            && TryBuildBrowseTargets(location, entry.NavBuilder, entry.Npc.gameObject.GetInstanceID(),
                out int browseSpotId, out var browseTargets, out var lookTargets))
        {
            entry.BrowseTargets = browseTargets;
            entry.BrowseLookTargets = lookTargets;
            entry.BrowseIndex = 0;
            entry.BrowseSlot = browseSlot;
            entry.BrowseSpotId = browseSpotId;
            entry.BrowsePauseUntil = 0f;
            entry.ReadyToOrder = false;
            entry.BrowsingFromQueue = promotedFromQueue;
            if (TrySendToBrowseTarget(entry, 0))
                return;
            entry.BrowseSlot = -1;
            entry.BrowseSpotId = -1;
        }

        entry.BrowsingFromQueue = false;
        JoinQueue(entry);
    }

    private static int FindOpenBrowseSlot(string locationId)
    {
        for (int slot = 0; slot < MaxLargeStoreIndoorCustomers; slot++)
        {
            bool occupied = false;
            foreach (var kvp in _active)
            {
                var e = kvp.Value;
                if (e.LocationId != locationId) continue;
                if (e.State == CustomerState.Leaving) continue;
                if (e.BrowseSlot == slot)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied) return slot;
        }
        return -1;
    }

    private static bool CanEnterStoreForBrowse(MogulLocation location)
    {
        if (location == null) return false;
        return CountIndoorCustomers(location.Id) < GetMaxIndoorCustomers(location);
    }

    private static int CountIndoorCustomers(string locationId)
    {
        int count = 0;
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if (e.Npc == null || e.Npc.gameObject == null) continue;
            if (e.State == CustomerState.Browsing
                || e.State == CustomerState.WalkingIn
                || e.State == CustomerState.WaitingAtCounter
                || e.State == CustomerState.BeingServed)
                count++;
        }
        return count;
    }

    private static int GetMaxIndoorCustomers(MogulLocation location)
    {
        return MaxLargeStoreIndoorCustomers;
    }

    private static bool TryBuildBrowseTargets(
        MogulLocation location,
        NavigationBuilder nav,
        int seed,
        out int browseSpotId,
        out List<Vector3> standTargets,
        out List<Vector3> lookTargets)
    {
        browseSpotId = -1;
        standTargets = new List<Vector3>();
        lookTargets = new List<Vector3>();
        if (nav == null || location == null) return false;

        var spots = BuildBrowseSpots(location, nav);
        if (spots.Count == 0) return false;

        var candidates = new List<int>();
        for (int i = 0; i < spots.Count; i++)
        {
            bool occupied = false;
            foreach (var kvp in _active)
            {
                var e = kvp.Value;
                if (e.LocationId != location.Id) continue;
                if (e.State == CustomerState.Leaving) continue;
                if (e.BrowseSpotId == i)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied) candidates.Add(i);
        }
        if (candidates.Count == 0) return false;

        int pick = Mathf.Abs(seed) % candidates.Count;
        browseSpotId = candidates[pick];
        var spot = spots[browseSpotId];
        standTargets.Add(spot.Stand);
        lookTargets.Add(spot.Look);
        return true;
    }

    private static List<BrowseSpot> BuildBrowseSpots(MogulLocation location, NavigationBuilder nav)
    {
        var spots = new List<BrowseSpot>(RackBrowseSpotCount + GrowTentBrowseSpotCount);

        if (LocationSpawner.TryGetRackObjects(location.Id, out var racks) && racks != null)
        {
            float[] lateralOffsets = { 0f, -0.85f, 0.85f, -1.7f, 1.7f };
            for (int i = 0; i < racks.Count && spots.Count < RackBrowseSpotCount; i++)
            {
                var rack = racks[i];
                if (rack == null) continue;
                var look = rack.transform.localPosition;
                var forward = rack.transform.localRotation * Vector3.forward;
                var right = rack.transform.localRotation * Vector3.right;
                for (int offsetIndex = 0; offsetIndex < lateralOffsets.Length && spots.Count < RackBrowseSpotCount; offsetIndex++)
                {
                    var stand = look + forward * 1.15f + right * lateralOffsets[offsetIndex];
                    stand.y = 0f;
                    AddBrowseSpot(nav, stand, look, spots);
                }
            }
        }

        if (EmployeeSystem.HasRole(location.Id, EmployeeRole.Budtender))
        {
            var look = GetGrowTentLocalPosition(location);
            var room = BuildingPreview.GetEffectiveRoomSize(location);
            float zOffset = look.z <= room.z * 0.5f ? 1.3f : -1.3f;
            float[] xOffsets = { -0.7f, 0.7f };
            for (int i = 0; i < xOffsets.Length && CountGrowTentSpots(spots) < GrowTentBrowseSpotCount; i++)
            {
                var stand = new Vector3(
                    Mathf.Clamp(look.x + xOffsets[i], 0.5f, Mathf.Max(0.5f, room.x - 0.5f)),
                    0f,
                    Mathf.Clamp(look.z + zOffset, 0.5f, Mathf.Max(0.5f, room.z - 0.5f)));
                AddBrowseSpot(nav, stand, look, spots);
            }
        }

        AddFallbackBrowseSpots(location, nav, spots);

        return spots;
    }

    private static void AddFallbackBrowseSpots(MogulLocation location, NavigationBuilder nav, List<BrowseSpot> spots)
    {
        if (spots.Count >= MaxLargeStoreIndoorCustomers) return;

        var room = BuildingPreview.GetEffectiveRoomSize(location);
        var look = new Vector3(room.x * 0.5f, 0f, room.z * 0.5f);
        var fallback = new[]
        {
            new Vector3(room.x * 0.35f, 0f, room.z * 0.35f),
            new Vector3(room.x * 0.65f, 0f, room.z * 0.35f),
            new Vector3(room.x * 0.5f, 0f, room.z * 0.65f),
            new Vector3(room.x * 0.35f, 0f, room.z * 0.65f),
            new Vector3(room.x * 0.65f, 0f, room.z * 0.65f),
        };

        for (int i = 0; i < fallback.Length && spots.Count < MaxLargeStoreIndoorCustomers; i++)
            AddBrowseSpot(nav, fallback[i], look, spots);
    }

    private static int CountGrowTentSpots(List<BrowseSpot> spots)
    {
        return Mathf.Max(0, spots.Count - RackBrowseSpotCount);
    }

    private static void AddBrowseSpot(
        NavigationBuilder nav,
        Vector3 stand,
        Vector3 look,
        List<BrowseSpot> spots)
    {
        if (!nav.IsWalkable(stand))
            stand = nav.NearestWalkableCell(stand);
        if (!nav.IsWalkable(stand)) return;
        for (int i = 0; i < spots.Count; i++)
        {
            if ((spots[i].Stand - stand).sqrMagnitude < 0.8f * 0.8f)
                return;
        }
        spots.Add(new BrowseSpot(stand, look));
    }

    private static Vector3 GetGrowTentLocalPosition(MogulLocation location)
    {
        return MogulPlacementSystem.TryGetPlacement(location.Id, MogulPlacementSystem.GrowTent, out var pos, out _)
            ? pos
            : EmployeeSystem.GetDefaultGrowTentLocalPosition(location);
    }

    private static void SendToBrowseTarget(CustomerEntry entry, int index)
    {
        if (!TrySendToBrowseTarget(entry, index))
            JoinQueue(entry);
    }

    private static bool TrySendToBrowseTarget(CustomerEntry entry, int index)
    {
        if (entry.BrowseTargets == null || index < 0 || index >= entry.BrowseTargets.Count)
        {
            return false;
        }

        try
        {
            entry.BrowseIndex = index;
            entry.BrowsePauseUntil = 0f;
            entry.CurrentNavTarget = entry.BrowseTargets[index];
            entry.PendingWorldTarget = entry.NavBuilder.LocalToWorld(entry.BrowseTargets[index]);
            entry.PendingArrival = () => OnBrowseArrived(entry);
            entry.NavBuilder.SendNPCToPosition(GetNavComponent(entry.Npc), entry.BrowseTargets[index], onArrival: () => OnBrowseArrived(entry));
            SetState(entry, CustomerState.Browsing);
            return true;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[Mogul] Browse route failed for {entry.LocationId}: {ex.Message}");
            return false;
        }
    }

    private static void OnBrowseArrived(CustomerEntry entry)
    {
        if (entry.State != CustomerState.Browsing) return;
        entry.PendingArrival = null;
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
            {
                CompleteBrowse(entry);
            }
        }
    }

    private static void CompleteBrowse(CustomerEntry entry)
    {
        if (entry == null || entry.State != CustomerState.Browsing) return;
        entry.PendingArrival = null;
        entry.BrowsePauseUntil = 0f;
        entry.ReadyToOrder = true;
        entry.BrowsingFromQueue = false;
        entry.Npc?.Movement?.Stop();
        AdvanceQueue(entry.LocationId);
    }

    private static void MaintainCustomerFlow()
    {
        if (Time.time < _nextFlowMaintenanceAt) return;
        _nextFlowMaintenanceAt = Time.time + FlowMaintenanceInterval;

        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            AdvanceQueue(location.Id);
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

    private static void StartLeaving(CustomerEntry entry, bool requestAdvance = true)
    {
        if (entry.Npc == null) return;

        bool shouldAdvance = entry.QueueIndex == 0 || entry.BrowseSlot >= 0 || entry.QueueIndex > 0;
        bool exteriorOnly = entry.QueueIndex > 0
                            && (entry.State == CustomerState.JoiningQueue || entry.State == CustomerState.WaitingInQueue);
        bool notYetInside = entry.QueueIndex < 0 && entry.BrowseSlot < 0;
        entry.PendingArrival = null;
        DisableCustomerInteractable(entry.Npc);
        entry.QueueIndex = -1;
        entry.BrowseSlot = -1;
        entry.BrowseSpotId = -1;
        entry.ReadyToOrder = false;
        SetState(entry, CustomerState.Leaving);
        entry.DespawnDeadline = Time.time + DespawnHardCeiling;

        var location = PropertySystem.Find(entry.LocationId);
        if (location != null)
        {
            entry.CurrentNavTarget = location.GetSpawnAnchor();
            if (exteriorOnly || notYetInside)
            {
                entry.Npc.Movement?.SetDestination(entry.CurrentNavTarget);
            }
            else
            {
                // RecallNPC handles door-aware exit routing for customers tracked inside.
                entry.NavBuilder.RecallNPC(GetNavComponent(entry.Npc));
            }
        }
        else
        {
            entry.NavBuilder.RecallNPC(GetNavComponent(entry.Npc));
        }

        if (requestAdvance && shouldAdvance)
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

        int queueIdx = 1;
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
        entry.BrowsingFromQueue = false;

        var slot = QueueSlots.Get(queueIdx, location, worldQueueAnchor, entry.NavBuilder);
        SendToQueueSlot(entry, slot, () => ArriveQueueSlot(entry));
        AdvanceQueue(entry.LocationId);
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
            entry.NavBuilder.SendNPCToPosition(GetNavComponent(entry.Npc), slot.Position, onArrival);
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
            NpcSpawner.Despawn(entry.Npc);

        if (oldIndex > 0)
            ReflowAfterRemoval(locId, oldIndex);
        if (oldIndex == 0 || entry.BrowseSlot >= 0)
            AdvanceQueue(locId);

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
            e.QueueIndex = Mathf.Max(1, e.QueueIndex - 1);
            try
            {
                var slot = QueueSlots.Get(e.QueueIndex, location, anchor, e.NavBuilder);
                SendToQueueSlot(e, slot, () => ArriveQueueSlot(e));
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
        if (entry.QueueIndex == 0)
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

    private static Component GetNavComponent(NPC npc)
    {
        return npc?.Movement != null ? npc.Movement : npc;
    }

    private static void SendToCounter(CustomerEntry entry, Vector3 worldQueueAnchor)
    {
        int oldQueueIndex = entry.QueueIndex;
        bool oldReadyToOrder = entry.ReadyToOrder;
        var oldTarget = entry.CurrentNavTarget;
        var oldPendingTarget = entry.PendingWorldTarget;
        var oldPendingArrival = entry.PendingArrival;

        entry.QueueIndex = 0;
        entry.ReadyToOrder = false;
        entry.CurrentNavTarget = worldQueueAnchor;
        if (LocationGeometry.TryGetCounterWorldPos(entry.LocationId, out var counterWorld))
        {
            entry.PendingWorldTarget = counterWorld;
            entry.PendingArrival = () => OnArrived(entry);
        }
        try
        {
            entry.NavBuilder.SendNPCToPosition(GetNavComponent(entry.Npc), worldQueueAnchor, onArrival: () => OnArrived(entry));
            SetState(entry, CustomerState.WalkingIn);
        }
        catch
        {
            entry.QueueIndex = oldQueueIndex;
            entry.ReadyToOrder = oldReadyToOrder;
            entry.CurrentNavTarget = oldTarget;
            entry.PendingWorldTarget = oldPendingTarget;
            entry.PendingArrival = oldPendingArrival;
            throw;
        }
    }

    private static bool HasCounterCustomer(string locationId)
    {
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if ((e.State == CustomerState.WalkingIn && e.QueueIndex == 0)
                || e.State == CustomerState.WaitingAtCounter
                || e.State == CustomerState.BeingServed)
                return true;
        }
        return false;
    }

    private static CustomerEntry FindNextReadyBrowser(string locationId)
    {
        CustomerEntry lowest = null;
        foreach (var kvp in _active)
        {
            var e = kvp.Value;
            if (e.LocationId != locationId) continue;
            if (e.BrowseSlot < 0) continue;
            if (e.State == CustomerState.Leaving) continue;
            if (lowest == null || e.BrowseSlot < lowest.BrowseSlot)
                lowest = e;
        }

        return lowest != null
               && lowest.State == CustomerState.Browsing
               && lowest.ReadyToOrder
            ? lowest
            : null;
    }

    private static bool TryPromoteQueuedCustomerToBrowse(CustomerEntry entry, MogulLocation location)
    {
        int browseSlot = FindOpenBrowseSlot(location.Id);
        if (browseSlot < 0 || !CanEnterStoreForBrowse(location)) return false;
        if (!TryBuildBrowseTargets(location, entry.NavBuilder, entry.Npc.gameObject.GetInstanceID(),
                out int browseSpotId, out var browseTargets, out var lookTargets))
            return false;

        int oldIndex = entry.QueueIndex;
        entry.QueueIndex = -1;
        entry.BrowseTargets = browseTargets;
        entry.BrowseLookTargets = lookTargets;
        entry.BrowseIndex = 0;
        entry.BrowseSlot = browseSlot;
        entry.BrowseSpotId = browseSpotId;
        entry.BrowsePauseUntil = 0f;
        entry.ReadyToOrder = false;
        entry.BrowsingFromQueue = true;
        if (!TrySendToBrowseTarget(entry, 0))
        {
            entry.QueueIndex = oldIndex;
            entry.BrowseSlot = -1;
            entry.BrowseSpotId = -1;
            entry.BrowsingFromQueue = false;
            return false;
        }
        ReflowAfterRemoval(entry.LocationId, oldIndex);
        return true;
    }

    private static void AdvanceQueue(string locationId)
    {
        if (!SellDesk.TryGetQueueAnchor(locationId, out var worldQueueAnchor)) return;
        var location = PropertySystem.Find(locationId);
        if (location == null) return;
        if (!HasSellableStock(locationId))
        {
            DismissPreCounterCustomersNoStock(locationId);
            return;
        }

        if (!HasCounterCustomer(locationId))
        {
            var nextBrowser = FindNextReadyBrowser(locationId);
            if (nextBrowser != null)
            {
                try
                {
                    SendToCounter(nextBrowser, worldQueueAnchor);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Mogul] AdvanceQueue: counter route failed for browser slot {nextBrowser.BrowseSlot}: {ex.Message}");
                }
            }
        }

        while (CanEnterStoreForBrowse(location) && FindOpenBrowseSlot(locationId) >= 0)
        {
            var waiting = new List<CustomerEntry>();
            foreach (var kvp in _active)
            {
                var e = kvp.Value;
                if (e.Npc == null || e.Npc.gameObject == null) continue;
                if (e.LocationId == locationId
                    && e.QueueIndex > 0
                    && (e.State == CustomerState.WaitingInQueue || e.State == CustomerState.JoiningQueue))
                    waiting.Add(e);
            }
            if (waiting.Count == 0) return;

            waiting.Sort((a, b) => a.QueueIndex.CompareTo(b.QueueIndex));
            if (!TryPromoteQueuedCustomerToBrowse(waiting[0], location))
                return;
        }
    }

}
