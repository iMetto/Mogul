using S1MAPI.Building.Structural;
using S1MAPI.Core;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Data;

public class StorageRackConfig
{
    public PrefabRef Prefab { get; }
    public Vector3 LocalPos { get; }
    public Quaternion Rotation { get; }

    public StorageRackConfig(PrefabRef prefab, Vector3 localPos, Quaternion rotation = default)
    {
        Prefab = prefab;
        LocalPos = localPos;
        Rotation = rotation == default ? Quaternion.identity : rotation;
    }
}

public class MogulLocation
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public float Price { get; }
    public Vector3 WorldPosition { get; }
    public WallSide Door { get; }
    public Vector3 RoomSize { get; }
    public Vector3 DeskOffset { get; }
    /// <summary>When set (non-identity), overrides the auto-computed desk rotation.</summary>
    public Quaternion DeskRotation { get; }
    public StorageRackConfig[] StorageRacks { get; }
    /// <summary>How many interior queue slots to allow before spilling outside. Default 8.</summary>
    public int MaxInteriorSlots { get; }

    public MogulLocation
    (string id,
     string name,
     string description,
     float price,
     Vector3 worldPosition,
     WallSide door,
     Vector3 roomSize,
     Vector3 deskOffset = default,
     Quaternion deskRotation = default,
     StorageRackConfig[] storageRacks = null,
     int maxInteriorSlots = 8)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        WorldPosition = worldPosition;
        Door = door;
        RoomSize = roomSize;
        DeskOffset = deskOffset;
        DeskRotation = deskRotation;
        StorageRacks = storageRacks ?? System.Array.Empty<StorageRackConfig>();
        MaxInteriorSlots = maxInteriorSlots;
    }
}
