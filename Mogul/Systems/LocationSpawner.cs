using System;
using System.Collections.Generic;
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

            var builder = new BuildingBuilder(location.Name)
                .DefineRoom(location.RoomSize.x, location.RoomSize.y, location.RoomSize.z);

            design.Apply(builder, location);

            if (ov != null)
            {
                if (ov.BaseMolding   == true) builder.AddBaseMolding();
                if (ov.CornerPillars == true) builder.AddCornerPillars();
                if (ov.CornerTrim    == true) builder.AddCornerTrim();
                if (ov.RoofTrim      == true) builder.AddRoofTrim();
            }

            builder
                .AddFoundation(height: foundationHeight, expandX: 0.25f, expandZ: 0.25f, material: Materials.Find("concrete light beige"))
                .AddStairs(location.Door, foundationHeight: foundationHeight);

            var go = builder.Build();
            go.transform.position = location.WorldPosition - new Vector3(0f, foundationHeight, 0f);
            builder.FlattenTerrain();
            TerrainClearer.ClearAroundBuilding(go, location.RoomSize, new ClearingOptions { Padding = 4f });

            var (doorPos, doorRot) = GetDoorLocalTransform(location.RoomSize, location.Door);
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
                _rackObjects[locId] = new List<GameObject>();
                foreach (var rack in location.StorageRacks)
                {
                    var pos = rack.LocalPos + new Vector3(0f, 0.5f, 0f);
                    placer.Place(rack.Prefab, pos, rack.Rotation, networked: true,
                        onReady: rackGo =>
                        {
                            if (_rackObjects.TryGetValue(locId, out var list))
                                list.Add(rackGo);
                            var comps = rackGo.GetComponents<UnityEngine.Component>();
                            var names = string.Join(", ", System.Linq.Enumerable.Select(comps, c => c?.GetType().Name ?? "null"));
                            MelonLogger.Msg($"[Mogul] Rack ready for {locId}: {rackGo.name} | components: {names}");
                        });
                }
            }
            var navBuilder = builder.CreateNavigationBuilder();
            navBuilder.Build();
            _navBuilders[location.Id] = navBuilder;
            _spawned[location.Id] = go;
            SellDesk.SyncDesks();

            MelonLogger.Msg($"[Mogul] Spawned {location.Name} at {location.WorldPosition}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnBuilding failed for {location.Id}: {ex}");
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

        if (_spawned.TryGetValue(locationId, out var existing) && existing != null)
            UnityEngine.Object.Destroy(existing);
        _spawned.Remove(locationId);
        _navBuilders.Remove(locationId);
        _rackObjects.Remove(locationId);

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
            FloorMaterial   = design.FloorMaterial,
            CeilingMaterial = design.CeilingMaterial,
            TrimMaterial    = design.TrimMaterial,
            HipRoof         = ov.RoofStyle == "hip"                     ? true
                            : ov.RoofStyle is "parapet" or "flat"        ? false
                            : design.HipRoof,
            ParapetRoof     = ov.RoofStyle == "parapet"                  ? true
                            : ov.RoofStyle is "hip" or "flat"            ? false
                            : design.ParapetRoof,
            LightColor      = ov.LightColor ?? design.LightColor,
            LightIntensity  = design.LightIntensity,
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