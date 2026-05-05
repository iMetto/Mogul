using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using S1API.Console;
using S1API.Entities;
using S1MAPI.Core;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Systems;

public class BuildingOverrides
{
    public string WallMaterialName;
    public string RoofStyle;       // "hip" | "parapet" | "flat" | null (keep design default)
    public string DoorPrefabKey;   // "classical" | "sliding" | "glass" | "metalglass" | "industrial" | "industrialpeep" | "base"
    public bool?  BaseMolding;
    public bool?  CornerPillars;
    public bool?  CornerTrim;
    public bool?  RoofTrim;
    public Color? LightColor;
}

public static class BuildingPreview
{
    private static readonly Dictionary<string, BuildingOverrides> _overrides = new();

    public static BuildingOverrides GetOrCreate(string locationId)
    {
        if (!_overrides.TryGetValue(locationId, out var ov))
            _overrides[locationId] = ov = new BuildingOverrides();
        return ov;
    }

    public static bool HasOverrides(string locationId)
        => _overrides.ContainsKey(locationId);

    public static void Reset(string locationId)
        => _overrides.Remove(locationId);

    public static PrefabRef ResolveDoorPrefab(string key) => key switch
    {
        "sliding"        => Prefabs.SlidingDoors,
        "slidingglass"   => Prefabs.SlidingGlassDoor,
        "glass"          => Prefabs.SlidingGlassDoor,
        "metalglass"     => Prefabs.MetalGlassDoor,
        "industrial"     => Prefabs.IndustrialMetalDoor,
        "industrialpeep" => Prefabs.IndustrialMetalDoorPeephole,
        "base"           => Prefabs.BaseDoor,
        _                => Prefabs.ClassicalWoodenDoor,
    };

    public static readonly string[] WallMaterials =
    {
        "dark grey concrete", "concrete light beige", "concrete_grey0",
        "brick red", "brick_brick_colored",
        "off white", "small tile dirty white",
        "tiles_black", "tiles_darkgrey",
        "metal_verydarkgrey_mat", "metal_medgrey", "metal_lightgrey", "metal_white",
        "wood planks medium brown mat", "wood_planks_light_brown", "wood_planks_white",
        "fabric_white", "fabric_red", "fabric_blue",
    };

    public static void RegisterConsoleCommands()
    {
        try
        {
            var regType = typeof(BaseConsoleCommand).Assembly
                .GetType("S1API.Console.CustomConsoleRegistry");
            var method = regType?.GetMethod("Register",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                MelonLogger.Warning("[Mogul] BuildingPreview: CustomConsoleRegistry.Register not found");
                return;
            }
            method.Invoke(null, new object[] { new MbCommand() });
            MelonLogger.Msg("[Mogul] Console command 'mb' registered");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] BuildingPreview: failed to register command: " + ex.Message);
        }
    }
}

public class MbCommand : BaseConsoleCommand
{
    public override string CommandWord        => "mb";
    public override string CommandDescription => "Live building preview — wall/roof/door/molding/corner/rooftrim/light/info/reset/walls/doors";
    public override string ExampleUsage       => "mb wall brick red";

