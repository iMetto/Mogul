using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Weather;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building;
using S1MAPI.Building.Components;
using S1MAPI.Building.Structural;
using S1API.Doors;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Systems;

public static class LocationSpawner
{
    // Fired after a building is fully constructed and registered in _spawned.
    // Both host and client fire this — SellDesk subscribes to spawn desks on demand.
    public static event Action<string, GameObject> OnBuildingReady;

    // Tracks which location IDs have a building in the scene already.
    // Key = location.Id, Value = the spawned GameObject
    private static readonly Dictionary<string, GameObject> _spawned = new();
    private static readonly Dictionary<string, NavigationBuilder> _navBuilders = new();
    private static readonly Dictionary<string, List<GameObject>> _rackObjects = new();

    public static void Initialize()
    {
        // When save data changes (e.g. a purchase), re-check what needs spawning                                            
        MogulNetwork.OnDataChanged += _ => SyncSpawns();
    }
    public static void ClearSpawned()
    {
        foreach (var go in _spawned.Values)
            UnityEngine.Object.Destroy(go);
        _spawned.Clear();
        _navBuilders.Clear();
        _rackObjects.Clear();
        EmployeeSystem.ClearSpawned();
        CustomerManager.ClearQueueCache();
        SellDesk.ClearSpawned();
    }
    public static void SyncSpawns()
    {
        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;  // skip unowned
            if (_spawned.ContainsKey(location.Id)) continue;     // skip already built                                       
            SpawnBuilding(location);
        }
    }
    private static void SpawnBuilding(MogulLocation location)
    {
        try
        {
            const float foundationHeight = 0.4f;
            var design = DesignCatalog.Get(MogulNetwork.GetDesignId(location.Id));
            var ov = BuildingPreview.HasOverrides(location.Id) ? BuildingPreview.GetOrCreate(location.Id) : null;
            if (ov != null) design = ApplyDesignOverrides(design, ov);

            var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
            var builder = new BuildingBuilder(location.Name)
                .DefineRoom(roomSize.x, roomSize.y, roomSize.z);

            design.Apply(builder, location);

            if (ov != null)
            {
                if (ov.BaseMolding       == true) builder.AddBaseMolding();
                if (ov.CornerPillars     == true) builder.AddCornerPillars();
                if (ov.CornerTrim        == true) builder.AddCornerTrim();
                if (ov.RoofTrim          == true) builder.AddRoofTrim();
                if (ov.SecondaryRoofTrim == true) builder.AddSecondaryRoofTrim();
                if (ov.AmbientLighting   == true) builder.AddAmbientLighting();
            }

            builder
                .AddFoundation(height: foundationHeight, expandX: 0.25f, expandZ: 0.25f, material: Materials.Find("concrete light beige"))
                .AddStairs(location.Door, foundationHeight: foundationHeight);

            var go = builder.Build();
            go.transform.position = BuildingPreview.GetEffectiveWorldPosition(location) - new Vector3(0f, foundationHeight, 0f);
            ConfigureWeatherEnclosure(go, roomSize, foundationHeight);
            builder.FlattenTerrain();
            TerrainClearer.ClearAroundBuilding(go, roomSize, new ClearingOptions { Padding = 4f });

            var (doorPos, doorRot) = GetDoorLocalTransform(roomSize, location.Door);
            var doorPrefab = ov?.DoorPrefabKey != null
                ? BuildingPreview.ResolveDoorPrefab(ov.DoorPrefabKey)
                : Prefabs.ClassicalWoodenDoor;

            if (MogulNetwork.IsHost)
            {
                var placer = new PrefabPlacer(go.transform);

                placer.Place(doorPrefab, doorPos, doorRot,
                    networked: true,
                    onReady: doorGo =>
                    {
                        var ctrl = new DoorController(doorGo);
                        ctrl.PlayerAccess = DoorAccess.Open;
                    });

                var locId = location.Id;
                var existingRacks = GetLiveRackObjects(locId);
                if (existingRacks.Count > 0)
                {
                    _rackObjects[locId] = existingRacks;
                    for (int i = 0; i < existingRacks.Count && i < location.StorageRacks.Length; i++)
                    {
                        var rackGo = existingRacks[i];
                        if (rackGo == null) continue;
                        var rack = location.StorageRacks[i];
                        rackGo.transform.SetParent(go.transform, false);
                        rackGo.transform.localPosition = GetRackSpawnPosition(location.Id, i, rack);
                        rackGo.transform.localRotation = GetRackSpawnRotation(location.Id, i, rack);
                    }
                    ApplyRackPlacementOverrides(locId);
                }
                else
                {
                    _rackObjects[locId] = new List<GameObject>();
                    for (int i = 0; i < location.StorageRacks.Length; i++)
                    {
                        var rack = location.StorageRacks[i];
                        var pos = GetRackSpawnPosition(location.Id, i, rack);
                        var rot = GetRackSpawnRotation(location.Id, i, rack);
                        placer.Place(rack.Prefab, pos, rot, networked: true,
                            onReady: rackGo =>
                            {
                                if (rackGo != null)
                                {
                                    rackGo.transform.localPosition = pos;
                                    rackGo.transform.localRotation = rot;
                                }
                                if (_rackObjects.TryGetValue(locId, out var list))
                                    list.Add(rackGo);
                            });
                    }
                }
                if (existingRacks.Count > 0 && existingRacks.Count < location.StorageRacks.Length)
                {
                    for (int i = existingRacks.Count; i < location.StorageRacks.Length; i++)
                    {
                        var rack = location.StorageRacks[i];
                        var pos = GetRackSpawnPosition(location.Id, i, rack);
                        var rot = GetRackSpawnRotation(location.Id, i, rack);
                        placer.Place(rack.Prefab, pos, rot, networked: true,
                            onReady: rackGo =>
                        {
                            if (rackGo != null)
                            {
                                rackGo.transform.localPosition = pos;
                                rackGo.transform.localRotation = rot;
                            }
                            if (_rackObjects.TryGetValue(locId, out var list))
                                list.Add(rackGo);
                        });
                    }
                }
            }
            var navBuilder = builder.CreateNavigationBuilder();
            navBuilder.Build();
            _navBuilders[location.Id] = navBuilder;
            _spawned[location.Id] = go;
            OnBuildingReady?.Invoke(location.Id, go);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnBuilding failed for {location.Id}: {ex}");
        }

    }

    private static void ConfigureWeatherEnclosure(GameObject buildingRoot, Vector3 roomSize, float foundationHeight)
    {
        if (buildingRoot == null) return;

        try
        {
            var enclosure = buildingRoot.GetComponent<WeatherEnclosure>() ?? buildingRoot.AddComponent<WeatherEnclosure>();

            var volumeGo = new GameObject("Mogul_WeatherEnclosureVolume");
            volumeGo.transform.SetParent(buildingRoot.transform, false);
            volumeGo.transform.localPosition = Vector3.zero;
            volumeGo.transform.localRotation = Quaternion.identity;

            var basic = volumeGo.AddComponent<BasicEnclosure>();
            basic._center = new Vector3(roomSize.x * 0.5f, foundationHeight + roomSize.y * 0.5f, roomSize.z * 0.5f);
            basic._size = new Vector3(roomSize.x + 0.75f, roomSize.y + 1.25f, roomSize.z + 0.75f);
            basic._isBlendZone = false;

            enclosure.Start();
            var env = UnityEngine.Object.FindObjectOfType<EnvironmentManager>();
            env?.RegisterEnclosure(enclosure);
            env?.RegisterWeatherEnclosure(enclosure);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Weather enclosure setup failed: " + ex.Message);
        }
    }
    public static void RebuildBuilding(string locationId)
    {
        if (!MogulNetwork.IsHost) return;

        var location = PropertySystem.Find(locationId);
        if (location == null)
        {
            MelonLogger.Warning($"[Mogul] RebuildBuilding: location '{locationId}' not found");
            return;
        }

        CustomerManager.EvictFromLocation(locationId);
        EmployeeSystem.EvictFromLocation(locationId);
        PreserveRacksForRebuild(locationId);

        if (_spawned.TryGetValue(locationId, out var existing) && existing != null)
            UnityEngine.Object.Destroy(existing);
        _spawned.Remove(locationId);
        _navBuilders.Remove(locationId);

        SellDesk.ClearForLocation(locationId);

        SpawnBuilding(location);
    }

    public static bool TryGetSpawnedBuilding(string locationId, out GameObject buildingRoot)
    {
        return _spawned.TryGetValue(locationId, out buildingRoot);
    }
    public static bool TryGetRackObjects(string locationId, out List<GameObject> racks)
        => _rackObjects.TryGetValue(locationId, out racks);

    public static bool TryGetNavigationBuilder(string locationId, out NavigationBuilder nb)
    => _navBuilders.TryGetValue(locationId, out nb);

    public static bool TryGetRackLocalTransform(string locationId, int index, out Vector3 localPos, out Quaternion localRot)
    {
        if (_rackObjects.TryGetValue(locationId, out var racks) && racks != null && index >= 0 && index < racks.Count && racks[index] != null)
        {
            localPos = racks[index].transform.localPosition;
            localRot = racks[index].transform.localRotation;
            return true;
        }

        var location = PropertySystem.Find(locationId);
        if (location != null && index >= 0 && index < location.StorageRacks.Length)
        {
            var rack = location.StorageRacks[index];
            localPos = GetRackSpawnPosition(locationId, index, rack);
            localRot = GetRackSpawnRotation(locationId, index, rack);
            return true;
        }

        localPos = default;
        localRot = default;
        return false;
    }

    public static void SetRackLiveTransform(string locationId, int index, Vector3 localPos, Quaternion localRot)
    {
        if (!_rackObjects.TryGetValue(locationId, out var racks) || racks == null) return;
        if (index < 0 || index >= racks.Count || racks[index] == null) return;

        racks[index].transform.localPosition = localPos;
        racks[index].transform.localRotation = localRot;
    }

    public static void ApplyRackPlacementOverrides(string locationId)
    {
        if (!_rackObjects.TryGetValue(locationId, out var racks) || racks == null) return;
        for (int i = 0; i < racks.Count; i++)
        {
            if (racks[i] == null) continue;
            string objectId = i == 0 ? MogulPlacementSystem.Storage0 : "storage_" + i;
            if (MogulPlacementSystem.TryGetPlacement(locationId, objectId, out var pos, out var rot))
            {
                racks[i].transform.localPosition = pos;
                racks[i].transform.localRotation = rot;
            }
        }
    }

    private static List<GameObject> GetLiveRackObjects(string locationId)
    {
        if (!_rackObjects.TryGetValue(locationId, out var racks) || racks == null)
            return new List<GameObject>();
        racks.RemoveAll(r => r == null);
        return racks;
    }

    private static void PreserveRacksForRebuild(string locationId)
    {
        var racks = GetLiveRackObjects(locationId);
        if (racks.Count == 0)
        {
            _rackObjects.Remove(locationId);
            return;
        }

        foreach (var rack in racks)
            rack.transform.SetParent(null, true);
        _rackObjects[locationId] = racks;
    }

    private static Vector3 GetRackSpawnPosition(string locationId, int index, StorageRackConfig rack)
    {
        string objectId = index == 0 ? MogulPlacementSystem.Storage0 : "storage_" + index;
        return MogulPlacementSystem.TryGetPlacement(locationId, objectId, out var pos, out _)
            ? pos
            : rack.LocalPos + new Vector3(0f, 0.5f, 0f);
    }

    private static Quaternion GetRackSpawnRotation(string locationId, int index, StorageRackConfig rack)
    {
        string objectId = index == 0 ? MogulPlacementSystem.Storage0 : "storage_" + index;
        return MogulPlacementSystem.TryGetPlacement(locationId, objectId, out _, out var rot)
            ? rot
            : rack.Rotation;
    }


    private static BuildingDesign ApplyDesignOverrides(BuildingDesign design, BuildingOverrides ov)
    {
        return new BuildingDesign
        {
            Id          = design.Id,
            Name        = design.Name,
            Description = design.Description,
            WallMaterial    = ov.WallMaterialName != null
                              ? () => Materials.Find(ov.WallMaterialName)
                              : design.WallMaterial,
            FloorMaterial   = ov.FloorMaterialName != null
                              ? () => Materials.Find(ov.FloorMaterialName)
                              : design.FloorMaterial,
            CeilingMaterial = ov.CeilingMaterialName != null
                              ? () => Materials.Find(ov.CeilingMaterialName)
                              : design.CeilingMaterial,
            TrimMaterial    = ov.TrimMaterialName != null
                              ? () => Materials.Find(ov.TrimMaterialName)
                              : design.TrimMaterial,
            RoofMaterial    = ov.RoofMaterialName != null
                              ? () => Materials.Find(ov.RoofMaterialName)
                              : design.RoofMaterial,
            HipRoof         = ov.RoofStyle == "hip"                     ? true
                            : ov.RoofStyle is "parapet" or "flat"        ? false
                            : design.HipRoof,
            ParapetRoof     = ov.RoofStyle == "parapet"                  ? true
                            : ov.RoofStyle is "hip" or "flat"            ? false
                            : design.ParapetRoof,
            LightColor      = ov.LightColor ?? design.LightColor,
            LightIntensity  = ov.LightIntensity ?? design.LightIntensity,
            WindowOppositeOfDoor = design.WindowOppositeOfDoor,
            PlaceFurniture  = design.PlaceFurniture,
        };
    }

    private static (Vector3 pos, Quaternion rot) GetDoorLocalTransform(Vector3 roomSize, WallSide door)
    {
        float w = roomSize.x, d = roomSize.z;
        return door switch
        {
            WallSide.East => (new Vector3(w, 0f, d / 2f), Quaternion.Euler(0f, 270f, 0f)),
            WallSide.West => (new Vector3(0f, 0f, d / 2f), Quaternion.Euler(0f, 90f, 0f)),
            WallSide.North => (new Vector3(w / 2f, 0f, d), Quaternion.Euler(0f, 180f, 0f)),
            WallSide.South => (new Vector3(w / 2f, 0f, 0f), Quaternion.Euler(0f, 0f, 0f)),
            _ => (new Vector3(w, 0f, d / 2f), Quaternion.Euler(0f, 270f, 0f)),
        };
    }
}
