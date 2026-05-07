using System;
using S1MAPI.Building;
using S1MAPI.Building.Interior;
using S1MAPI.Building.Structural;
using UnityEngine;

namespace Mogul.Data;

public class BuildingDesign
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }

    public Func<Material> WallMaterial { get; init; }
    public Func<Material> FloorMaterial { get; init; }
    public Func<Material> CeilingMaterial { get; init; }
    public Func<Material> TrimMaterial { get; init; }
    public Func<Material> RoofMaterial { get; init; }

    public bool HipRoof { get; init; }
    public bool ParapetRoof { get; init; }

    public Color LightColor { get; init; } = Color.white;
    public float LightIntensity { get; init; } = 1f;

    // Adds a window on the wall directly opposite the door
    public bool WindowOppositeOfDoor { get; init; }

    // Furniture placement — receives the builder and location so it can
    // use room dimensions and door direction for relative positioning
    public Action<BuildingBuilder, MogulLocation> PlaceFurniture { get; init; }

    public void Apply(BuildingBuilder builder, MogulLocation location)
    {
        var doorOpening = WallOpening.Door(width: 1.05f, height: 2.1f);
        var windowOpening = WindowOppositeOfDoor ? WallOpening.Window() : null;

        var opposite = location.Door switch
        {
            WallSide.North => WallSide.South,
            WallSide.South => WallSide.North,
            WallSide.East => WallSide.West,
            WallSide.West => WallSide.East,
            _ => WallSide.South,
        };

        WallOpening Opening(WallSide wall) =>
            wall == location.Door ? doorOpening :
            wall == opposite ? windowOpening :
            null;

        var wallMat = WallMaterial?.Invoke();
        if (wallMat != null) builder.Config.Palette.WallMaterial = wallMat;

        builder
            .AddWalls(Opening(WallSide.North), Opening(WallSide.South),
                      Opening(WallSide.East), Opening(WallSide.West))
            .AddFloor(material: FloorMaterial?.Invoke())
            .AddCeiling(material: CeilingMaterial?.Invoke())
            .AddDoorFrames(TrimMaterial?.Invoke())
            .AddLights(intensity: LightIntensity, color: LightColor);

        var roofMat = RoofMaterial?.Invoke();
        if (HipRoof)     builder.AddHipRoof(roofMaterial: roofMat);
        if (ParapetRoof) builder.AddParapetRoof(parapetMaterial: roofMat);

        PlaceFurniture?.Invoke(builder, location);
    }
}
