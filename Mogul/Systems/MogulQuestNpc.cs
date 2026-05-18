using System;
using System.Collections.Generic;
using Il2CppScheduleOne.NPCs;
using MelonLoader;
using UnityEngine;

namespace Mogul.Systems;

public static class MogulQuestNpcSpawner
{
    private static readonly Dictionary<string, NPC> _active = new();
    private static readonly HashSet<string> _recorded = new();
    private static readonly Dictionary<string, float> _pendingDespawn = new();

    private const float DespawnDelay = 10f;

    public static void Tick()
    {
        if (!MogulNetwork.IsHost) return;
        var data = MogulNetwork.Data;
        if (data == null) return;

        foreach (var quest in MogulQuestSystem.All)
        {
            if (quest.Event != MogulObjectiveEvent.KnockoutNpc) continue;
            if (string.IsNullOrEmpty(quest.TargetId)) continue;
            if (quest.WorldPosition == Vector3.zero) continue;

            bool claimed   = MogulQuestSystem.IsClaimed(quest, data);
            bool available = quest.IsAvailable(data);
            bool accepted  = MogulQuestSystem.IsAccepted(quest, data);

            if (claimed || !available || !accepted)
            {
                Despawn(quest.TargetId);
                continue;
            }

            if (_active.TryGetValue(quest.TargetId, out var existing) && existing != null)
                continue;

            SpawnForQuest(quest);
        }

        foreach (var kvp in new Dictionary<string, float>(_pendingDespawn))
        {
            if (Time.time >= kvp.Value)
            {
                _pendingDespawn.Remove(kvp.Key);
                Despawn(kvp.Key);
            }
        }
    }

    private static void SpawnForQuest(MogulQuestDefinition quest)
    {
        _active[quest.TargetId] = null; // lock against re-entry this frame
        var targetId = quest.TargetId;
        var evt      = quest.Event;

        NpcSpawner.SpawnQuestNpc(targetId, "Mark", "Target", quest.WorldPosition, npc =>
        {
            _active[targetId] = npc;
            var health = npc.gameObject.GetComponent<NPCHealth>();
            if (health == null) return;

            health.onDieOrKnockedOut.AddListener(new Action(() =>
            {
                if (_recorded.Contains(targetId)) return;
                _recorded.Add(targetId);
                _pendingDespawn[targetId] = Time.time + DespawnDelay;
                MogulQuestSystem.RequestRecordEvent(evt, targetId);
            }));
        });
    }

    public static void Despawn(string targetId)
    {
        if (!_active.TryGetValue(targetId, out var npc)) return;
        _active.Remove(targetId);
        if (npc != null)
            NpcSpawner.Despawn(npc);
    }

    public static void DespawnAll()
    {
        foreach (var kvp in _active)
        {
            if (kvp.Value != null)
                NpcSpawner.Despawn(kvp.Value);
        }
        _active.Clear();
        _recorded.Clear();
        _pendingDespawn.Clear();
    }
}
