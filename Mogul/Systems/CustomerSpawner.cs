using System;
using Il2CppFishNet;
using Il2CppFishNet.Object;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.Product;
using MelonLoader;
using Mogul.Data;
using UnityEngine;
using UnityEngine.AI;
using LayerList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.AvatarFramework.AvatarSettings.LayerSetting>;
using AccessoryList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.AvatarFramework.AvatarSettings.AccessorySetting>;

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
            SpawnAt(position, _nextId++, "Customer", "Mogul_Customer_", addCustomerComponent: true, onSpawned);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Mogul] SpawnTestNPC failed: " + ex);
        }
    }

    public static void SpawnWorkerNPC(Vector3 position, EmployeeRole role, string displayName, Action<NPC> onSpawned = null)
    {
        if (!MogulNetwork.IsHost)
        {
            MelonLogger.Warning("[Mogul] SpawnWorkerNPC: host only");
            return;
        }

        if (_basePrefab == null && !TryFindCivilianPrefab(out _basePrefab))
        {
            MelonLogger.Warning("[Mogul] SpawnWorkerNPC: CivilianNPC prefab not found");
            return;
        }

        try
        {
            int id = _nextId++;
            SpawnAt(position, id, displayName, "Mogul_Worker_", addCustomerComponent: false, npc =>
            {
                npc.ID = "mogul_worker_" + id;
                var parts = (displayName ?? role.ToString()).Split(' ');
                npc.FirstName = parts.Length > 0 ? parts[0] : role.ToString();
                npc.LastName = parts.Length > 1 ? parts[1] : role.ToString();
                ApplyWorkerAppearance(npc, unchecked((displayName ?? role.ToString()).GetHashCode()), role);
                onSpawned?.Invoke(npc);
            });
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Mogul] SpawnWorkerNPC failed: " + ex);
        }
    }

    public static void Despawn(NPC npc)
    {
        try { NPCManager.NPCRegistry?.Remove(npc); } catch { }

        var go = npc?.gameObject;
        if (go == null) return;

        // Network despawn first (host-authoritative). Whether or not this destroys the
        // object varies — it sometimes only marks it for cleanup. We follow up with an
        // explicit Destroy so the GameObject can never linger in the world.
        try
        {
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && InstanceFinder.ServerManager != null && netObj.IsSpawned)
                InstanceFinder.ServerManager.Despawn(netObj);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] ServerManager.Despawn failed: " + ex.Message);
        }

        try { if (go != null) UnityEngine.Object.Destroy(go); }
        catch (Exception ex) { MelonLogger.Warning("[Mogul] Destroy failed: " + ex.Message); }
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
                return true;
            }
        }
        return false;
    }

    private static void SpawnAt(Vector3 worldPos, int id, string displayName, string objectPrefix,
        bool addCustomerComponent, Action<NPC> onSpawned = null)
    {
        // Snap to NavMesh
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            worldPos = hit.position;

        // Clone prefab while inactive so AddComponent works before Awake
        var clone = UnityEngine.Object.Instantiate(_basePrefab);
        clone.name = objectPrefix + id;
        clone.SetActive(false);

        // Parent to NPCContainer
        var npcMgr = NetworkSingleton<NPCManager>.Instance;
        if (npcMgr?.NPCContainer != null)
            clone.transform.SetParent(npcMgr.NPCContainer, false);

        clone.transform.position = worldPos;

        // Identity
        var npc = clone.GetComponent<NPC>();
        npc.ID = addCustomerComponent ? "mogul_customer_" + id : "mogul_worker_" + id;
        npc.FirstName = displayName ?? "Customer";
        npc.LastName = id.ToString();

        // Remove from auto-registered list — we manage lifecycle
        NPCManager.NPCRegistry?.Remove(npc);

        if (addCustomerComponent)
        {
            // Add Customer component while inactive so S1API can wrap it after network spawn
            var customerComp = clone.AddComponent<Customer>();
            customerComp.enabled = true;
            AssignCustomerData(customerComp, id);
        }

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
        if (agent != null)
        {
            agent.autoTraverseOffMeshLink = true;
            // Lower avoidancePriority = higher priority. Vanilla NPCs default to 50;
            // setting ours to 30 makes vanilla NPCs yield to our customers when paths cross.
            agent.avoidancePriority = 30;
        }

        onSpawned?.Invoke(npc);
    }

    private static void AssignCustomerData(Customer customerComp, int seed)
    {
        var rng = new System.Random(unchecked(seed ^ Environment.TickCount));

        // Walk-in crowd skews toward lower tiers; Phase 2 will weight this by player reach
        int roll = rng.Next(10);
        var standard = roll < 2 ? ECustomerStandard.VeryLow
                     : roll < 5 ? ECustomerStandard.Low
                     : roll < 8 ? ECustomerStandard.Moderate
                     : roll < 9 ? ECustomerStandard.High
                                : ECustomerStandard.VeryHigh;

        (float min, float max) weeklySpend = standard switch
        {
            ECustomerStandard.VeryLow  => (160f,  300f),
            ECustomerStandard.Low      => (300f,  600f),
            ECustomerStandard.Moderate => (600f,  1000f),
            ECustomerStandard.High     => (1000f, 1800f),
            _                          => (1800f, 3000f),
        };

        var data = ScriptableObject.CreateInstance<CustomerData>();
        data.Standards       = standard;
        data.MinWeeklySpend  = weeklySpend.min;
        data.MaxWeeklySpend  = weeklySpend.max;
        data.MinOrdersPerWeek = 1;
        data.MaxOrdersPerWeek = 3;

        data.DefaultAffinityData = new CustomerAffinityData();
        foreach (EDrugType dt in System.Enum.GetValues(typeof(EDrugType)))
        {
            data.DefaultAffinityData.ProductAffinities.Add(new ProductTypeAffinity
            {
                DrugType = dt,
                Affinity = (float)(rng.NextDouble() * 2.0 - 1.0),
            });
        }

        customerComp.customerData = data;
    }

    // Pool data (paths verified in vanilla via OTC reference). All entries excluded
    // police uniforms / hats so customers don't read as cops.
    private static readonly Color[] SkinTones =
    {
        new(0.96f, 0.87f, 0.78f), new(0.92f, 0.80f, 0.70f),
        new(0.85f, 0.70f, 0.55f), new(0.75f, 0.58f, 0.42f),
        new(0.65f, 0.48f, 0.35f), new(0.55f, 0.40f, 0.28f),
        new(0.48f, 0.35f, 0.25f), new(0.42f, 0.30f, 0.22f),
    };

    private static readonly string[] FaceExpressions =
    {
        "Avatar/Layers/Face/Face_Neutral",
        "Avatar/Layers/Face/Face_NeutralPout",
        "Avatar/Layers/Face/Face_SlightSmile",
        "Avatar/Layers/Face/Face_SlightFrown",
        "Avatar/Layers/Face/Face_SmugPout",
    };

    private static readonly string[] Shirts =
    {
        "Avatar/Layers/Top/T-Shirt",
        "Avatar/Layers/Top/V-Neck",
        "Avatar/Layers/Top/Buttonup",
        "Avatar/Layers/Top/RolledButtonUp",
        "Avatar/Layers/Top/FlannelButtonUp",
        "Avatar/Layers/Top/Tucked T-Shirt",
    };

    private static readonly string[] Pants =
    {
        "Avatar/Layers/Bottom/Jeans",
        "Avatar/Layers/Bottom/CargoPants",
        "Avatar/Layers/Bottom/Jorts",
    };

    private static readonly string[] MaleHair =
    {
        "Avatar/Hair/buzzcut/BuzzCut", "Avatar/Hair/closebuzzcut/CloseBuzzCut",
        "Avatar/Hair/franklin/Franklin", "Avatar/Hair/spiky/Spiky",
        "Avatar/Hair/peaked/Peaked", "Avatar/Hair/tony/Tony",
        "Avatar/Hair/mohawk/Mohawk", "Avatar/Hair/receding/Receding",
        "Avatar/Hair/afro/Afro", "Avatar/Hair/bowlcut/BowlCut",
    };

    private static readonly string[] FemaleHair =
    {
        "Avatar/Hair/bun/Bun", "Avatar/Hair/highbun/HighBun",
        "Avatar/Hair/lowbun/LowBun", "Avatar/Hair/fringeponytail/FringePonyTail",
        "Avatar/Hair/messybob/MessyBob", "Avatar/Hair/sidepartbob/SidePartBob",
        "Avatar/Hair/shoulderlength/ShoulderLength", "Avatar/Hair/longcurly/LongCurly",
        "Avatar/Hair/doubletopknot/DoubleTopKnot", "Avatar/Hair/afro/Afro",
        "Avatar/Hair/midfringe/MidFringe",
    };

    private static readonly string[] Shoes =
    {
        "Avatar/Accessories/Feet/Sneakers/Sneakers",
        "Avatar/Accessories/Feet/CombatBoots/CombatBoots",
        "Avatar/Accessories/Feet/DressShoes/DressShoes",
        "Avatar/Accessories/Feet/Sandals/Sandals",
    };

    private static readonly string[] CapFriendlyMaleHair =
    {
        "Avatar/Hair/buzzcut/BuzzCut",
        "Avatar/Hair/closebuzzcut/CloseBuzzCut",
        "Avatar/Hair/receding/Receding",
    };

    private static readonly string[] CapFriendlyFemaleHair =
    {
        "Avatar/Hair/bun/Bun",
        "Avatar/Hair/lowbun/LowBun",
        "Avatar/Hair/closebuzzcut/CloseBuzzCut",
    };

    private static void ApplyAppearance(NPC npc, int seed)
    {
        if (npc.Avatar == null) return;

        // Re-seed every spawn so the same sequential `id` doesn't always yield the same NPC.
        // (`seed` is just _nextId++ — alone, the first customer of a session is always the same.)
        int rngSeed = unchecked(seed ^ Environment.TickCount);
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(rngSeed);
        bool isFemale;
        try
        {
            var settings = ScriptableObject.CreateInstance<AvatarSettings>();
            settings.FaceLayerSettings = new LayerList();
            settings.BodyLayerSettings = new LayerList();
            settings.AccessorySettings = new AccessoryList();

            settings.Gender = UnityEngine.Random.Range(0f, 1f);
            isFemale = settings.Gender >= 0.5f;

            var skinTone = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
            settings.SkinColor = skinTone;
            // Face tint slightly darker than skin for shading depth
            var faceTint = new Color(skinTone.r * 0.92f, skinTone.g * 0.88f, skinTone.b * 0.85f);

            settings.Height = UnityEngine.Random.Range(0.9f, 1.1f);
            settings.Weight = UnityEngine.Random.Range(0.3f, 0.7f);

            // Hair — natural tones, gender-pooled style
            float hairHue = UnityEngine.Random.value;
            settings.HairColor = hairHue < 0.4f ? new Color(0.10f, 0.08f, 0.06f)   // black/dark brown
                               : hairHue < 0.7f ? new Color(0.35f, 0.22f, 0.12f)   // brown
                               : hairHue < 0.9f ? new Color(0.70f, 0.55f, 0.35f)   // blonde
                                                : new Color(0.55f, 0.25f, 0.15f);  // red
            var hairPool = isFemale ? FemaleHair : MaleHair;
            settings.HairPath = hairPool[UnityEngine.Random.Range(0, hairPool.Length)];

            // Eyes — without these, face renders black or eyes never load
            settings.EyeBallTint = Color.white;
            settings.PupilDilation = UnityEngine.Random.Range(0.5f, 0.8f);
            settings.LeftEyeLidColor = skinTone;
            settings.RightEyeLidColor = skinTone;
            var lidConfig = new Eye.EyeLidConfiguration { topLidOpen = 0.5f, bottomLidOpen = 0.5f };
            settings.LeftEyeRestingState = lidConfig;
            settings.RightEyeRestingState = lidConfig;
            settings.EyebrowScale = UnityEngine.Random.Range(0.8f, 1.1f);
            settings.EyebrowThickness = UnityEngine.Random.Range(0.7f, 1.2f);

            // Face expression
            settings.FaceLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = FaceExpressions[UnityEngine.Random.Range(0, FaceExpressions.Length)],
                layerTint = faceTint,
            });

            // Clothing
            settings.BodyLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = Shirts[UnityEngine.Random.Range(0, Shirts.Length)],
                layerTint = RandomClothingColor(),
            });
            settings.BodyLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = Pants[UnityEngine.Random.Range(0, Pants.Length)],
                layerTint = RandomClothingColor(),
            });

            // Shoes (accessory)
            settings.AccessorySettings.Add(new AvatarSettings.AccessorySetting
            {
                path = Shoes[UnityEngine.Random.Range(0, Shoes.Length)],
                color = RandomClothingColor(),
            });

            npc.Avatar.LoadAvatarSettings(settings);
        }
        finally
        {
            UnityEngine.Random.state = prevState;
        }

        // Voice — borrow from EmployeeManager. Pitch jittered so two same-voice NPCs sound distinct.
        var empMgr = NetworkSingleton<EmployeeManager>.Instance;
        if (empMgr != null)
        {
            var voiceDb = empMgr.GetVoice(!isFemale, Math.Abs(rngSeed % 100));
            if (voiceDb != null)
            {
                npc.VoiceOverEmitter?.SetDatabase(voiceDb, true);
                float pitch = (isFemale ? 1.1f : 0.9f) + ((rngSeed & 0xff) / 255f - 0.5f) * 0.2f;
                npc.VoiceOverEmitter.PitchMultiplier = pitch;
            }
        }
    }

    private static void ApplyWorkerAppearance(NPC npc, int seed, EmployeeRole role)
    {
        if (npc.Avatar == null) return;

        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(seed);
        bool isFemale;
        try
        {
            var settings = ScriptableObject.CreateInstance<AvatarSettings>();
            settings.FaceLayerSettings = new LayerList();
            settings.BodyLayerSettings = new LayerList();
            settings.AccessorySettings = new AccessoryList();

            settings.Gender = UnityEngine.Random.Range(0f, 1f);
            isFemale = settings.Gender >= 0.5f;

            var skinTone = SkinTones[UnityEngine.Random.Range(0, SkinTones.Length)];
            settings.SkinColor = skinTone;
            var faceTint = new Color(skinTone.r * 0.92f, skinTone.g * 0.88f, skinTone.b * 0.85f);

            settings.Height = UnityEngine.Random.Range(0.93f, 1.08f);
            settings.Weight = UnityEngine.Random.Range(0.3f, 0.6f);
            settings.HairColor = RandomHairColor();
            var hairPool = isFemale ? CapFriendlyFemaleHair : CapFriendlyMaleHair;
            settings.HairPath = hairPool[UnityEngine.Random.Range(0, hairPool.Length)];

            settings.EyeBallTint = Color.white;
            settings.PupilDilation = UnityEngine.Random.Range(0.5f, 0.8f);
            settings.LeftEyeLidColor = skinTone;
            settings.RightEyeLidColor = skinTone;
            var lidConfig = new Eye.EyeLidConfiguration { topLidOpen = 0.5f, bottomLidOpen = 0.5f };
            settings.LeftEyeRestingState = lidConfig;
            settings.RightEyeRestingState = lidConfig;
            settings.EyebrowScale = UnityEngine.Random.Range(0.8f, 1.1f);
            settings.EyebrowThickness = UnityEngine.Random.Range(0.7f, 1.2f);

            var (shirt, cap) = WorkerColors(role);

            settings.FaceLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = FaceExpressions[UnityEngine.Random.Range(0, FaceExpressions.Length)],
                layerTint = faceTint,
            });
            settings.BodyLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = "Avatar/Layers/Top/Tucked T-Shirt",
                layerTint = shirt,
            });
            settings.BodyLayerSettings.Add(new AvatarSettings.LayerSetting
            {
                layerPath = "Avatar/Layers/Bottom/Jeans",
                layerTint = new Color(0.12f, 0.12f, 0.14f),
            });
            settings.AccessorySettings.Add(new AvatarSettings.AccessorySetting
            {
                path = "Avatar/Accessories/Head/Cap/Cap",
                color = cap,
            });
            settings.AccessorySettings.Add(new AvatarSettings.AccessorySetting
            {
                path = "Avatar/Accessories/Feet/Sneakers/Sneakers",
                color = new Color(0.15f, 0.15f, 0.15f),
            });

            npc.Avatar.LoadAvatarSettings(settings);
        }
        finally
        {
            UnityEngine.Random.state = prevState;
        }
    }

    private static Color RandomHairColor()
    {
        float hairHue = UnityEngine.Random.value;
        return hairHue < 0.4f ? new Color(0.10f, 0.08f, 0.06f)
             : hairHue < 0.7f ? new Color(0.35f, 0.22f, 0.12f)
             : hairHue < 0.9f ? new Color(0.70f, 0.55f, 0.35f)
                              : new Color(0.55f, 0.25f, 0.15f);
    }

    private static (Color shirt, Color cap) WorkerColors(EmployeeRole role) => role switch
    {
        EmployeeRole.Cashier   => (new Color(0.18f, 0.52f, 0.22f), new Color(0.15f, 0.45f, 0.18f)),
        EmployeeRole.Budtender => (new Color(0.22f, 0.42f, 0.68f), new Color(0.16f, 0.30f, 0.50f)),
        EmployeeRole.Runner    => (new Color(0.55f, 0.40f, 0.18f), new Color(0.42f, 0.30f, 0.14f)),
        _                      => (new Color(0.18f, 0.52f, 0.22f), new Color(0.15f, 0.45f, 0.18f)),
    };

    private static Color RandomClothingColor()
    {
        // Muted, realistic palette — grays, blues, earth tones, occasional brights.
        float pick = UnityEngine.Random.value;
        if (pick < 0.25f) { float g = UnityEngine.Random.Range(0.1f, 0.5f); return new Color(g, g, g); }
        if (pick < 0.55f) return new Color(UnityEngine.Random.Range(0.1f, 0.3f),
                                           UnityEngine.Random.Range(0.15f, 0.35f),
                                           UnityEngine.Random.Range(0.3f, 0.5f));
        if (pick < 0.85f) return new Color(UnityEngine.Random.Range(0.3f, 0.55f),
                                           UnityEngine.Random.Range(0.25f, 0.45f),
                                           UnityEngine.Random.Range(0.15f, 0.3f));
        return new Color(UnityEngine.Random.Range(0.4f, 0.8f),
                         UnityEngine.Random.Range(0.2f, 0.5f),
                         UnityEngine.Random.Range(0.2f, 0.5f));
    }
}
