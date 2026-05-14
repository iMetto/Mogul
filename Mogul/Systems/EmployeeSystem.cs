using System;
using System.Collections.Generic;
using Il2CppScheduleOne.NPCs;
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

    public static string GetBudtenderGrowStatus(string locationId)
    {
        int budtenders = CountRole(locationId, EmployeeRole.Budtender);
        if (budtenders <= 0) return "No budtender hired";

        int dailyYield = EmployeeProduction.GetBudtenderDailyYield(budtenders);
        int stock = GetVirtualInventory(locationId, EmployeeProduction.TestBudtenderProductId);
        return $"OG Kush test grow · {dailyYield} pkg/day · virtual stock {stock} pkg";
    }

    public static void RequestHire(string locationId, EmployeeRole role)
    {
        if (string.IsNullOrEmpty(locationId)) return;
        MogulNetwork.RequestAction(MogulActions.HireEmployee, $"{locationId}:{role}");
    }

    public static void Tick()
    {
        if (!MogulNetwork.IsHost) return;
        int currentDay = GetCurrentGameDay();
        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            int yield = TickBudtenderProduction(location.Id, currentDay);
            if (yield > 0)
            {
                MogulNetwork.RequestAction(MogulActions.AddVirtualInventory,
                    $"{location.Id}:{EmployeeProduction.TestBudtenderProductId}:{yield}");
                MelonLogger.Msg($"[Mogul] Budtender produced {yield} OG Kush at {location.Id}");
            }
        }
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

    private static int GetCurrentGameDay()
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

    public static void SyncAll()
    {
        foreach (var location in PropertySystem.Catalog)
            SyncLocation(location.Id);
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
        for (int i = 0; i < employees.Count; i++)
        {
            var employee = employees[i];
            if (employee == null || string.IsNullOrEmpty(employee.Id)) continue;
            if (_spawned.ContainsKey(employee.Id)) continue;

            var localPos = GetWorkerLocalPosition(locationId, location, employee.Role, i);
            var spawnPos = location.GetSpawnAnchor();
            CustomerSpawner.SpawnWorkerNPC(spawnPos, employee.Role, employee.DisplayName, npc =>
            {
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

    private static Vector3 GetWorkerLocalPosition(string locationId, MogulLocation location, EmployeeRole role, int index)
    {
        var desk = location.DeskOffset != Vector3.zero
            ? location.DeskOffset
            : new Vector3(location.RoomSize.x * 0.5f, 0f, location.RoomSize.z * 0.5f);

        return role switch
        {
            EmployeeRole.Cashier   => SellDesk.TryGetStaffAnchor(locationId, out var staffAnchor) ? staffAnchor : desk,
            EmployeeRole.Budtender => new Vector3(1.5f + index * 0.7f, 0f, location.RoomSize.z - 1.5f),
            EmployeeRole.Runner    => new Vector3(location.RoomSize.x - 1.5f, 0f, 1.5f + index * 0.7f),
            _                      => desk,
        };
    }

    private static void FaceWorker(NPC npc, GameObject buildingRoot, string locationId, MogulLocation location, EmployeeRole role, Vector3 localPos)
    {
        if (npc == null || buildingRoot == null) return;

        Vector3 localTarget;
        if (role == EmployeeRole.Cashier && SellDesk.TryGetQueueAnchor(locationId, out var queueAnchor))
            localTarget = queueAnchor;
        else
            localTarget = new Vector3(location.RoomSize.x * 0.5f, localPos.y, location.RoomSize.z * 0.5f);

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

        if (_growTents.ContainsKey(locationId)) return;
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null) return;

        var location = PropertySystem.Find(locationId);
        if (location == null) return;

        var localPos = GetGrowTentLocalPosition(location);
        var pending = new GameObject("Mogul_GrowTentPending_" + locationId);
        pending.transform.SetParent(buildingRoot.transform, false);
        pending.transform.localPosition = localPos;
        _growTents[locationId] = pending;

        try
        {
            var placer = new PrefabPlacer(buildingRoot.transform);
            placer.Place(new PrefabRef("GrowTent_Built"), localPos, Quaternion.identity, networked: true,
                onReady: tentGo =>
                {
                    if (pending != null)
                        UnityEngine.Object.Destroy(pending);

                    if (tentGo == null)
                    {
                        _growTents.Remove(locationId);
                        MelonLogger.Warning($"[Mogul] GrowTent_Built onReady returned null for {locationId}");
                        return;
                    }

                    tentGo.name = "Mogul_GrowTent_" + locationId;
                    _growTents[locationId] = tentGo;
                });
        }
        catch (Exception ex)
        {
            _growTents.Remove(locationId);
            if (pending != null)
                UnityEngine.Object.Destroy(pending);
            MelonLogger.Warning($"[Mogul] GrowTent_Built spawn failed for {locationId}: {ex.Message}");
        }
    }

    private static Vector3 GetGrowTentLocalPosition(MogulLocation location)
    {
        return new Vector3(
            Mathf.Clamp(2.8f, 1.4f, Mathf.Max(1.4f, location.RoomSize.x - 1.4f)),
            0.35f,
            Mathf.Clamp(location.RoomSize.z - 2.1f, 1.2f, Mathf.Max(1.2f, location.RoomSize.z - 1.2f)));
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
        if (!_growTents.TryGetValue(locationId, out var tent)) return;
        if (tent != null)
            UnityEngine.Object.Destroy(tent);
        _growTents.Remove(locationId);
    }
}
