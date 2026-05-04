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
///   Slots 1..N-1  = step toward door along interior navmesh (local-space)
///   Slots N+      = extend 1m outside door, then along wall (world-space, world NavMesh)
/// </summary>
internal static class QueueSlots
{
    private const float Spacing          = 1.0f;
    private const int   MaxInterior      = 8;
    private const int   MaxExterior      = 6;

    private static readonly Dictionary<string, List<QueueSlot>> _cache = new();

    public static void ClearCache() => _cache.Clear();

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

        // Step in the direction the desk faces (toward customers), falling back to door direction.
        Vector3 inDir = location.DeskRotation != default
            ? location.DeskRotation * Vector3.forward
            : DoorLocalDir(location.Door);

        while (results.Count <= location.MaxInteriorSlots)
        {
            var prev      = results[results.Count - 1].Position;
            var candidate = SnapToGrid(prev + inDir * Spacing, cell);

            if (!nav.IsWalkable(candidate))
                candidate = nav.NearestWalkableCell(candidate);

            if (!nav.IsWalkable(candidate)) break;
            // Stop if we didn't actually advance (navmesh edge / wall)
            if (Mathf.Abs(Vector3.Dot(candidate - prev, inDir)) < Spacing * 0.4f) break;

            results.Add(new QueueSlot(candidate, false));
            MelonLogger.Msg($"[Mogul] [{location.Id}] interior slot {results.Count - 1}: {candidate}");
        }

        // ── Exterior slots (world-space) ────────────────────────────────────
        var extSlots = BuildExterior(location);
        results.AddRange(extSlots);

        MelonLogger.Msg($"[Mogul] [{location.Id}] slots: {results.Count} ({results.Count - extSlots.Count} interior, {extSlots.Count} exterior)");
        return results;
    }

    // Auto-computes exterior positions: 1.5m outside the door, then along the wall.
    private static List<QueueSlot> BuildExterior(MogulLocation location)
    {
        var list    = new List<QueueSlot>(MaxExterior);
        var origin  = DoorExitWorld(location);      // world-space start, just outside door
        var wallDir = DoorWallDir(location.Door);   // direction to extend along the wall

        for (int i = 0; i < MaxExterior; i++)
        {
            var worldPos = origin + wallDir * (i * Spacing);
            // Snap to world NavMesh (covers the whole map)
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                worldPos = hit.position;

            list.Add(new QueueSlot(worldPos, true));
            MelonLogger.Msg($"[Mogul] [{location.Id}] exterior slot {i}: {worldPos}");
        }
        return list;
    }

    // World-space point 1.5m outside the door centre.
    private static Vector3 DoorExitWorld(MogulLocation location)
    {
        var c = location.WorldPosition;
        var s = location.RoomSize;
        const float gap = 1.5f;
        return location.Door switch
        {
            WallSide.East  => c + new Vector3(s.x + gap, 0f, s.z / 2f),
            WallSide.West  => c + new Vector3(-gap,      0f, s.z / 2f),
            WallSide.North => c + new Vector3(s.x / 2f,  0f, s.z + gap),
            WallSide.South => c + new Vector3(s.x / 2f,  0f, -gap),
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
}
