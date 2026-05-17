using System.Collections.Generic;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building;
using S1MAPI.Building.Structural;
using UnityEngine;
using UnityEngine.AI;

namespace Mogul.Systems;

internal readonly struct QueueSlot
{
    /// <summary>Local-space for interior slots; world-space for exterior slots.</summary>
    public readonly Vector3 Position;
    /// <summary>True → use npc.Movement.SetDestination; False → use navBuilder.SendNPCToPosition.</summary>
    public readonly bool IsExterior;

    public QueueSlot(Vector3 pos, bool exterior) { Position = pos; IsExterior = exterior; }
}

/// <summary>
/// Builds the ordered queue slot list for a location:
///   Slot 0        = counter anchor (interior, local-space)
///   Slots 1..N    = exterior line outside the door (world-space, world NavMesh)
/// </summary>
internal static class QueueSlots
{
    private const float Spacing          = 1.0f;
    private const int   MaxExterior      = 10;
    private const int   BfsMaxVisited    = 500;
    private const float DoorClearWidth   = 1.4f;
    private const float DoorClearDepth   = 1.8f;

    private static readonly Dictionary<string, List<QueueSlot>> _cache = new();

    public static void ClearCache() => _cache.Clear();

    public static int Capacity(MogulLocation location) => 1 + MaxExterior;

    public static QueueSlot Get(int index, MogulLocation location, Vector3 anchor, NavigationBuilder nav)
    {
        if (!_cache.TryGetValue(location.Id, out var slots))
        {
            slots = Compute(anchor, nav, location);
            _cache[location.Id] = slots;
        }
        if (slots.Count == 0) return new QueueSlot(anchor, false);
        return index < slots.Count ? slots[index] : slots[slots.Count - 1];
    }

    private static List<QueueSlot> Compute(Vector3 slot0, NavigationBuilder nav, MogulLocation location)
    {
        var results = new List<QueueSlot>();
        float cell  = nav.CellSize;
        if (cell <= 0f) return results;

        // ── Interior slots ──────────────────────────────────────────────────
        if (!nav.IsWalkable(slot0))
            slot0 = nav.NearestWalkableCell(slot0);
        results.Add(new QueueSlot(slot0, false));

        // ── Exterior slots (world-space) ────────────────────────────────────
        var extSlots = BuildExterior(location);
        results.AddRange(extSlots);

        return results;
    }

    // Exterior queue layout. The line hugs the wall (small perpendicular gap) but steps
    // well clear of the door's exit corridor laterally so leaving NPCs aren't blocked.
    private const float DoorGap           = 0.8f; // perpendicular distance from the wall
    private const float LineLateralOffset = 3.0f; // offset along the wall before slot 0

    private static List<QueueSlot> BuildExterior(MogulLocation location)
    {
        var list    = new List<QueueSlot>(MaxExterior);
        var wallDir = DoorWallDir(location.Door);
        var origin  = DoorExitWorld(location) + wallDir * LineLateralOffset;

        for (int i = 0; i < MaxExterior; i++)
        {
            var worldPos = origin + wallDir * (i * Spacing);
            // Snap to world NavMesh (covers the whole map)
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                worldPos = hit.position;

            list.Add(new QueueSlot(worldPos, true));
        }
        return list;
    }

    // World-space point DoorGap meters outside the door centre.
    private static Vector3 DoorExitWorld(MogulLocation location)
    {
        var c = BuildingPreview.GetEffectiveWorldPosition(location);
        var s = BuildingPreview.GetEffectiveRoomSize(location);
        return location.Door switch
        {
            WallSide.East  => c + new Vector3(s.x + DoorGap, 0f, s.z / 2f),
            WallSide.West  => c + new Vector3(-DoorGap,      0f, s.z / 2f),
            WallSide.North => c + new Vector3(s.x / 2f,      0f, s.z + DoorGap),
            WallSide.South => c + new Vector3(s.x / 2f,      0f, -DoorGap),
            _              => c,
        };
    }

    // Direction to walk along the building wall outside (left when facing the door).
    private static Vector3 DoorWallDir(WallSide door) => door switch
    {
        WallSide.East  => new Vector3( 0, 0, -1), // south along east wall
        WallSide.West  => new Vector3( 0, 0,  1), // north along west wall
        WallSide.North => new Vector3( 1, 0,  0), // east along north wall
        WallSide.South => new Vector3(-1, 0,  0), // west along south wall
        _              => new Vector3( 0, 0, -1),
    };

    // Direction from counter toward the door, in building-local space.
    private static Vector3 DoorLocalDir(WallSide door) => door switch
    {
        WallSide.East  => Vector3.right,
        WallSide.West  => Vector3.left,
        WallSide.North => Vector3.forward,
        WallSide.South => Vector3.back,
        _              => Vector3.right,
    };

    private static Vector3 SnapToGrid(Vector3 p, float cell) =>
        new Vector3((Mathf.FloorToInt(p.x / cell) + 0.5f) * cell, 0f,
                    (Mathf.FloorToInt(p.z / cell) + 0.5f) * cell);

    private static Vector3? FindNextInteriorSlot(
        Vector3 from,
        HashSet<(int, int)> used,
        NavigationBuilder nav,
        float cell,
        MogulLocation location)
    {
        var queue = new Queue<Vector3>();
        var visited = new HashSet<(int, int)>();
        var offsets = new[]
        {
            new Vector3(cell, 0f, 0f),
            new Vector3(-cell, 0f, 0f),
            new Vector3(0f, 0f, cell),
            new Vector3(0f, 0f, -cell),
        };

        queue.Enqueue(from);
        visited.Add(GridKey(from, cell));
        float minSpacingSqr = Spacing * Spacing;

        while (queue.Count > 0 && visited.Count < BfsMaxVisited)
        {
            var current = queue.Dequeue();
            var key = GridKey(current, cell);
            if (!used.Contains(key) && !IsDoorClearanceCell(current, location))
            {
                var delta = current - from;
                if (delta.x * delta.x + delta.z * delta.z >= minSpacingSqr)
                    return current;
            }

            for (int i = 0; i < offsets.Length; i++)
            {
                var neighbor = SnapToGrid(current + offsets[i], cell);
                var nkey = GridKey(neighbor, cell);
                if (visited.Contains(nkey)) continue;
                visited.Add(nkey);
                if (!nav.IsWalkable(neighbor)) continue;
                queue.Enqueue(neighbor);
            }
        }

        return null;
    }

    private static bool IsDoorClearanceCell(Vector3 local, MogulLocation location)
    {
        var s = BuildingPreview.GetEffectiveRoomSize(location);
        return location.Door switch
        {
            WallSide.East  => local.x > s.x - DoorClearDepth && Mathf.Abs(local.z - s.z * 0.5f) < DoorClearWidth,
            WallSide.West  => local.x < DoorClearDepth       && Mathf.Abs(local.z - s.z * 0.5f) < DoorClearWidth,
            WallSide.North => local.z > s.z - DoorClearDepth && Mathf.Abs(local.x - s.x * 0.5f) < DoorClearWidth,
            WallSide.South => local.z < DoorClearDepth       && Mathf.Abs(local.x - s.x * 0.5f) < DoorClearWidth,
            _              => false,
        };
    }

    private static (int, int) GridKey(Vector3 p, float cell) =>
        (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
}
