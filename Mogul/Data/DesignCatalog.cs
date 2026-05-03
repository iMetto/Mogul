using System.Collections.Generic;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Structural;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Data;

public static class DesignCatalog
{
    public static readonly BuildingDesign Industrial = new()
    {
        Id          = "industrial",
        Name        = "Industrial",
        Description = "Raw concrete and dark metal. All business.",

        WallMaterial    = () => Materials.IndustrialBuildingAUpperWalls,
        FloorMaterial   = () => Materials.TilesDarkGrey,
        CeilingMaterial = () => Materials.IndustrialBuildingARoof,
        TrimMaterial    = () => Materials.MetalDarkGrey,

        ParapetRoof          = true,
        LightColor           = new Color(0.80f, 0.90f, 1.00f),
        LightIntensity       = 1.2f,
        WindowOppositeOfDoor = true,

        PlaceFurniture = (builder, loc) =>
        {
            float w    = loc.RoomSize.x;
            float d    = loc.RoomSize.z;
            var   face = FaceDoor(loc.Door);
            var   dark = new Color(0.18f, 0.18f, 0.18f);

            builder
                .AddFurniture(FurnitureType.Desk,      new Vector3(w * 0.35f, 0f, d * 0.50f), face, dark)
                .AddFurniture(FurnitureType.Counter,    new Vector3(w * 0.15f, 0f, d * 0.25f), face, dark)
                .AddFurniture(FurnitureType.Bookshelf,  new Vector3(w * 0.10f, 0f, d * 0.75f), Quaternion.identity, dark);
        },
    };

    public static readonly BuildingDesign Classic = new()
    {
        Id          = "classic",
        Name        = "Classic",
        Description = "Brick walls, warm wood floors. Old money.",

        WallMaterial    = () => Materials.BrickWallRed,
        FloorMaterial   = () => Materials.WoodPlanksLightBrown,
        CeilingMaterial = () => Materials.ConcreteLightGrey,
        TrimMaterial    = () => Materials.WoodMediumBrown,

        HipRoof              = true,
        LightColor           = new Color(1.00f, 0.88f, 0.60f),
        LightIntensity       = 0.9f,
        WindowOppositeOfDoor = true,

        PlaceFurniture = (builder, loc) =>
        {
            float w    = loc.RoomSize.x;
            float d    = loc.RoomSize.z;
            var   face = FaceDoor(loc.Door);
            var   warm = new Color(0.60f, 0.40f, 0.20f);

            builder
                .AddFurniture(FurnitureType.Desk,        new Vector3(w * 0.35f, 0f, d * 0.50f), face, warm)
                .AddFurniture(FurnitureType.Chair,        new Vector3(w * 0.55f, 0f, d * 0.50f), face, warm)
                .AddFurniture(FurnitureType.CoffeeTable,  new Vector3(w * 0.65f, 0f, d * 0.50f), Quaternion.identity, warm);
        },
    };

    private static readonly Dictionary<string, BuildingDesign> _registry = new()
    {
        { Industrial.Id, Industrial },
        { Classic.Id,    Classic    },
    };

    public static IReadOnlyCollection<BuildingDesign> All => _registry.Values;

    public static BuildingDesign Get(string id)
    {
        if (id != null && _registry.TryGetValue(id, out var design)) return design;
        return Industrial;
    }

    // Rotates furniture to face whoever walks through the door
    private static Quaternion FaceDoor(WallSide door) => door switch
    {
        WallSide.East  => Quaternion.Euler(0f, 90f,  0f),
        WallSide.West  => Quaternion.Euler(0f, 270f, 0f),
        WallSide.North => Quaternion.Euler(0f, 180f, 0f),
        WallSide.South => Quaternion.identity,
        _              => Quaternion.identity,
    };
}
