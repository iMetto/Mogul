using System;
using System.Collections.Generic;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building.Components;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Config;
using S1MAPI.Building.Structural;
using S1MAPI.Gltf;
using S1MAPI.S1;
using S1MAPI.Utils;
using UnityEngine;
using Il2CppScheduleOne.Interaction;

namespace Mogul.Systems;

public static class SellDesk
{
    private class SellDeskInstance
    {
        public GameObject Counter;         // the counter surface GO
        public Vector3 QueueAnchor;
        public InteractableObject RegisterInteractable;  // cash collection point
        public InteractableObject CounterInteractable;   // "take order" point
    }

    private static readonly Dictionary<string, SellDeskInstance> _spawned = new();

    public static void Initialize()
    {
        // Direct hook: whenever a building is ready (including late-joining clients),
        // spawn its desk immediately rather than relying on SyncDesks timing.
        LocationSpawner.OnBuildingReady += (locationId, buildingRoot) =>
        {
            var location = PropertySystem.Find(locationId);
            if (location != null && !_spawned.ContainsKey(locationId))
                SpawnDesk(location, buildingRoot);
        };

        MogulNetwork.OnDataChanged += _ => SyncDesks();
    }

    public static bool TryGetQueueAnchor(string locationId, out Vector3 worldPos)
    {
        if (_spawned.TryGetValue(locationId, out var inst))
        {
            worldPos = inst.QueueAnchor;
            return true;
        }
        worldPos = default;
        return false;
    }

    public static bool TryGetCounterObject(string locationId, out GameObject counter)
    {
        if (_spawned.TryGetValue(locationId, out var inst) && inst.Counter != null)
        {
            counter = inst.Counter;
            return true;
        }

        counter = null;
        return false;
    }

    public static bool TryGetStaffAnchor(string locationId, out Vector3 localPos)
    {
        if (MogulPlacementSystem.TryGetPlacement(locationId, MogulPlacementSystem.Cashier, out var savedPos, out _))
        {
            localPos = savedPos;
            return true;
        }

        var location = PropertySystem.Find(locationId);
        if (location != null)
        {
            localPos = ComputeStaffAnchor(location);
            return true;
        }

        localPos = default;
        return false;
    }

    public static bool TryGetDefaultStaffAnchor(string locationId, out Vector3 localPos, out Quaternion localRot)
    {
        var location = PropertySystem.Find(locationId);
        if (location == null)
        {
            localPos = default;
            localRot = default;
            return false;
        }

        localPos = ComputeStaffAnchor(location);
        if (location.SellDesk.HasStaffLocalRotation)
            localRot = location.SellDesk.StaffLocalRotation;
        else
        {
            var (_, deskRot) = ComputeBaseDeskTransform(location);
            localRot = Quaternion.LookRotation(deskRot * Vector3.forward, Vector3.up);
        }
        return true;
    }

    public static bool TryGetRegister(string locationId, out GameObject register)
    {
        if (_spawned.TryGetValue(locationId, out var inst) && inst.Counter != null)
        {
            register = inst.Counter;
            return true;
        }
        register = null;
        return false;
    }

    public static bool TryGetRegisterInteractable(string locationId, out InteractableObject interactable)
    {
        if (_spawned.TryGetValue(locationId, out var inst) && inst.RegisterInteractable != null)
        {
            interactable = inst.RegisterInteractable;
            return true;
        }
        interactable = null;
        return false;
    }

    public static bool TryGetCounterInteractable(string locationId, out InteractableObject interactable)
    {
        if (_spawned.TryGetValue(locationId, out var inst) && inst.CounterInteractable != null)
        {
            interactable = inst.CounterInteractable;
            return true;
        }
        interactable = null;
        return false;
    }

    public static void ClearSpawned() => _spawned.Clear();

    public static void ClearForLocation(string locationId)
    {
        if (_spawned.TryGetValue(locationId, out var inst))
        {
            if (inst.Counter != null)
                UnityEngine.Object.Destroy(inst.Counter);
            _spawned.Remove(locationId);
        }
    }

