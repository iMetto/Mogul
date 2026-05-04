using System;
using Il2CppFishNet;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Behaviour;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Mogul.Systems;

public static class CustomerSpawner
{
    private static int _nextId = 9000;

    private static GameObject _basePrefab;

    public static void SpawnTestNPC(Vector3 position, Action<NPC> onSpawned = null)
    {
        if (!MogulNetwork.IsHost)
        {
            MelonLogger.Warning("[Mogul] SpawnTestNPC: host only");
            return;
        }

        if (_basePrefab == null && !TryFindCivilianPrefab(out _basePrefab))
        {
            MelonLogger.Warning("[Mogul] SpawnTestNPC: CivilianNPC prefab not found");
            return;
        }

        try
        {
            SpawnAt(position, _nextId++, onSpawned);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Mogul] SpawnTestNPC failed: " + ex);
        }
    }

    public static void Despawn(NPC npc)
    {
        try { NPCManager.NPCRegistry?.Remove(npc); } catch { }
        try
        {
            var netObj = npc.gameObject?.GetComponent<NetworkObject>();
            if (netObj != null && InstanceFinder.ServerManager != null && netObj.IsSpawned)
                InstanceFinder.ServerManager.Despawn(netObj);
            else if (npc.gameObject != null)
                UnityEngine.Object.Destroy(npc.gameObject);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] Despawn error (safe to ignore): " + ex.Message);
        }
    }

    private static bool TryFindCivilianPrefab(out GameObject prefab)
    {
        prefab = null;
        var nm = InstanceFinder.NetworkManager;
        if (nm == null) return false;

        var spawnables = nm.SpawnablePrefabs;
        int count = spawnables.GetObjectCount();
        for (int i = 0; i < count; i++)
        {
            var obj = spawnables.GetObject(true, i);
            if (obj?.gameObject?.name == "CivilianNPC")
            {
                prefab = obj.gameObject;
                MelonLogger.Msg("[Mogul] CivilianNPC prefab found at index " + i);
                return true;
            }
        }
        return false;
    }

    private static void SpawnAt(Vector3 worldPos, int id, Action<NPC> onSpawned = null)
    {
        // Snap to NavMesh
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            worldPos = hit.position;

        // Clone prefab while inactive so AddComponent works before Awake
        var clone = UnityEngine.Object.Instantiate(_basePrefab);
        clone.name = "Mogul_Customer_" + id;
        clone.SetActive(false);

        // Parent to NPCContainer
        var npcMgr = NetworkSingleton<NPCManager>.Instance;
        if (npcMgr?.NPCContainer != null)
            clone.transform.SetParent(npcMgr.NPCContainer, false);

        clone.transform.position = worldPos;

        // Identity
        var npc = clone.GetComponent<NPC>();
        npc.ID = "mogul_customer_" + id;
        npc.FirstName = "Customer";
        npc.LastName = id.ToString();

        // Remove from auto-registered list — we manage lifecycle
        NPCManager.NPCRegistry?.Remove(npc);

        // Apply appearance before activation
        ApplyAppearance(npc, id);

        // Activate — triggers Awake/Start
        clone.SetActive(true);

        // Network spawn
        InstanceFinder.ServerManager.Spawn(clone);

        // Warp to position and stop
        npc.Movement?.Warp(worldPos);
        npc.Movement?.Stop();
        npc.Movement?.SetAgentType(NPCMovement.EAgentType.Humanoid);

        var agent = clone.GetComponent<NavMeshAgent>();
        if (agent != null) agent.autoTraverseOffMeshLink = true;

        MelonLogger.Msg($"[Mogul] Spawned customer {id} at {worldPos}");
        onSpawned?.Invoke(npc);
    }

    private static void ApplyAppearance(NPC npc, int seed)
    {
        if (npc.Avatar == null) return;

        var rng = new System.Random(seed);
        bool isMale = rng.NextDouble() < 0.5;
        float gender = isMale ? 0.1f : 0.9f;

        // Skin tones: 8 options from very light to very dark
        Color[] skins =
        {
            new Color(1.00f, 0.87f, 0.76f), new Color(0.95f, 0.76f, 0.60f),
            new Color(0.87f, 0.67f, 0.47f), new Color(0.76f, 0.55f, 0.35f),
            new Color(0.62f, 0.40f, 0.23f), new Color(0.48f, 0.29f, 0.15f),
            new Color(0.34f, 0.19f, 0.09f), new Color(0.22f, 0.13f, 0.06f),
        };
        var skinColor = skins[rng.Next(skins.Length)];

        string[] maleHair  = { "Avatar/Hair/buzzcut/BuzzCut", "Avatar/Hair/afro/Afro", "Avatar/Hair/shortback/ShortBack" };
        string[] femaleHair = { "Avatar/Hair/bun/Bun", "Avatar/Hair/afro/Afro", "Avatar/Hair/longstraight/LongStraight" };
        string hairPath = isMale
            ? maleHair[rng.Next(maleHair.Length)]
            : femaleHair[rng.Next(femaleHair.Length)];

        var settings = ScriptableObject.CreateInstance<AvatarSettings>();
        settings.Gender     = gender;
        settings.SkinColor  = skinColor;
        settings.HairPath   = hairPath;
        settings.Height     = 0.9f + (float)rng.NextDouble() * 0.2f;
        settings.Weight     = 0.3f + (float)rng.NextDouble() * 0.4f;

        // Face expression — REQUIRED, prevents black face
        var faceList = new Il2CppSystem.Collections.Generic.List<AvatarSettings.LayerSetting>();
        faceList.Add(new AvatarSettings.LayerSetting { layerPath = "Avatar/Layers/Face/Face_Neutral", layerTint = Color.white });
        settings.FaceLayerSettings = faceList;

        // Basic clothing
        var bodyList = new Il2CppSystem.Collections.Generic.List<AvatarSettings.LayerSetting>();
        bodyList.Add(new AvatarSettings.LayerSetting { layerPath = "Avatar/Layers/Top/T-Shirt",   layerTint = Color.white });
        bodyList.Add(new AvatarSettings.LayerSetting { layerPath = "Avatar/Layers/Bottom/Jeans",  layerTint = Color.white });
        settings.BodyLayerSettings = bodyList;

        npc.Avatar.LoadAvatarSettings(settings);

        // Voice — borrow from EmployeeManager
        var empMgr = NetworkSingleton<EmployeeManager>.Instance;
        if (empMgr != null)
        {
            var voiceDb = empMgr.GetVoice(isMale, Math.Abs(seed % 100));
            if (voiceDb != null)
            {
                npc.VoiceOverEmitter?.SetDatabase(voiceDb, true);
                float pitch = isMale ? 0.8f : 1.3f;
                pitch += -0.1f + (float)(seed % 10) / 10f * 0.2f;
                npc.VoiceOverEmitter.PitchMultiplier = pitch;
            }
        }
    }
}
