using System.Collections.Generic;
using Il2CppScheduleOne.Economy;
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
    // Cached per-location spawn anchor (vanilla DeliveryLocation.TeleportPoint nearest to
    // the door, validated outside the room AABB). Falls back to ComputeDoorExterior.
    private static readonly Dictionary<string, Vector3> _spawnAnchorCache = new();

    public static void ClearSpawnAnchorCache() => _spawnAnchorCache.Clear();

    // Resolve a spawn anchor for new customers. Pattern adapted from OTC: scan vanilla
    // DeliveryLocation.TeleportPoints, reject any inside the room AABB, pick the closest
    // to the door. Falls back to door+offset placeholder when none qualify.
    public static Vector3 GetSpawnAnchor(this MogulLocation location)
    {
        if (_spawnAnchorCache.TryGetValue(location.Id, out var cached)) return cached;

        var doorExterior = location.ComputeDoorExterior();
        var picks = Object.FindObjectsOfType<DeliveryLocation>();
        var candidates = new List<Vector3>();
        foreach (var dl in picks)
        {
            if (dl == null || dl.TeleportPoint == null) continue;
            var pos = dl.TeleportPoint.position;
            if (location.IsInsideRoomAABB(pos)) continue;
            candidates.Add(pos);
        }

        var anchor = doorExterior;
        float bestDist = float.MaxValue;
        foreach (var c in candidates)
        {
            float d = Vector3.Distance(doorExterior, c);
            if (d < bestDist) { bestDist = d; anchor = c; }
        }

        _spawnAnchorCache[location.Id] = anchor;
        return anchor;
    }

    // Is the world point inside the location's room AABB? Used to (a) reject vanilla
    // delivery anchors that happen to sit inside our room and (b) validate exit targets.
    public static bool IsInsideRoomAABB(this MogulLocation location, Vector3 worldPos)
    {
        var c = location.WorldPosition;
        var s = location.RoomSize;
        return worldPos.x >= c.x && worldPos.x <= c.x + s.x
            && worldPos.z >= c.z && worldPos.z <= c.z + s.z
            && worldPos.y >= c.y - 1f && worldPos.y <= c.y + s.y + 1f;
    }

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