    public static void SyncDesks()
    {
        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;

            // Stale entry: entry exists but the counter GO was destroyed — clear and re-spawn.
            if (_spawned.TryGetValue(location.Id, out var existing))
            {
                if (existing.Counter != null) continue;
                _spawned.Remove(location.Id);
            }

            if (!LocationSpawner.TryGetSpawnedBuilding(location.Id, out var buildingRoot))
                continue;

            SpawnDesk(location, buildingRoot);
        }
    }

    public static (Vector3 position, Quaternion rotation) ComputeDeskTransform(MogulLocation location)
    {
        var (position, rotation) = ComputeBaseDeskTransform(location);
        if (MogulPlacementSystem.TryGetPlacement(location.Id, MogulPlacementSystem.Desk, out var savedPos, out var savedRot))
            return (savedPos, savedRot);

        return (position, rotation);
    }

    public static (Vector3 position, Quaternion rotation) ComputeBaseDeskTransform(MogulLocation location)
    {
        if (location.DeskOffset != Vector3.zero)
        {
            var rot = location.DeskRotation != default ? location.DeskRotation : RotationFacingDoor(location.Door);
            return (location.DeskOffset, rot);
        }

        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        float w = roomSize.x;
        float d = roomSize.z;
        const float wallOffset = 1.5f;

        return location.Door switch
        {
            WallSide.East  => (new Vector3(wallOffset, 0f, d / 2f),     Quaternion.Euler(0f, 90f,  0f)),
            WallSide.West  => (new Vector3(w - wallOffset, 0f, d / 2f), Quaternion.Euler(0f, 270f, 0f)),
            WallSide.North => (new Vector3(w / 2f, 0f, wallOffset),     Quaternion.Euler(0f, 0f,   0f)),
            WallSide.South => (new Vector3(w / 2f, 0f, d - wallOffset), Quaternion.Euler(0f, 180f, 0f)),
            _              => (new Vector3(w / 2f, 0f, wallOffset),     Quaternion.identity),
        };
    }

    public static void SetLiveDeskTransform(string locationId, Vector3 localPosition, Quaternion localRotation)
    {
        if (!_spawned.TryGetValue(locationId, out var inst) || inst.Counter == null) return;
        inst.Counter.transform.localPosition = localPosition;
        inst.Counter.transform.localRotation = localRotation;
        inst.QueueAnchor = localPosition + (localRotation * Vector3.forward * 0.3f);
    }

    public static void ApplyPlacementOverrides(string locationId)
    {
        var location = PropertySystem.Find(locationId);
        if (location == null) return;
        var (pos, rot) = ComputeDeskTransform(location);
        SetLiveDeskTransform(locationId, pos, rot);
        CustomerManager.ClearQueueCache();
    }

    private static Quaternion RotationFacingDoor(WallSide door) => door switch
    {
        WallSide.East  => Quaternion.Euler(0f, 90f,  0f),
        WallSide.West  => Quaternion.Euler(0f, 270f, 0f),
        WallSide.North => Quaternion.Euler(0f, 0f,   0f),
        WallSide.South => Quaternion.Euler(0f, 180f, 0f),
        _              => Quaternion.identity,
    };

    private static Vector3 ComputeStaffAnchor(MogulLocation location)
    {
        if (location.SellDesk.HasStaffLocalPos)
            return location.SellDesk.StaffLocalPos;

        var (position, rotation) = ComputeDeskTransform(location);
        return position - (rotation * Vector3.forward * 0.9f);
    }

    private static void SpawnDesk(MogulLocation location, GameObject buildingRoot)
    {
        try
        {
            var (position, rotation) = ComputeDeskTransform(location);
            var locId = location.Id;

            // Counter — try the embedded Counter.glb first; fall back to FurnitureBuilder's
            // 2m×0.9m×0.6m coloured block when no asset has shipped yet. Each machine spawns
            // its own copy locally; the building root owns lifetime.
            GameObject counter;
            float counterTopY;
            var counterGlb = TryLoadCounterGlb(buildingRoot.transform, position, rotation);
            if (counterGlb != null)
            {
                counter = counterGlb;
                counterTopY = ComputeLocalTopY(counter);
            }
            else
            {
                var palette = new BuildingPalette();
                var furnitureBuilder = new FurnitureBuilder(buildingRoot.transform, palette);
                counter = furnitureBuilder.Create(FurnitureType.Counter, position, rotation, new Color(0.18f, 0.18f, 0.18f));
                counterTopY = 0.9f; // FurnitureType.Counter is 2×0.9×0.6
            }
            counter.name = "Mogul_Counter_" + locId;

            var queueAnchor = position + (rotation * Vector3.forward * 0.3f);
            var instance = new SellDeskInstance { Counter = counter, QueueAnchor = queueAnchor };
            _spawned[locId] = instance;

            // "Take order" interactable — sits on the counter, faces the customer side.
            var counterInteractable = counter.GetComponentInChildren<InteractableObject>(true);
            if (counterInteractable == null)
            {
                counterInteractable = counter.AddComponent<InteractableObject>();
                var col = counter.GetComponent<Collider>() ?? counter.GetComponentInChildren<Collider>(true);
                if (col != null) counterInteractable.displayLocationCollider = col;
            }
            counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            instance.CounterInteractable = counterInteractable;

            // CashRegister: single GameObject carrying visual + trigger + InteractableObject.
            // Loads embedded GLB if shipped; otherwise a primitive cube placeholder. Either way
            // the visual and the hover trigger are the same node, so we can never end up
            // invisible-but-hoverable. Local-only on every machine, no FishNet involvement.
            var registerGo = TryLoadRegisterGlb() ?? CreatePlaceholderRegister();
            registerGo.name = "Mogul_Register_" + locId;
            registerGo.transform.SetParent(counter.transform, false);
            registerGo.transform.localPosition = location.SellDesk.HasRegisterPlacement
                ? location.SellDesk.RegisterLocalPos
                : new Vector3(0.3f, counterTopY + 0.05f, 0.05f);
            registerGo.transform.localRotation = location.SellDesk.RegisterLocalRotation;

            // Strip any colliders the GLB carried — they'd block the player or hijack hover.
            foreach (var col in registerGo.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(col);

            var registerCollider = registerGo.AddComponent<BoxCollider>();
            registerCollider.size = new Vector3(0.4f, 0.5f, 0.3f);
            registerCollider.center = new Vector3(0f, 0.25f, 0f);
            registerCollider.isTrigger = true;

            int deskLayer = counter.layer;
            registerGo.layer = deskLayer;
            foreach (var t in registerGo.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = deskLayer;

            var registerInteractable = registerGo.AddComponent<InteractableObject>();
            registerInteractable.displayLocationCollider = registerCollider;
            registerInteractable.MaxInteractionRange = 3f;
            registerInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            instance.RegisterInteractable = registerInteractable;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnDesk failed for {location.Id}: {ex}");
        }
    }

    private static GameObject TryLoadCounterGlb(Transform parent, Vector3 worldPos, Quaternion worldRot)
    {
        try
        {
            var bytes = EmbeddedResourceLoader.LoadBytes(
                "Mogul.Resources.Counter.glb",
                System.Reflection.Assembly.GetExecutingAssembly());
            if (bytes == null) return null;
            var go = GltfLoader.LoadGlb(bytes);
            if (go == null) return null;

            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;

            // Imported colliders rarely match the visual cleanly — strip and add a
            // single bounding box so the player can lean on it but not phase through.
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(col);

            var bounds = ComputeWorldBounds(go);
            if (bounds.HasValue)
            {
                var box = go.AddComponent<BoxCollider>();
                var localCenter = go.transform.InverseTransformPoint(bounds.Value.center);
                box.center = localCenter;
                // Scale world-size into local-size assuming uniform scale (typical for GLB imports).
                var scale = go.transform.lossyScale;
                box.size = new Vector3(
                    bounds.Value.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    bounds.Value.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    bounds.Value.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z))
                );
            }

            return go;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[Mogul] Counter GLB load failed: {ex.Message}");
            return null;
        }
    }

    private static float ComputeLocalTopY(GameObject go)
    {
        var bounds = ComputeWorldBounds(go);
        if (!bounds.HasValue) return 0.9f;
        var topWorld = new Vector3(bounds.Value.center.x, bounds.Value.max.y, bounds.Value.center.z);
        return go.transform.InverseTransformPoint(topWorld).y;
    }

    private static Bounds? ComputeWorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return null;
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static GameObject TryLoadRegisterGlb()
    {
        try
        {
            var bytes = EmbeddedResourceLoader.LoadBytes(
                "Mogul.Resources.cashregister_cashdrawer.glb",
                System.Reflection.Assembly.GetExecutingAssembly());
            if (bytes == null) return null;
            var go = GltfLoader.LoadGlb(bytes);
            if (go == null) return null;
            go.transform.localScale = Vector3.one * 0.7f;
            return go;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[Mogul] CashRegister GLB load failed: {ex.Message}");
            return null;
        }
    }

    // Last-resort placeholder so the register is visible and interactable even
    // before art lands. Dark red cube, register-ish proportions.
    private static GameObject CreatePlaceholderRegister()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(0.35f, 0.25f, 0.25f);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = new Color(0.4f, 0.05f, 0.05f);
        return go;
    }
}
