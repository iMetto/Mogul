using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Storage;
using MelonLoader;
using Mogul.Data;
using S1MAPI.Building.Components;
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

            // If we have an entry but the counter GO was destroyed externally (e.g. building
            // rebuild), clear the stale entry so SpawnDesk runs again.
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
            WallSide.East  => (new Vector3(wallOffset, 0f, d / 2f),       Quaternion.Euler(0f, 90f,  0f)),
            WallSide.West  => (new Vector3(w - wallOffset, 0f, d / 2f),   Quaternion.Euler(0f, 270f, 0f)),
            WallSide.North => (new Vector3(w / 2f, 0f, wallOffset),       Quaternion.Euler(0f, 0f,   0f)),
            WallSide.South => (new Vector3(w / 2f, 0f, d - wallOffset),   Quaternion.Euler(0f, 180f, 0f)),
            _              => (new Vector3(w / 2f, 0f, wallOffset),       Quaternion.identity),
        };
    }

    private static Quaternion RotationFacingDoor(WallSide door) => door switch
    {
        WallSide.East  => Quaternion.Euler(0f, 90f,  0f),
        WallSide.West  => Quaternion.Euler(0f, 270f, 0f),
        WallSide.North => Quaternion.Euler(0f, 0f,   0f),
        WallSide.South => Quaternion.Euler(0f, 180f, 0f),
        _              => Quaternion.identity,
    };

    private static void SpawnDesk(MogulLocation location, GameObject buildingRoot)
    {
        if (!MogulNetwork.IsHost) return;

        try
        {
            var (position, rotation) = ComputeDeskTransform(location);
            var locId = location.Id;

            MelonLogger.Msg($"[Mogul] SpawnDesk: {locId} pos={position} rot={rotation.eulerAngles}");

            var instance = new SellDeskInstance { QueueAnchor = position + (rotation * Vector3.forward * 0.3f) };
            _spawned[locId] = instance;

            // Counter surface — MetalSquareTable as the base (non-networked, child of building root).
            // Sits at desk position, register goes on top.
            var placer = new PrefabPlacer(buildingRoot.transform);
            placer.Place(Prefabs.MetalSquareTable, position, rotation,
                networked: false,
                onReady: counterGo =>
                {
                    if (counterGo == null)
                    {
                        MelonLogger.Warning($"[Mogul] Counter table prefab not ready for {locId}");
                        return;
                    }
                    counterGo.name = "Mogul_Counter_" + locId;
                    if (_spawned.TryGetValue(locId, out var inst)) inst.Counter = counterGo;

                    // InteractableObject on the table itself (for the "Take order" prompt).
                    var counterInteractable = counterGo.GetComponentInChildren<InteractableObject>(true);
                    if (counterInteractable == null)
                    {
                        counterInteractable = counterGo.AddComponent<InteractableObject>();
                        var col = counterGo.GetComponent<Collider>() ?? counterGo.GetComponentInChildren<Collider>(true);
                        if (col != null) counterInteractable.displayLocationCollider = col;
                    }
                    counterInteractable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
                    if (_spawned.TryGetValue(locId, out var inst2)) inst2.CounterInteractable = counterInteractable;

                    MelonLogger.Msg($"[Mogul] Counter table ready for {locId}");

                    // Cash register on top of the table. MetalSquareTable is ~0.75m tall.
                    // Networked so it survives save/load properly.
                    var regPlacer = new PrefabPlacer(counterGo.transform);
                    regPlacer.Place(Prefabs.CashRegister,
                        new Vector3(0f, 0.8f, 0f),
                        Quaternion.identity,
                        networked: true,
                        onReady: regGo =>
                        {
                            if (regGo == null)
                            {
                                MelonLogger.Warning($"[Mogul] CashRegister prefab not ready for {locId}");
                                return;
                            }
                            regGo.name = "Mogul_CashRegister_" + locId;
                            var interactable = regGo.GetComponent<InteractableObject>()
                                            ?? regGo.AddComponent<InteractableObject>();
                            interactable.displayLocationCollider = regGo.GetComponent<Collider>()
                                                                ?? regGo.GetComponentInChildren<Collider>(true);
                            interactable.SetInteractableState(InteractableObject.EInteractableState.Disabled);
                            if (_spawned.TryGetValue(locId, out var inst3))
                            {
                                inst3.RegisterInteractable = interactable;
                                inst3.Register = regGo;
                            }
                            MelonLogger.Msg($"[Mogul] Cash register ready for {locId}");
                        });
                });

            MelonLogger.Msg($"[Mogul] SpawnDesk placement requested for {locId}. QueueAnchor={instance.QueueAnchor}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[Mogul] SpawnDesk failed for {location.Id}: {ex}");
        }
    }
}
