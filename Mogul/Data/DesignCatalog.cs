using System.Collections.Generic;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Data;

public static class DesignCatalog
{
    public static readonly BuildingDesign Industrial = new()
    {
        Id = "industrial",
        Name = "Industrial",
        Description = "Raw concrete and dark metal. All business.",

        WallMaterial = () => Materials.Find("dark grey concrete"),
        FloorMaterial = () => Materials.Find("tiles_black"),
        CeilingMaterial = () => Materials.Find("concrete_grey0"),
        TrimMaterial = () => Materials.Find("metal_verydarkgrey_mat"),

        ParapetRoof = true,
        LightColor = new Color(0.80f, 0.90f, 1.00f),
        LightIntensity = 1.2f,
        WindowOppositeOfDoor = true,

        PlaceFurniture = (builder, loc) =>
        {
            //Design only, spawned through Mogul.Selldesk
        },
    };

    public static readonly BuildingDesign Classic = new()
    {
        Id = "classic",
        Name = "Classic",
        Description = "Brick walls, warm wood floors. Old money.",

        WallMaterial = () => Materials.Find("brick red"),
        FloorMaterial = () => Materials.Find("wood planks medium brown mat"),
        CeilingMaterial = () => Materials.Find("off white"),
        TrimMaterial = () => Materials.Find("wood planks medium brown mat"),

        HipRoof = true,
        LightColor = new Color(1.00f, 0.88f, 0.60f),
        LightIntensity = 0.9f,
        WindowOppositeOfDoor = true,

        PlaceFurniture = (builder, loc) =>
        {
            //Design only, spawned through Mogul.Selldesk
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
}
