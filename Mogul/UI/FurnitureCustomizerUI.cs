// PLACEHOLDER — Furniture customization UI
// Implement when Phase 3+ interior design is ready.
//
// ═══════════════════════════════════════════════════════════════════
// WHERE TO LOOK
// ═══════════════════════════════════════════════════════════════════
//
// Furniture types
//   assembly/S1MAPI_Il2Cpp/S1MAPI/Building/Interior/FurnitureType.cs
//   enum FurnitureType { Table, Chair, Desk, Bookshelf, Counter, CoffeeTable }
//
// Furniture builder / footprint
//   assembly/S1MAPI_Il2Cpp/S1MAPI/Building/Interior/FurnitureBuilder.cs
//   FurnitureBuilder.Create(FurnitureType, ...)
//   FurnitureBuilder.GetFootprint(FurnitureType) → Vector2 (width, depth)
//
// BuildingBuilder.AddFurniture — two overloads:
//   .AddFurniture(FurnitureType type, string position, Color? color = null)
//   .AddFurniture(FurnitureType type, Vector3 localPos, Quaternion rot, Color? color = null)
//
// Semantic position strings (parsed by BuildingBuilder.ParseSemanticPosition):
//   "center"      — room centre
//   "north"       — mid-north wall
//   "south"       — mid-south wall
//   "east"        — mid-east wall
//   "west"        — mid-west wall
//   "corner-ne"   — north-east corner
//   "corner-nw"   — north-west corner
//   "corner-se"   — south-east corner
//   "corner-sw"   — south-west corner
//
// ═══════════════════════════════════════════════════════════════════
// WHAT NEEDS TO CHANGE WHEN THIS IS IMPLEMENTED
// ═══════════════════════════════════════════════════════════════════
//
// 1. Add FurnitureSpec struct + List<FurnitureSpec> to BuildingOverrides
//    (Mogul/Systems/BuildingPreview.cs):
//
//      public struct FurnitureSpec
//      {
//          public FurnitureType Type;
//          public string        Position;   // semantic string or null if using LocalPos
//          public Vector3?      LocalPos;
//          public Quaternion?   Rotation;
//          public Color?        Color;
//      }
//
//      // inside BuildingOverrides:
//      public List<FurnitureSpec> Furniture;
//
// 2. Apply furniture overrides in LocationSpawner.SpawnBuilding()
//    (Mogul/Systems/LocationSpawner.cs) — after design.Apply(builder, location):
//
//      if (ov?.Furniture != null)
//          foreach (var f in ov.Furniture)
//              if (f.LocalPos.HasValue)
//                  builder.AddFurniture(f.Type, f.LocalPos.Value, f.Rotation ?? Quaternion.identity, f.Color);
//              else
//                  builder.AddFurniture(f.Type, f.Position ?? "center", f.Color);
//
// 3. Add furniture tab to BuildingCustomizerUI (Mogul/UI/BuildingCustomizerUI.cs):
//    - Tab label "Furniture"
//    - Type picker row (one row per FurnitureType)
//    - Position picker (semantic strings dropdown or grid)
//    - Color picker (optional — same presets as Lights tab)
//    - "Add" / "Clear all" actions
//    - On add: append to ov.Furniture, call LocationSpawner.RebuildBuilding()
//
// ═══════════════════════════════════════════════════════════════════

using S1MAPI.Building.Interior;

namespace Mogul.UI;

// Stub so the namespace compiles. Replace with real implementation.
internal static class FurnitureCustomizerUI
{
    // FurnitureType values available for placement:
    //   FurnitureType.Table
    //   FurnitureType.Chair
    //   FurnitureType.Desk
    //   FurnitureType.Bookshelf
    //   FurnitureType.Counter
    //   FurnitureType.CoffeeTable
}
