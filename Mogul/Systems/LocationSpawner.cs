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

    public static void Initialize()
    {
        // When save data changes (e.g. a purchase), re-check what needs spawning                                            
        MogulNetwork.OnDataChanged += _ => SyncSpawns();
    }
    public static void ClearSpawned() => _spawned.Clear();
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

            var builder = new BuildingBuilder(location.Name)
                .DefineRoom(location.RoomSize.x, location.RoomSize.y, location.RoomSize.z);

            design.Apply(builder, location);

            builder
                .AddFoundation(height: foundationHeight, expandX: 0.25f, expandZ: 0.25f, material: Materials.Find("concrete light beige"))
                .AddStairs(location.Door, foundationHeight: foundationHeight);

            var go = builder.Build();
            go.transform.position = location.WorldPosition - new Vector3(0f, foundationHeight, 0f);
            builder.FlattenTerrain();
            TerrainClearer.ClearAroundBuilding(go, location.RoomSize, new ClearingOptions { Padding = 4f });

            var (doorPos, doorRot) = GetDoorLocalTransform(location.RoomSize, location.Door);

            if (MogulNetwork.IsHost)
            {
                new PrefabPlacer(go.transform).Place(
                Prefabs.ClassicalWoodenDoor, doorPos, doorRot,
                networked: true,
                 onReady: doorGo =>
                {
                    var ctrl = new DoorController(doorGo);
                    ctrl.PlayerAccess = DoorAccess.Open;
                });
            }

            builder.CreateNavigationBuilder().Build();
            _spawned[location.Id] = go;

            var beacon = new GameObject("Beacon_" + location.Id);
            beacon.transform.SetParent(go.transform);
            beacon.transform.localPosition = new Vector3(0f, 15f, 0f);
            var light = beacon.AddComponent<Light>();
            light.color = Color.cyan;
            light.intensity = 8f;
            light.range = 60f;

            MelonLogger.Msg($"[Mogul] Spawned {location.Name} at {location.WorldPosition}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnBuilding failed for {location.Id}: {ex}");
        }

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