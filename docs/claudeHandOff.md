# Mogul Mod — Claude Handoff

## Project overview

MelonLoader mod for **Schedule I** (the game). Adds a phone app called **Mogul** — a management hub for properties, online orders, and quests. Uses `S1API.PhoneApp` as the base class. Built with `UnityEngine.UI` (UGUI) entirely in code — no prefabs, no Unity editor, every element constructed in C# at runtime.

## Deployment

```bash
./deploy.sh
```

Builds the project and copies `Mogul.dll` + all asset PNGs to:
```
C:/Users/ahmed/AppData/Roaming/Thunderstore Mod Manager/DataFolder/ScheduleI/profiles/Schedule 1/Mods/imetto-Mogul/
```

Assets deployed alongside the DLL:
- `property.png`, `orders.png`, `quests.png` — background images for hub cards and tab panels
- `propertyIcon.png`, `ordersIcon.png`, `questsIcon.png` — icon PNGs (currently unused in code, ready to wire up)

## File structure

```
Mogul/Apps/
  MogulApp.cs              — partial class root: color constants, fields, OnCreatedUI, helpers (BuildButton, BuildSection, MakeText, EnsureRect)
  MogulApp.Chrome.cs       — BuildBackground, BuildHeader, BuildTabBar, BuildHubPanel, BuildHubCard, LoadModTexture
  MogulApp.PropertiesTab.cs — BuildListPanel, RefreshList, BuildRow
  MogulApp.OrdersTab.cs    — BuildOrdersPanel, RefreshOrdersPanel, BuildOrderRow, BuildOrderStatusRow
  MogulApp.QuestsTab.cs    — BuildQuestPanel, RefreshQuestPanel, BuildQuestHeader, BuildQuestRow
  MogulApp.ManagePanel.cs  — BuildManagePanel, RefreshManagePanel
  MogulApp.MixBuilder.cs   — BuildMixBuilderPanel, RefreshMixBuilderPanel
```

## Color palette (MogulApp.cs)

```csharp
ColorBg       = (0.025, 0.030, 0.040, 1)     // near-black background
ColorHeader   = (0.040, 0.048, 0.060, 0.98)  // slightly lighter header
ColorPanel    = (0.070, 0.085, 0.105, 0.96)
ColorRow      = (0.095, 0.115, 0.140, 0.92)  // list row background
ColorRowSel   = (0.145, 0.120, 0.055, 0.96)  // selected row (golden tint)
ColorRowOwned = (0.065, 0.115, 0.085, 0.94)  // owned row (green tint)
ColorGold     = (0.86, 0.72, 0.22, 1)        // primary accent, titles, active tab
ColorMuted    = (0.58, 0.64, 0.70, 1)        // secondary text
ColorDark     = (0.025, 0.030, 0.040, 1)     // same as ColorBg, used for text on gold
ColorAccent   = (0.34, 0.62, 0.90, 1)        // blue — Orders
ColorGreen    = (0.42, 0.80, 0.27, 1)        // green — Properties
ColorPurple   = (0.68, 0.36, 0.92, 1)        // purple — Quests
```

## UI layout (anchor space)

The phone is horizontal (`EOrientation.Horizontal`). The container fills the phone screen.

```
y=1.0 ┌─────────────────────────────┐
      │  Header (MOGUL + reach)     │  0.90–1.00
y=0.9 ├─────────────────────────────┤
      │  Tab bar (PROPERTIES/ORDERS/QUESTS)  │  0.82–0.90
y=0.82├─────────────────────────────┤
      │                             │
      │  Active panel (0.0–0.82)   │  list / orders / quests / manage / hub
      │                             │
y=0.0 └─────────────────────────────┘
```

**Hub panel** replaces the tab bar + active panel area when active (0.0–0.90), contains three side-by-side cards.

## Hub cards (BuildHubCard in Chrome.cs)

Each card layer stack (bottom to top):
1. `Image` on card — solid `accent × 0.25f, alpha 1` (dark tinted base)
2. `BgImage` (`RawImage`) — PNG background at 38% opacity, stretched to fill
3. `Strip` — full accent color bar, 5px, pinned to top edge
4. `Title`, `Divider`, `Desc`, `Open` button — content
5. `BorderL`, `BorderR`, `BorderB` — 2px accent-colored border strips at 45% opacity (top edge covered by Strip)

Card anchors inside the shell: `(0.035, 0.06)→(0.325, 0.93)`, `(0.355, 0.06)→(0.645, 0.93)`, `(0.675, 0.06)→(0.965, 0.93)`

## Tab panels

Each panel has:
1. `Image` component on the panel root — `accent × 0.18f, alpha 1` (solid colored bg)
2. `BgImage` child (`RawImage`) — panel's PNG at 40% opacity, stretched to fill
3. `ScrollRect` → `Viewport` (RectMask2D) → `Content` (VerticalLayoutGroup + ContentSizeFitter)

Panels:
- Properties: `ColorGreen` tint, `property.png`
- Orders: `ColorAccent` tint, `orders.png`
- Quests: `ColorPurple` tint, `quests.png`

## Key helpers

```csharp
LoadModTexture(filename)     // loads PNG from same dir as DLL (System.IO + Reflection)
EnsureRect(go)               // gets or adds RectTransform
MakeText(parent, name, text) // creates Text child with the runtime font
BuildButton(...)             // creates a labeled Image+Button child
BuildSection(...)            // creates a titled box section
```

## Known open issues / things to tune

- **Background images**: currently simple stretch (fill with distortion). Portrait images in landscape panels look slightly squished horizontally. No clean solution found yet without excessive zoom or narrow strips — the images may need landscape-cropped versions, or the user may accept the slight stretch at lower opacity.
- **Icon PNGs** (`propertyIcon.png`, `ordersIcon.png`, `questsIcon.png`): deployed but not wired up. The text "P"/"O"/"!" in hub cards was removed from the signature — to re-add icons, add a `Texture2D iconTexture = null` param back to `BuildHubCard` and render a `RawImage` at anchor `(0.15, 0.68)→(0.85, 0.90)` with `FitInParent` and 80% opacity.
- **Hub card title text**: the `"P"/"O"/"!"` string parameter was removed from `BuildHubCard` entirely. If a text icon fallback is ever needed, the old parameter was `string icon` as the second argument.
- The hub card title/subtitle ("MOGUL" + "Management hub") were removed from `BuildHubPanel` by the user — the space is now used by the taller cards.

## Asset loading pattern

```csharp
private static Texture2D LoadModTexture(string filename)
{
    var dir  = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    var path = Path.Combine(dir, filename);
    if (!File.Exists(path)) return null;
    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    tex.LoadImage(File.ReadAllBytes(path));
    return tex;
}
```

## Navigation flow

```
Hub (default)
  → click card → SelectMainTab(tab) → ShowView(View.List)
  
List (View.List, tab = Properties/Orders/Quests)
  → MANAGE button → ShowView(View.Manage)
  → CUSTOMIZE button → ShowView(View.Customize)

Manage → back → View.List
MixBuilder → back → View.Manage
List → back → View.Hub
```

## Build command

```bash
cd /home/imetto/projects/mods/Mogul
./deploy.sh
```
