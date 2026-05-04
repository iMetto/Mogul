using Mogul.Data;
using S1MAPI.Building;
using S1MAPI.Building.Structural;
using UnityEngine;

namespace Mogul.Systems;

// World-space geometry helpers for MogulLocations.
// Pure-ish: ComputeDoorExterior is a pure function of the location data; the Try*
// helpers reach into LocationSpawner / SellDesk / PropertySystem for runtime state.
public static class LocationGeometry
{
    // Spawn anchor for new customers: a fixed offset outside the door, on the building's
    // door-facing wall. Used by CustomerSpawner — keep distinct from QueueSlots' interior
    // exit point (which uses a smaller 1.5m offset for the queue line start).
    public static Vector3 ComputeDoorExterior(this MogulLocation location)
    {
        const float offset = 3f;
        var c = location.WorldPosition;
        var s = location.RoomSize;
        return location.Door switch
        {
            WallSide.North => c + new Vector3(s.x / 2f, 0f, s.z + offset),
            WallSide.South => c + new Vector3(s.x / 2f, 0f, -offset),
            WallSide.East  => c + new Vector3(s.x + offset, 0f, s.z / 2f),
            WallSide.West  => c + new Vector3(-offset, 0f, s.z / 2f),
            _              => c,
        };
    }

    // World position of the sell-desk queue anchor, transformed from the building's
    // local-space anchor through the spawned building's transform.
    // Returns false if the building isn't spawned or the anchor isn't registered.
    public static bool TryGetCounterWorldPos(string locationId, out Vector3 worldPos)
    {
        worldPos = default;
        if (!SellDesk.TryGetQueueAnchor(locationId, out var localAnchor)) return false;
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var root)) return false;
        worldPos = root.transform.TransformPoint(localAnchor);
        return true;
    }

    // Closest owned + spawned location to the given player position.
    // Skips unowned locations and locations whose buildings haven't been spawned yet.
    public static bool TryFindNearestLocation(Vector3 playerPos, out MogulLocation nearest)
    {
        nearest = null;
        float bestDist = float.MaxValue;
        foreach (var loc in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(loc.Id)) continue;
            if (!LocationSpawner.TryGetSpawnedBuilding(loc.Id, out _)) continue;
            float dist = Vector3.Distance(playerPos, loc.WorldPosition);
            if (dist < bestDist) { bestDist = dist; nearest = loc; }
        }
        return nearest != null;
    }
}
