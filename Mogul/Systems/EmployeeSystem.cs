using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.ObjectScripts;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building.Components;
using S1MAPI.Core;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Systems;

public static class EmployeeSystem
{
    private class WorkerEntry
    {
        public string EmployeeId;
        public string LocationId;
        public EmployeeRole Role;
        public NPC Npc;
    }

    private static readonly Dictionary<string, WorkerEntry> _spawned = new();
    private static readonly Dictionary<string, GameObject> _growTents = new();
    private static readonly Dictionary<string, string> _growVisualKeys = new();
    private static readonly HashSet<string> _growLightsLogged = new();
    private const int FirstGrowSlot = 0;
    private static bool _dumpedSeedDefinitions;
    private static int _lastGrowSyncDay = -1;
    private static int _lastGrowSyncBucket = -1;

    public static void Initialize()
    {
        LocationSpawner.OnBuildingReady += (locationId, _) => SyncLocation(locationId);
        MogulNetwork.OnDataChanged += _ => SyncAll();
    }

    public static void ClearSpawned()
    {
        foreach (var entry in _spawned.Values)
            if (entry.Npc != null)
                CustomerSpawner.Despawn(entry.Npc);
        _spawned.Clear();
        ClearGrowTents();
    }

    public static void EvictFromLocation(string locationId)
    {
        var toRemove = new List<string>();
        foreach (var kvp in _spawned)
        {
            if (kvp.Value.LocationId != locationId) continue;
            if (kvp.Value.Npc != null)
                CustomerSpawner.Despawn(kvp.Value.Npc);
            toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove)
            _spawned.Remove(id);
        RemoveGrowTent(locationId);
    }

    public static IReadOnlyList<HiredEmployeeData> GetEmployees(string locationId)
    {
        if (string.IsNullOrEmpty(locationId)) return Array.Empty<HiredEmployeeData>();
        return MogulNetwork.Data.LocationEmployees.TryGetValue(locationId, out var list)
            ? list
            : Array.Empty<HiredEmployeeData>();
    }

    public static bool HasRole(string locationId, EmployeeRole role)
    {
        foreach (var employee in GetEmployees(locationId))
            if (employee.Role == role)
                return true;
        return false;
    }

    public static int CountRole(string locationId, EmployeeRole role) =>
        EmployeeProduction.CountRole(GetEmployees(locationId), role);

