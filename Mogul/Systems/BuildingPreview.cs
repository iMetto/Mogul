using System;
using System.Collections.Generic;
using System.Globalization;
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
    public string FloorMaterialName;
    public string CeilingMaterialName;
    public string RoofMaterialName;
    public string TrimMaterialName;     // door frames
    public string RoofStyle;            // "hip" | "parapet" | "flat" | null
    public string DoorPrefabKey;        // "classical" | "sliding" | "slidingglass" | "metalglass" | "industrial" | "industrialpeep" | "base"
    public bool?  BaseMolding;
    public bool?  CornerPillars;
    public bool?  CornerTrim;
    public bool?  RoofTrim;
    public bool?  SecondaryRoofTrim;
    public bool?  AmbientLighting;
    public Color? LightColor;
    public float? LightIntensity;       // 0.5 dim → 2.0 max
    public float? RoomWidth;
    public float? RoomHeight;
    public float? RoomDepth;
    public float? WorldX;
    public float? WorldY;
    public float? WorldZ;
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

    public static Vector3 GetEffectiveRoomSize(Mogul.Data.MogulLocation location)
    {
        var size = location.RoomSize;
        if (!_overrides.TryGetValue(location.Id, out var ov)) return size;

        return new Vector3(
            Mathf.Clamp(ov.RoomWidth ?? size.x, 3f, 30f),
            Mathf.Clamp(ov.RoomHeight ?? size.y, 2.2f, 8f),
            Mathf.Clamp(ov.RoomDepth ?? size.z, 3f, 30f));
    }

    public static Vector3 GetEffectiveWorldPosition(Mogul.Data.MogulLocation location)
    {
        var pos = location.WorldPosition;
        if (!_overrides.TryGetValue(location.Id, out var ov)) return pos;

        return new Vector3(
            ov.WorldX ?? pos.x,
            ov.WorldY ?? pos.y,
            ov.WorldZ ?? pos.z);
    }

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
        "brick brick colored", "concrete light grey", "dark grey concrete",
        "off white", "small tile dirty white", "tiles_darkgrey",
        "metal_verydarkgrey_mat", "metal_mediumgrey_mat", "metal_lightgrey_mat", "metal white",
        "wood planks medium brown mat", "wood planks light brown mat", "wood planks white",
        "fabric white", "fabric red", "fabric blue", "fabric green", "fabric denim", "fabric brown", "fabric black",
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
    public override string CommandDescription => "Live building preview — wall/roof/door/molding/corner/rooftrim/light/size/pos/info/reset/walls/doors";
    public override string ExampleUsage       => "mb pos nw -160.08 79.05 -3.03";

    public override void ExecuteCommand(List<string> args)
    {
        if (args.Count == 0)
        {
            MelonLogger.Msg("[mb] Sub-commands: wall | roof | door | molding | corner | rooftrim | light | size | pos | info | reset | walls | doors");
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
            var size = BuildingPreview.GetEffectiveRoomSize(location);
            var world = BuildingPreview.GetEffectiveWorldPosition(location);
            MelonLogger.Msg($"  size    = {size.x:F2}w {size.z:F2}d {size.y:F2}h");
            MelonLogger.Msg($"  pos     = {world.x:F2} {world.y:F2} {world.z:F2}");
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

            case "size":
                if (args.Count >= 2 && args[1] == "reset")
                {
                    ov.RoomWidth = null;
                    ov.RoomDepth = null;
                    ov.RoomHeight = null;
                    MelonLogger.Msg($"[mb] Size reset for {location.Name} — rebuilding...");
                    LocationSpawner.RebuildBuilding(location.Id);
                    break;
                }

                if (args.Count < 3)
                {
                    MelonLogger.Msg("[mb] Usage: mb size <width> <depth> [height] | mb size reset");
                    return;
                }
                if (!TryParseFloat(args[1], out float width) ||
                    !TryParseFloat(args[2], out float depth) ||
                    (args.Count >= 4 && !TryParseFloat(args[3], out _)))
                {
                    MelonLogger.Msg("[mb] Invalid size values. Example: mb size 11 4.5 3");
                    return;
                }

                float height = args.Count >= 4 && TryParseFloat(args[3], out var parsedHeight)
                    ? parsedHeight
                    : location.RoomSize.y;
                ov.RoomWidth = Mathf.Clamp(width, 3f, 30f);
                ov.RoomDepth = Mathf.Clamp(depth, 3f, 30f);
                ov.RoomHeight = Mathf.Clamp(height, 2.2f, 8f);
                MelonLogger.Msg($"[mb] Size → {ov.RoomWidth:F2}w {ov.RoomDepth:F2}d {ov.RoomHeight:F2}h — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "pos":
                if (args.Count >= 2 && args[1] == "reset")
                {
                    ov.WorldX = null;
                    ov.WorldY = null;
                    ov.WorldZ = null;
                    MelonLogger.Msg($"[mb] Position reset for {location.Name} — rebuilding...");
                    LocationSpawner.RebuildBuilding(location.Id);
                    break;
                }

                if (args.Count >= 2 && args[1] == "here")
                {
                    ov.WorldX = pos.Value.x;
                    ov.WorldY = pos.Value.y;
                    ov.WorldZ = pos.Value.z;
                    MelonLogger.Msg($"[mb] Position → player origin ({ov.WorldX:F2}, {ov.WorldY:F2}, {ov.WorldZ:F2}) — rebuilding...");
                    LocationSpawner.RebuildBuilding(location.Id);
                    break;
                }

                bool northWestCorner = args.Count >= 2 && args[1] == "nw";
                int firstValue = northWestCorner ? 2 : 1;
                if (args.Count < firstValue + 2)
                {
                    MelonLogger.Msg("[mb] Usage: mb pos <x> <z> [y] | mb pos nw <x> <z> [y] | mb pos here | mb pos reset");
                    return;
                }
                if (!TryParseFloat(args[firstValue], out float x) ||
                    !TryParseFloat(args[firstValue + 1], out float z) ||
                    (args.Count > firstValue + 2 && !TryParseFloat(args[firstValue + 2], out _)))
                {
                    MelonLogger.Msg("[mb] Invalid position values. Example: mb pos nw -160.08 79.05 -3.03");
                    return;
                }

                float y = args.Count > firstValue + 2 && TryParseFloat(args[firstValue + 2], out var parsedY)
                    ? parsedY
                    : BuildingPreview.GetEffectiveWorldPosition(location).y;
                var newPos = new Vector3(x, y, z);
                if (northWestCorner)
                {
                    var size = BuildingPreview.GetEffectiveRoomSize(location);
                    newPos.z -= size.z;
                }
                ov.WorldX = newPos.x;
                ov.WorldY = newPos.y;
                ov.WorldZ = newPos.z;
                MelonLogger.Msg($"[mb] Position → ({ov.WorldX:F2}, {ov.WorldY:F2}, {ov.WorldZ:F2}) — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            case "reset":
                BuildingPreview.Reset(location.Id);
                MelonLogger.Msg($"[mb] Reset overrides for {location.Name} — rebuilding...");
                LocationSpawner.RebuildBuilding(location.Id);
                break;

            default:
                MelonLogger.Msg("[mb] Unknown sub-command. Try: wall / roof / door / molding / corner / rooftrim / light / size / pos / info / reset / walls / doors");
                break;
        }
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
