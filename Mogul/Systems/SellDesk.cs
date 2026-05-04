using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building.Components;
using S1MAPI.Building.Config;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Structural;
using S1MAPI.S1;
using UnityEngine;
using Il2CppScheduleOne.Interaction;

namespace Mogul.Systems;

public static class SellDesk
{
    private class SellDeskInstance
    {
        public GameObject Counter;
        public GameObject Register;
        public Vector3 QueueAnchor;
        public InteractableObject RegisterInteractable;
        public InteractableObject CounterInteractable;
    }

    private static readonly Dictionary<string, SellDeskInstance> _spawned = new();

    public static void Initialize()
    {
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

    public static bool TryGetRegister(string locationId, out GameObject register)
    {
        if (_spawned.TryGetValue(locationId, out var inst) && inst.Register != null)
        {
            register = inst.Register;
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

    public static void SyncDesks()
    {
        foreach (var location in PropertySystem.Catalog)
        {
            if (!PropertySystem.IsOwned(location.Id)) continue;
            if (_spawned.ContainsKey(location.Id)) continue;

            if (!LocationSpawner.TryGetSpawnedBuilding(location.Id, out var buildingRoot))
            {
                // Building may not have spawned yet. Next data change or manual sync can catch it.
                continue;
            }

            SpawnDesk(location, buildingRoot);
        }
    }

    public static (Vector3 position, Quaternion rotation) ComputeDeskTransform(MogulLocation location)
    {
        if (location.DeskOffset != Vector3.zero)
        {
            var rot = location.DeskRotation != default ? location.DeskRotation : RotationFacingDoor(location.Door);
            return (location.DeskOffset, rot);
        }

        float w = location.RoomSize.x;
        float d = location.RoomSize.z;

        const float wallOffset = 1.5f;

        return location.Door switch
        {
            // Door on east wall, desk goes near west wall, faces east (+X).
            WallSide.East => (
                new Vector3(wallOffset, 0f, d / 2f),
                Quaternion.Euler(0f, 90f, 0f)
            ),

            // Door on west wall, desk goes near east wall, faces west (-X).
            WallSide.West => (
                new Vector3(w - wallOffset, 0f, d / 2f),
                Quaternion.Euler(0f, 270f, 0f)
            ),

            // Door on north wall, desk goes near south wall, faces north (+Z).
            WallSide.North => (
                new Vector3(w / 2f, 0f, wallOffset),
                Quaternion.Euler(0f, 0f, 0f)
            ),

            // Door on south wall, desk goes near north wall, faces south (-Z).
            WallSide.South => (
                new Vector3(w / 2f, 0f, d - wallOffset),
                Quaternion.Euler(0f, 180f, 0f)
            ),

            _ => (
                new Vector3(w / 2f, 0f, wallOffset),
                Quaternion.identity
            ),
        };
    }

    private static Quaternion RotationFacingDoor(WallSide door) => door switch
    {
        WallSide.East => Quaternion.Euler(0f, 90f, 0f),
        WallSide.West => Quaternion.Euler(0f, 270f, 0f),
        WallSide.North => Quaternion.Euler(0f, 0f, 0f),
        WallSide.South => Quaternion.Euler(0f, 180f, 0f),
        _ => Quaternion.identity,
    };

    private static void SpawnDesk(MogulLocation location, GameObject buildingRoot)
    {
        try
        {
            var (position, rotation) = ComputeDeskTransform(location);

            var color = new Color(0.18f, 0.18f, 0.18f);

            var palette = new BuildingPalette();
            var furnitureBuilder = new FurnitureBuilder(buildingRoot.transform, palette);
            // Create at identity so PrimitiveBuilder's SetParent(worldPositionStays:true) gives
            // children localRotation=identity. Rotate folder afterward so children inherit it.
            var counter = furnitureBuilder.Create(FurnitureType.Counter, position, Quaternion.identity, color);
            counter.transform.localRotation = rotation;
            counter.name = "Mogul_SellDesk_Counter_" + location.Id;

            var queueAnchor = position + (rotation * Vector3.forward * 0.3f);

            var instance = new SellDeskInstance { Counter = counter, QueueAnchor = queueAnchor };
            _spawned[location.Id] = instance;

            var counterInteractable = counter.GetComponentInChildren<InteractableObject>(true);
            if (counterInteractable == null)
            {
                counterInteractable = counter.AddComponent<InteractableObject>();
                var col = counter.GetComponent<Collider>() ?? counter.GetComponentInChildren<Collider>(true);
                if (col != null) counterInteractable.displayLocationCollider = col;
            }
            counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
            instance.CounterInteractable = counterInteractable;

            var registerGo = Meshes.Custom("Cash").Instantiate(
             "Mogul_CashRegister_" + location.Id,
                parent: counter.transform,
                addCollider: true
             );

            if (registerGo != null)
            {
                registerGo.SetActive(true);
                registerGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                registerGo.transform.localRotation = Quaternion.identity;
                registerGo.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                var interactable = registerGo.AddComponent<InteractableObject>();
                interactable.displayLocationCollider = registerGo.GetComponent<Collider>();
                interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
                instance.RegisterInteractable = interactable;

                instance.Register = registerGo;
                MelonLogger.Msg($"[Mogul] Cash register placed for {location.Id}");
            }
            else
                MelonLogger.Warning($"[Mogul] CashRegister mesh not found for {location.Id}");

            MelonLogger.Msg($"[Mogul] Sell desk spawned for {location.Id}. Desk={position}, QueueAnchor={queueAnchor}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnDesk failed for {location.Id}: {ex}");
        }
    }
}