    public static int GetVirtualInventory(string locationId, string productId)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(productId)) return 0;
        return MogulNetwork.Data.LocationVirtualInventory.TryGetValue(locationId, out var inv)
            && inv.TryGetValue(productId, out var qty)
            ? qty
            : 0;
    }

    public static BudtenderOrderData GetBudtenderOrder(string locationId)
    {
        return !string.IsNullOrEmpty(locationId)
            && MogulNetwork.Data.LocationBudtenderOrders.TryGetValue(locationId, out var order)
            ? order
            : null;
    }

    public static string GetBudtenderGrowStatus(string locationId)
    {
        int budtenders = CountRole(locationId, EmployeeRole.Budtender);
        if (budtenders <= 0) return "No budtender hired";

        var order = GetBudtenderOrder(locationId);
        if (order == null) return "Budtender idle · order one stack to start a grow";

        int currentDay = GetCurrentGameDay();
        int currentTime = GetCurrentGameTime();
        string product = StrainMixingSystem.BuildOrderDisplayName(order);
        if (EmployeeProduction.IsOrderComplete(order, currentDay, currentTime))
            return $"{product} stack ready for storage";

        return $"{product} growing · ready day {order.ReadyDay} after 17:00";
    }

    public static void RequestHire(string locationId, EmployeeRole role)
    {
        if (string.IsNullOrEmpty(locationId)) return;
        MogulNetwork.RequestAction(MogulActions.HireEmployee, $"{locationId}:{role}");
    }

    public static void RequestFire(string locationId, EmployeeRole role)
    {
        if (string.IsNullOrEmpty(locationId)) return;
        MogulNetwork.RequestAction(MogulActions.FireEmployee, $"{locationId}:{role}");
    }

    public static void RequestBudtenderOrder(string locationId, string productId, IReadOnlyList<string> ingredientIds = null)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(productId)) return;
        MogulNetwork.RequestAction(MogulActions.StartBudtenderOrder,
            StrainMixingSystem.BuildOrderPayload(locationId, productId, ingredientIds));
    }

    public static void Tick()
    {
        foreach (var entry in _spawned.Values)
            DisableOutdoorWeather(entry.Npc);

        if (!MogulNetwork.IsHost) return;
        int currentDay = GetCurrentGameDay();
        int currentTime = GetCurrentGameTime();
        bool syncGrowVisuals = ShouldSyncGrowVisuals(currentDay, currentTime);
        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            bool hasBudtender = HasRole(location.Id, EmployeeRole.Budtender);
            if (syncGrowVisuals && hasBudtender)
                SyncGrowTent(location.Id);
            var order = GetBudtenderOrder(location.Id);
            if (hasBudtender && EmployeeProduction.IsOrderComplete(order, currentDay, currentTime))
            {
                MogulNetwork.RequestAction(MogulActions.CompleteBudtenderOrder, location.Id);
            }
        }
    }

    private static bool ShouldSyncGrowVisuals(int currentDay, int currentTime)
    {
        int bucket =
            currentTime < EmployeeProduction.WorkingDayStart ? 0 :
            currentTime < EmployeeProduction.WorkingDayEnd ? 1 :
            2;

        if (currentDay == _lastGrowSyncDay && bucket == _lastGrowSyncBucket)
            return false;

        _lastGrowSyncDay = currentDay;
        _lastGrowSyncBucket = bucket;
        return true;
    }

    public static int TickBudtenderProduction(string locationId, int currentDay)
    {
        if (string.IsNullOrEmpty(locationId) || currentDay <= 0) return 0;
        if (!MogulNetwork.Data.LocationBudtenderProductionDay.TryGetValue(locationId, out var lastDay))
        {
            MogulNetwork.Data.LocationBudtenderProductionDay[locationId] = currentDay;
            return 0;
        }

        int elapsed = currentDay - lastDay;
        if (elapsed <= 0) return 0;

        int yield = EmployeeProduction.CalculateBudtenderYield(GetEmployees(locationId), elapsed);
        MogulNetwork.Data.LocationBudtenderProductionDay[locationId] = currentDay;
        return yield;
    }

    public static int GetCurrentGameDay()
    {
        try
        {
            var tm = Il2CppScheduleOne.DevUtilities.NetworkSingleton<Il2CppScheduleOne.GameTime.TimeManager>.Instance;
            return tm != null ? tm.ElapsedDays : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static int GetCurrentGameTime()
    {
        try
        {
            var tm = Il2CppScheduleOne.DevUtilities.NetworkSingleton<Il2CppScheduleOne.GameTime.TimeManager>.Instance;
            return tm != null ? tm.CurrentTime : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void SyncAll()
    {
        foreach (var location in PropertySystem.Catalog)
            SyncLocation(location.Id);
    }

    public static void ApplyPlacementOverrides(string locationId)
    {
        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        if (MogulPlacementSystem.TryGetPlacement(locationId, MogulPlacementSystem.GrowTent, out var growPos, out var growRot))
            SetLiveGrowTentTransform(locationId, growPos, growRot);

        if (MogulPlacementSystem.TryGetPlacement(locationId, MogulPlacementSystem.Cashier, out var staffPos, out var staffRot))
            SetLiveCashierTransform(locationId, staffPos, staffRot);

        SetLiveBudtenderTransforms(locationId, location);
    }

    public static void SetLiveCashierTransform(string locationId, Vector3 localPos, Quaternion localRot)
    {
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null) return;
        foreach (var entry in _spawned.Values)
        {
            if (entry.LocationId != locationId || entry.Role != EmployeeRole.Cashier || entry.Npc == null) continue;
            entry.Npc.Movement?.Stop();
            entry.Npc.transform.position = buildingRoot.transform.TransformPoint(localPos);
            entry.Npc.transform.rotation = buildingRoot.transform.rotation * localRot;
        }
    }

    public static void SetLiveGrowTentTransform(string locationId, Vector3 localPos, Quaternion localRot)
    {
        if (!_growTents.TryGetValue(locationId, out var tent) || tent == null) return;
        tent.transform.localPosition = localPos;
        tent.transform.localRotation = localRot;
    }

    private static void SetLiveBudtenderTransforms(string locationId, MogulLocation location)
    {
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null) return;

        int index = 0;
        foreach (var entry in _spawned.Values)
        {
            if (entry.LocationId != locationId || entry.Role != EmployeeRole.Budtender || entry.Npc == null) continue;
            var localPos = GetBudtenderLocalPosition(location, index++);
            entry.Npc.Movement?.Stop();
            entry.Npc.transform.position = buildingRoot.transform.TransformPoint(localPos);
            FaceWorker(entry.Npc, buildingRoot, locationId, location, entry.Role, localPos);
        }
    }

    public static void DumpGrowTentHierarchy(string locationId)
    {
        if (string.IsNullOrEmpty(locationId)) return;
        if (!_growTents.TryGetValue(locationId, out var tent) || tent == null)
        {
            MelonLogger.Msg($"[Mogul] No spawned grow tent for {locationId}");
            return;
        }

        MelonLogger.Msg($"[Mogul] Grow tent hierarchy for {locationId}:");
        DumpTransform(tent.transform, 0, 4);
    }

    public static void DumpNearestGrowTent(Vector3 worldPosition)
    {
        try
        {
            var pots = UnityEngine.Object.FindObjectsOfType<Pot>();
            Pot nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < pots.Length; i++)
            {
                if (pots[i] == null) continue;
                float sqr = (pots[i].transform.position - worldPosition).sqrMagnitude;
                if (sqr >= nearestSqr) continue;
                nearest = pots[i];
                nearestSqr = sqr;
            }

            if (nearest == null)
            {
                MelonLogger.Msg("[Mogul] No live Pot grow tents found near player");
                return;
            }

            MelonLogger.Msg($"[Mogul] Nearest live Pot hierarchy distance={Mathf.Sqrt(nearestSqr):F2} name={nearest.name}:");
            DumpTransform(nearest.transform, 0, 6);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Failed dumping nearest grow tent: " + ex.Message);
        }
    }

    public static void DumpSeedDefinitionsOnce()
    {
        if (_dumpedSeedDefinitions) return;
        _dumpedSeedDefinitions = true;

        try
        {
            var registry = UnityEngine.Object.FindObjectOfType<Registry>();
            var items = registry?.GetAllItems();
            if (items == null)
            {
                MelonLogger.Warning("[Mogul] Seed dump failed: registry items unavailable");
                return;
            }

            int count = 0;
            MelonLogger.Msg("[Mogul] SeedDefinition dump:");
            for (int i = 0; i < items.Count; i++)
            {
                var seed = items[i]?.TryCast<SeedDefinition>();
                if (seed == null) continue;
                count++;
                string plantName = seed.PlantPrefab != null ? seed.PlantPrefab.name : "(null)";
                string seedPrefabName = seed.FunctionSeedPrefab != null ? seed.FunctionSeedPrefab.name : "(null)";
                MelonLogger.Msg($"[Mogul] [SEED] id={seed.ID} name={seed.Name} plant={plantName} seedPrefab={seedPrefabName}");
            }
            MelonLogger.Msg($"[Mogul] SeedDefinition dump complete: {count}");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Seed dump failed: " + ex.Message);
        }
    }

    private static void SyncLocation(string locationId)
    {
        SyncGrowTent(locationId);

        if (!MogulNetwork.IsHost) return;
        if (!PropertySystem.IsOwned(locationId)) return;
        if (!LocationSpawner.TryGetNavigationBuilder(locationId, out var navBuilder)) return;
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null) return;

        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        var employees = GetEmployees(locationId);
        DespawnMissingWorkers(locationId, employees);
        for (int i = 0; i < employees.Count; i++)
        {
            var employee = employees[i];
            if (employee == null || string.IsNullOrEmpty(employee.Id)) continue;
            if (employee.Role != EmployeeRole.Cashier && employee.Role != EmployeeRole.Budtender) continue;
            if (_spawned.ContainsKey(employee.Id)) continue;

            var localPos = GetWorkerLocalPosition(locationId, location, employee.Role, i);
            var spawnPos = location.GetSpawnAnchor();
            CustomerSpawner.SpawnWorkerNPC(spawnPos, employee.Role, employee.DisplayName, npc =>
            {
                DisableOutdoorWeather(npc);
                _spawned[employee.Id] = new WorkerEntry
                {
                    EmployeeId = employee.Id,
                    LocationId = locationId,
                    Role = employee.Role,
                    Npc = npc,
                };

                try
                {
                    navBuilder.SendNPCToPosition(npc, localPos, onArrival: () =>
                    {
                        npc.Movement?.Stop();
                        DisableOutdoorWeather(npc);
                        FaceWorker(npc, buildingRoot, locationId, location, employee.Role, localPos);
                        npc.DialogueHandler?.WorldspaceRend?.ShowText(employee.Role.ToString(), 2f);
                    });
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Mogul] Worker route failed for {employee.Id}: {ex.Message}");
                }
            });
        }
    }

    private static void DespawnMissingWorkers(string locationId, IReadOnlyList<HiredEmployeeData> employees)
    {
        var savedIds = new HashSet<string>();
        for (int i = 0; i < employees.Count; i++)
            if (!string.IsNullOrEmpty(employees[i]?.Id))
                savedIds.Add(employees[i].Id);

        var toRemove = new List<string>();
        foreach (var kvp in _spawned)
        {
            if (kvp.Value.LocationId != locationId) continue;
            if (savedIds.Contains(kvp.Key)) continue;
            if (kvp.Value.Npc != null)
                CustomerSpawner.Despawn(kvp.Value.Npc);
            toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove)
            _spawned.Remove(id);
    }

    private static void DisableOutdoorWeather(NPC npc)
    {
        try
        {
            if (npc == null) return;
            npc.IsUnderCover = true;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Failed disabling worker umbrella: " + ex.Message);
        }
    }

    private static Vector3 GetWorkerLocalPosition(string locationId, MogulLocation location, EmployeeRole role, int index)
    {
        var desk = location.DeskOffset != Vector3.zero
            ? location.DeskOffset
            : RoomCenter(location);

        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        return role switch
        {
            EmployeeRole.Cashier   => SellDesk.TryGetStaffAnchor(locationId, out var staffAnchor) ? staffAnchor : desk,
            EmployeeRole.Budtender => GetBudtenderLocalPosition(location, index),
            _                      => desk,
        };
    }

    private static Vector3 GetBudtenderLocalPosition(MogulLocation location, int index)
    {
        var tent = GetGrowTentLocalPosition(location);
        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        float zOffset = tent.z <= roomSize.z * 0.5f ? 1.4f : -1.4f;
        return new Vector3(
            Mathf.Clamp(tent.x + index * 0.7f, 0.5f, Mathf.Max(0.5f, roomSize.x - 0.5f)),
            0f,
            Mathf.Clamp(tent.z + zOffset, 0.5f, Mathf.Max(0.5f, roomSize.z - 0.5f)));
    }

    private static void FaceWorker(NPC npc, GameObject buildingRoot, string locationId, MogulLocation location, EmployeeRole role, Vector3 localPos)
    {
        if (npc == null || buildingRoot == null) return;

        if (role == EmployeeRole.Cashier && MogulPlacementSystem.TryGetPlacement(locationId, MogulPlacementSystem.Cashier, out _, out var savedRot))
        {
            npc.transform.rotation = buildingRoot.transform.rotation * savedRot;
            return;
        }

        if (role == EmployeeRole.Cashier && location.SellDesk.HasStaffLocalRotation)
        {
            npc.transform.rotation = buildingRoot.transform.rotation * location.SellDesk.StaffLocalRotation;
            return;
        }

        Vector3 localTarget;
        if (role == EmployeeRole.Cashier && SellDesk.TryGetQueueAnchor(locationId, out var queueAnchor))
            localTarget = queueAnchor;
        else if (role == EmployeeRole.Budtender)
            localTarget = GetGrowTentLocalPosition(location);
        else
            localTarget = RoomCenter(location, localPos.y);

        var worldTarget = buildingRoot.transform.TransformPoint(localTarget);
        var dir = worldTarget - npc.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        npc.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private static void SyncGrowTent(string locationId)
    {
        if (!PropertySystem.IsOwned(locationId) || !HasRole(locationId, EmployeeRole.Budtender))
        {
            RemoveGrowTent(locationId);
            return;
        }

        string slotKey = GetGrowSlotKey(locationId, FirstGrowSlot);
        if (_growTents.TryGetValue(slotKey, out var existingTent) && existingTent != null)
        {
            EnsureDecorativePlant(existingTent.transform, locationId, FirstGrowSlot);
            EnsureGrowLightOn(existingTent.transform, locationId);
            return;
        }
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null) return;

        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        var localPos = GetGrowTentLocalPosition(location);
        var pending = new GameObject("Mogul_GrowTentPending_" + locationId);
        pending.transform.SetParent(buildingRoot.transform, false);
        pending.transform.localPosition = localPos;
        _growTents[slotKey] = pending;

        try
        {
            var placer = new PrefabPlacer(buildingRoot.transform);
            placer.Place(new PrefabRef("GrowTent_Built"), localPos, GetGrowTentLocalRotation(location), networked: true,
                onReady: tentGo =>
                {
                    if (pending != null)
                        UnityEngine.Object.Destroy(pending);

                    if (tentGo == null)
                    {
                        _growTents.Remove(slotKey);
                        MelonLogger.Warning($"[Mogul] GrowTent_Built onReady returned null for {locationId}");
                        return;
                    }

                     tentGo.name = "Mogul_GrowTent_" + locationId;
                     tentGo.transform.localPosition = localPos;
                     tentGo.transform.localRotation = GetGrowTentLocalRotation(location);
                     EnsureDecorativePlant(tentGo.transform, locationId, FirstGrowSlot);
                     EnsureGrowLightOn(tentGo.transform, locationId);
                     _growTents[slotKey] = tentGo;
                 });
        }
        catch (Exception ex)
        {
            _growTents.Remove(slotKey);
            if (pending != null)
                UnityEngine.Object.Destroy(pending);
            MelonLogger.Warning($"[Mogul] GrowTent_Built spawn failed for {locationId}: {ex.Message}");
        }
    }

    public static Vector3 GetDefaultGrowTentLocalPosition(MogulLocation location)
    {
        if (location.GrowTent.LocalPos != Vector3.zero)
            return location.GrowTent.LocalPos;

        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        return new Vector3(
            Mathf.Clamp(2.8f, 1.4f, Mathf.Max(1.4f, roomSize.x - 1.4f)),
            0.35f,
            Mathf.Clamp(roomSize.z - 2.1f, 1.2f, Mathf.Max(1.2f, roomSize.z - 1.2f)));
    }

    private static Vector3 RoomCenter(MogulLocation location, float y = 0f)
    {
        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        return new Vector3(roomSize.x * 0.5f, y, roomSize.z * 0.5f);
    }

    public static Quaternion GetDefaultGrowTentLocalRotation(MogulLocation location)
    {
        return location.GrowTent.Rotation;
    }

    private static Vector3 GetGrowTentLocalPosition(MogulLocation location)
    {
        return MogulPlacementSystem.TryGetPlacement(location.Id, MogulPlacementSystem.GrowTent, out var pos, out _)
            ? pos
            : GetDefaultGrowTentLocalPosition(location);
    }

    private static Quaternion GetGrowTentLocalRotation(MogulLocation location)
    {
        return MogulPlacementSystem.TryGetPlacement(location.Id, MogulPlacementSystem.GrowTent, out _, out var rot)
            ? rot
            : GetDefaultGrowTentLocalRotation(location);
    }

    private static void CreateTentCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        SetColorAndDisableCollider(go, color);
    }

    private static void EnsureDecorativePlant(Transform tent, string locationId, int slotIndex)
    {
        if (tent == null) return;

        Transform parent = tent.Find("Model/IntObj/PlantContainer") ?? tent;
        string slotKey = GetGrowSlotKey(locationId, slotIndex);
        if (!TryGetActiveGrowSlotProduct(locationId, slotIndex, out var productId))
        {
            if (_growVisualKeys.Remove(slotKey))
            {
                ClearMogulPlantVisuals(parent);
                MelonLogger.Msg($"[Mogul] Cleared grow plant visual for {locationId} slot {slotIndex}");
            }
            return;
        }

        string plantName = GetPlantPrefabName(productId);
        string visualName = "Mogul_VisualPlant_" + productId;
        if (_growVisualKeys.TryGetValue(slotKey, out var existingKey) && existingKey == visualName && parent.Find(visualName) != null)
            return;

        ClearMogulPlantVisuals(parent);
        _growVisualKeys[slotKey] = visualName;

        if (TrySpawnSeedPlant(productId, plantName, parent, visualName, locationId))
            return;

        var root = new GameObject("Mogul_FullGrownPlant");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = parent == tent ? new Vector3(0f, 0.42f, 0f) : Vector3.zero;
        root.transform.localRotation = Quaternion.identity;

        CreateTentCube(root.transform, "Stem", new Vector3(0f, 0.35f, 0f), new Vector3(0.08f, 0.70f, 0.08f), new Color(0.22f, 0.48f, 0.16f));
        CreateTentCube(root.transform, "Canopy", new Vector3(0f, 0.82f, 0f), new Vector3(0.85f, 0.55f, 0.85f), new Color(0.12f, 0.50f, 0.17f));
        CreateTentCube(root.transform, "Top", new Vector3(0f, 1.18f, 0f), new Vector3(0.48f, 0.42f, 0.48f), new Color(0.18f, 0.62f, 0.22f));
        CreateTentCube(root.transform, "BudA", new Vector3(0.22f, 1.08f, 0.08f), new Vector3(0.18f, 0.26f, 0.18f), new Color(0.50f, 0.77f, 0.25f));
        CreateTentCube(root.transform, "BudB", new Vector3(-0.18f, 0.98f, -0.12f), new Vector3(0.16f, 0.24f, 0.16f), new Color(0.50f, 0.77f, 0.25f));
    }

    private static bool TryGetActiveGrowSlotProduct(string locationId, int slotIndex, out string productId)
    {
        productId = null;
        if (slotIndex != FirstGrowSlot) return false;

        var order = GetBudtenderOrder(locationId);
        int currentDay = GetCurrentGameDay();
        int currentTime = GetCurrentGameTime();
        if (order == null || currentDay != order.ReadyDay
            || currentTime < EmployeeProduction.WorkingDayStart
            || currentTime >= EmployeeProduction.WorkingDayEnd)
            return false;

        productId = !string.IsNullOrWhiteSpace(order.BaseProductId) ? order.BaseProductId : order.ProductId;
        return !string.IsNullOrEmpty(productId);
    }

    private static string GetPlantPrefabName(string productId)
    {
        return productId switch
        {
            "greencrack" => "GreenCrackPlant",
            "sourdiesel" => "SourDieselPlant",
            "granddaddypurple" => "GranddaddyPurplePlant",
            "ogkush" => "OGKush_Plant",
            _ => "OGKush_Plant",
        };
    }

    private static void ClearMogulPlantVisuals(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child == null) continue;
            if (child.name.StartsWith("Mogul_VisualPlant_", StringComparison.Ordinal)
                || child.name == "Mogul_FullGrownPlant")
                UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static bool TrySpawnSeedPlant(string productId, string plantName, Transform parent, string visualName, string locationId)
    {
        try
        {
            foreach (var seedId in GetSeedIdCandidates(productId))
            {
                SeedDefinition seed = null;
                try { seed = Registry.GetItem<SeedDefinition>(seedId); }
                catch { }
                if (seed == null || seed.PlantPrefab == null) continue;

                var clone = UnityEngine.Object.Instantiate(seed.PlantPrefab.gameObject);
                clone.name = visualName;
                clone.transform.SetParent(parent, false);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = seed.PlantPrefab.transform.localScale;
                ShowFinalPlantStage(clone.transform);
                MelonLogger.Msg($"[Mogul] Spawned grow plant visual for {locationId}: {plantName} via seed {seedId}");
                return true;
            }

            MelonLogger.Warning($"[Mogul] Grow plant seed prefab not found for {locationId}: {productId}/{plantName}");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Failed spawning grow plant visual: " + ex.Message);
        }
        return false;
    }

    private static IEnumerable<string> GetSeedIdCandidates(string productId)
    {
        yield return productId + "seed";
        yield return productId + "_seed";
        yield return productId + "seeds";
        yield return productId + "_seeds";
    }

    private static void ShowFinalPlantStage(Transform plant)
    {
        var stages = plant.GetComponentsInChildren<PlantGrowthStage>(true);
        for (int i = 0; i < stages.Length; i++)
            if (stages[i] != null)
                stages[i].gameObject.SetActive(stages[i].name == "weedplant_stage10");
    }

    private static string GetGrowSlotKey(string locationId, int slotIndex)
    {
        return locationId + ":grow_" + slotIndex;
    }

    private static void EnsureGrowLightOn(Transform tent, string locationId)
    {
        if (tent == null) return;

        try
        {
            var lights = tent.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].gameObject.SetActive(true);
                lights[i].enabled = true;
            }
            if (_growLightsLogged.Add(locationId))
                MelonLogger.Msg($"[Mogul] Grow tent lights enabled for {locationId}: {lights.Length}");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Failed enabling grow light: " + ex.Message);
        }
    }

    private static string GetTransformPath(Transform current, Transform root)
    {
        if (current == null) return "(missing)";
        if (current == root) return current.name;

        string path = current.name;
        while (current.parent != null && current.parent != root)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    private static void DumpTransform(Transform t, int depth, int maxDepth)
    {
        if (t == null || depth > maxDepth) return;
        string indent = new string(' ', depth * 2);
        string components = "";
        try
        {
            var comps = t.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                if (components.Length > 0) components += ",";
                components += comps[i].GetIl2CppType().Name;
            }
        }
        catch (Exception ex)
        {
            components = "component-read-failed:" + ex.Message;
        }

        MelonLogger.Msg($"[Mogul] {indent}{t.name} local=({t.localPosition.x:F2},{t.localPosition.y:F2},{t.localPosition.z:F2}) comps=[{components}]");
        for (int i = 0; i < t.childCount; i++)
            DumpTransform(t.GetChild(i), depth + 1, maxDepth);
    }

    private static void SetColorAndDisableCollider(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = color;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);
    }

    private static void ClearGrowTents()
    {
        foreach (var tent in _growTents.Values)
            if (tent != null)
                UnityEngine.Object.Destroy(tent);
        _growTents.Clear();
    }

    private static void RemoveGrowTent(string locationId)
    {
        string slotKey = GetGrowSlotKey(locationId, FirstGrowSlot);
        if (!_growTents.TryGetValue(slotKey, out var tent)) return;
        if (tent != null)
            UnityEngine.Object.Destroy(tent);
        _growTents.Remove(slotKey);
        _growVisualKeys.Remove(slotKey);
        _growLightsLogged.Remove(locationId);
    }
}