    public override void ExecuteCommand(List<string> args)
    {
        if (args.Count == 0)
        {
            MelonLogger.Msg("[mb] Sub-commands: wall | roof | door | molding | corner | rooftrim | light | info | reset | walls | doors");
            return;
        }

        string sub = args[0];

        if (sub == "walls")
        {
            MelonLogger.Msg("[mb] Available wall materials:");
            foreach (var m in BuildingPreview.WallMaterials)
                MelonLogger.Msg("  " + m);
            return;
        }

        if (sub == "doors")
        {
            MelonLogger.Msg("[mb] Door keys: classical | sliding | glass | metalglass | industrial | industrialpeep | base");
            return;
        }

        if (!MogulNetwork.IsHost)
        {
            MelonLogger.Msg("[mb] Host only");
            return;
        }

        var pos = Player.Local?.Position;
        if (!pos.HasValue) { MelonLogger.Msg("[mb] Player not available"); return; }

        if (!LocationGeometry.TryFindNearestLocation(pos.Value, out var location))
        {
            MelonLogger.Msg("[mb] No owned+spawned location nearby");
            return;
        }

        if (sub == "info")
        {
            var ov2 = BuildingPreview.HasOverrides(location.Id)
                ? BuildingPreview.GetOrCreate(location.Id)
                : null;
            MelonLogger.Msg($"[mb] {location.Name} ({location.Id}):");
            MelonLogger.Msg($"  wall    = {ov2?.WallMaterialName ?? "(default)"}");
            MelonLogger.Msg($"  roof    = {ov2?.RoofStyle ?? "(default)"}");
            MelonLogger.Msg($"  door    = {ov2?.DoorPrefabKey ?? "(default)"}");
            MelonLogger.Msg($"  molding = {ov2?.BaseMolding?.ToString() ?? "(off)"}");
            MelonLogger.Msg($"  corner  = {ov2?.CornerPillars?.ToString() ?? "(off)"}");
            MelonLogger.Msg($"  rooftrim= {ov2?.RoofTrim?.ToString() ?? "(off)"}");
            MelonLogger.Msg($"  light   = {ov2?.LightColor?.ToString() ?? "(default)"}");
            return;
        }

        var ov = BuildingPreview.GetOrCreate(location.Id);

        switch (sub)
        {
            case "wall":
                if (args.Count < 2) { MelonLogger.Msg("[mb] Usage: mb wall <material name>  (run 'mb walls' for list)"); return; }
                ov.WallMaterialName = string.Join(" ", args.GetRange(1, args.Count - 1));
                MelonLogger.Msg($"[mb] Wall → '{ov.WallMaterialName}' — rebuilding {location.Name}...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "roof":
                if (args.Count < 2) { MelonLogger.Msg("[mb] Usage: mb roof <hip|parapet|flat>"); return; }
                ov.RoofStyle = args[1];
                MelonLogger.Msg($"[mb] Roof → '{ov.RoofStyle}' — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "door":
                if (args.Count < 2) { MelonLogger.Msg("[mb] Usage: mb door <key>  (run 'mb doors' for options)"); return; }
                ov.DoorPrefabKey = args[1];
                MelonLogger.Msg($"[mb] Door → '{ov.DoorPrefabKey}' — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "molding":
                ov.BaseMolding = !(ov.BaseMolding ?? false);
                MelonLogger.Msg($"[mb] BaseMolding → {ov.BaseMolding} — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "corner":
                ov.CornerPillars = !(ov.CornerPillars ?? false);
                MelonLogger.Msg($"[mb] CornerPillars → {ov.CornerPillars} — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "rooftrim":
                ov.RoofTrim = !(ov.RoofTrim ?? false);
                MelonLogger.Msg($"[mb] RoofTrim → {ov.RoofTrim} — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "light":
                if (args.Count < 4) { MelonLogger.Msg("[mb] Usage: mb light <r> <g> <b>  (floats 0-1, e.g. mb light 1 0.88 0.6)"); return; }
                if (!float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float r) ||
                    !float.TryParse(args[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float g) ||
                    !float.TryParse(args[3], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float b))
                {
                    MelonLogger.Msg("[mb] Invalid values — example: mb light 1 0.88 0.6");
                    return;
                }
                ov.LightColor = new Color(r, g, b);
                MelonLogger.Msg($"[mb] Light → ({r:F2},{g:F2},{b:F2}) — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "reset":
                BuildingPreview.Reset(location.Id);
                MelonLogger.Msg($"[mb] Reset overrides for {location.Name} — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            default:
                MelonLogger.Msg("[mb] Unknown sub-command. Try: wall / roof / door / molding / corner / rooftrim / light / info / reset / walls / doors");
                break;
        }
    }
}
