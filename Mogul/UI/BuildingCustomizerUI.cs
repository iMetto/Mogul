using System;
using Il2CppScheduleOne.PlayerScripts;
using Mogul.Systems;
using S1API.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Mogul.UI;

/// <summary>
/// In-game building customiser panel. Open with F8 near an owned location.
/// All BuildingOverrides fields are exposed here — this is the complete surface.
///
/// COVERAGE:
///   Wall / Floor / Ceiling / Roof style / Roof material / Door prefab /
///   Door-frame trim material / Base molding / Corner pillars / Corner trim /
///   Roof trim / Secondary roof trim / Ambient lighting /
///   Light colour / Light intensity
///
/// NOT YET COVERED (see FurnitureCustomizerUI.cs):
///   Furniture placement
/// </summary>
public static class BuildingCustomizerUI
{
    private enum Tab { Wall, Floor, Ceiling, Roof, Door, Trim, Lights, Size, Position }

    // ── Material option tables ────────────────────────────────────────────────
    // (Label shown in UI, material lookup string passed to Materials.Find, swatch colour)

    private static readonly (string L, string V, Color S)[] _wallOpts =
    {
        ("Brick Red",         "brick brick colored",         new Color(0.60f, 0.30f, 0.20f)),
        ("Concrete Grey",     "concrete light grey",         new Color(0.75f, 0.75f, 0.75f)),
        ("Dark Concrete",     "dark grey concrete",          new Color(0.35f, 0.35f, 0.35f)),
        ("Off White",         "off white",                   new Color(0.92f, 0.92f, 0.88f)),
        ("Tile Dirty White",  "small tile dirty white",      new Color(0.80f, 0.80f, 0.75f)),
        ("Tiles Dark Grey",   "tiles_darkgrey",              new Color(0.30f, 0.30f, 0.30f)),
        ("Metal Very Dark",   "metal_verydarkgrey_mat",      new Color(0.15f, 0.15f, 0.15f)),
        ("Metal Dark Grey",   "metal_darkgrey_mat",          new Color(0.20f, 0.20f, 0.20f)),
        ("Metal Mid Grey",    "metal_mediumgrey_mat",        new Color(0.45f, 0.45f, 0.45f)),
        ("Metal Light Grey",  "metal_lightgrey_mat",         new Color(0.65f, 0.65f, 0.65f)),
        ("Metal White",       "metal white",                 new Color(0.90f, 0.90f, 0.90f)),
        ("Wood Med Brown",    "wood planks medium brown mat",new Color(0.45f, 0.30f, 0.15f)),
        ("Wood Light Brown",  "wood planks light brown mat", new Color(0.55f, 0.40f, 0.25f)),
        ("Wood Planks White", "wood planks white",           new Color(0.90f, 0.90f, 0.90f)),
        ("Fabric White",      "fabric white",                new Color(0.95f, 0.95f, 0.95f)),
        ("Fabric Red",        "fabric red",                  new Color(0.70f, 0.20f, 0.20f)),
        ("Fabric Blue",       "fabric blue",                 new Color(0.20f, 0.30f, 0.60f)),
        ("Fabric Green",      "fabric green",                new Color(0.20f, 0.50f, 0.30f)),
        ("Fabric Denim",      "fabric denim",                new Color(0.25f, 0.35f, 0.55f)),
        ("Fabric Brown",      "fabric brown",                new Color(0.40f, 0.25f, 0.15f)),
        ("Fabric Black",      "fabric black",                new Color(0.15f, 0.15f, 0.15f)),
    };

    private static readonly (string L, string V, Color S)[] _floorOpts =
    {
        ("Tiles Dark Grey",   "tiles_darkgrey",              new Color(0.30f, 0.30f, 0.30f)),
        ("Tile Dirty White",  "small tile dirty white",      new Color(0.80f, 0.80f, 0.75f)),
        ("Concrete Lot",      "concrete_parking_lot",        new Color(0.45f, 0.45f, 0.40f)),
        ("Concrete Grey",     "concrete light grey",         new Color(0.75f, 0.75f, 0.75f)),
        ("Granite Salmon",    "granite dull salmon lighter", new Color(0.80f, 0.55f, 0.50f)),
        ("Wood Med Brown",    "wood planks medium brown mat",new Color(0.45f, 0.30f, 0.15f)),
        ("Wood Light Brown",  "wood planks light brown mat", new Color(0.55f, 0.40f, 0.25f)),
        ("Wood Planks White", "wood planks white",           new Color(0.90f, 0.90f, 0.90f)),
    };

    private static readonly (string L, string V, Color S)[] _ceilingOpts =
    {
        ("Concrete Grey",     "concrete light grey",         new Color(0.75f, 0.75f, 0.75f)),
        ("Off White",         "off white",                   new Color(0.92f, 0.92f, 0.88f)),
        ("Metal Light Grey",  "metal_lightgrey_mat",         new Color(0.65f, 0.65f, 0.65f)),
        ("Metal White",       "metal white",                 new Color(0.90f, 0.90f, 0.90f)),
        ("Wood Med Brown",    "wood planks medium brown mat",new Color(0.45f, 0.30f, 0.15f)),
    };

    private static readonly (string L, string V)[] _roofOpts =
    {
        ("Hip Roof",     "hip"),
        ("Parapet Roof", "parapet"),
        ("Flat Roof",    "flat"),
    };

    private static readonly (string L, string V)[] _doorOpts =
    {
        ("Classical Wood",  "classical"),
        ("Sliding",         "sliding"),
        ("Sliding Glass",   "slidingglass"),
        ("Metal Glass",     "metalglass"),
        ("Industrial",      "industrial"),
        ("Industrial Peep", "industrialpeep"),
        ("Base Door",       "base"),
    };

    private static readonly (string L, Color S, Color V)[] _lightOpts =
    {
        ("Warm White",  new Color(1.00f, 0.88f, 0.60f), new Color(1.00f, 0.88f, 0.60f)),
        ("Cool White",  new Color(0.85f, 0.90f, 1.00f), new Color(0.85f, 0.90f, 1.00f)),
        ("Neutral",     new Color(1.00f, 1.00f, 1.00f), new Color(1.00f, 1.00f, 1.00f)),
        ("Warm Yellow", new Color(1.00f, 0.75f, 0.20f), new Color(1.00f, 0.75f, 0.20f)),
        ("Blue",        new Color(0.40f, 0.60f, 1.00f), new Color(0.40f, 0.60f, 1.00f)),
        ("Red",         new Color(1.00f, 0.30f, 0.30f), new Color(1.00f, 0.30f, 0.30f)),
        ("Green",       new Color(0.30f, 1.00f, 0.40f), new Color(0.30f, 1.00f, 0.40f)),
        ("Purple",      new Color(0.80f, 0.30f, 1.00f), new Color(0.80f, 0.30f, 1.00f)),
    };

    // brightness swatch goes from very dim to near-white
    private static readonly (string L, float V, Color S)[] _intensityOpts =
    {
        ("Dim",    0.5f, new Color(0.22f, 0.22f, 0.18f)),
        ("Normal", 1.0f, new Color(0.55f, 0.55f, 0.45f)),
        ("Bright", 1.5f, new Color(0.80f, 0.80f, 0.65f)),
        ("Max",    2.0f, new Color(1.00f, 1.00f, 0.90f)),
    };

    // ── UI state ──────────────────────────────────────────────────────────────

    private static readonly Color _tabOn        = new Color(0.20f, 0.55f, 0.90f);
    private static readonly Color _tabOff       = new Color(0.18f, 0.18f, 0.18f);
    private static readonly Color _rowOn        = new Color(0.10f, 0.20f, 0.12f);
    private static readonly Color _rowOff       = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color _swatchNeutral = new Color(0.28f, 0.28f, 0.28f);

    private static GameObject    _root;
    private static RectTransform _listContent;
    private static ScrollRect    _scrollRect;
    private static Text          _titleText;
    private static Tab           _tab = Tab.Wall;
    private static string        _locId;
    private static Button[]      _tabBtns;
    private static CursorLockMode _prevLock;
    private static bool           _prevVisible;

    public static bool IsVisible => _root != null && _root.activeSelf;

    // ── Init ─────────────────────────────────────────────────────────────────

    public static void Init()
    {
        var canvasGO = new GameObject("MogulBC_Canvas");
        UnityEngine.Object.DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        _root = UIFactory.Panel("BC_Root", canvasGO.transform, new Color(0.10f, 0.10f, 0.10f, 0.97f));
        var rt = _root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(860f, 620f);
        rt.anchoredPosition = Vector2.zero;

        var vl = _root.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 0;
        vl.padding = new RectOffset(0, 0, 0, 0);
        ((HorizontalOrVerticalLayoutGroup)vl).childControlWidth      = true;
        ((HorizontalOrVerticalLayoutGroup)vl).childControlHeight     = true;
        ((HorizontalOrVerticalLayoutGroup)vl).childForceExpandWidth  = true;
        ((HorizontalOrVerticalLayoutGroup)vl).childForceExpandHeight = false;

        BuildTopBar();
        BuildTabBar();
        BuildScrollList();

        _root.SetActive(false);
    }

    private static void BuildTopBar()
    {
        var bar = new GameObject("TopBar");
        bar.transform.SetParent(_root.transform, false);
        bar.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
        bar.AddComponent<LayoutElement>().minHeight = 52f;

        var hl = bar.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(18, 12, 10, 10);
        hl.spacing = 12f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        ((HorizontalOrVerticalLayoutGroup)hl).childForceExpandWidth  = false;
        ((HorizontalOrVerticalLayoutGroup)hl).childForceExpandHeight = true;

        _titleText = UIFactory.Text("Title", "Building Customiser", bar.transform, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
        ((Component)_titleText).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var (_, closeBtn, _) = UIFactory.RoundedButtonWithLabel(
            "CloseBtn", "✕", bar.transform, new Color(0.65f, 0.18f, 0.18f), 36f, 36f, 18, Color.white);
        closeBtn.onClick.AddListener((UnityAction)Hide);
    }

    private static void BuildTabBar()
    {
        var bar = new GameObject("TabBar");
        bar.transform.SetParent(_root.transform, false);
        bar.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f);
        bar.AddComponent<LayoutElement>().minHeight = 44f;

        var hl = bar.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(10, 10, 6, 6);
        hl.spacing = 6f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        ((HorizontalOrVerticalLayoutGroup)hl).childForceExpandWidth  = false;
        ((HorizontalOrVerticalLayoutGroup)hl).childForceExpandHeight = true;

        var tabs = (Tab[])Enum.GetValues(typeof(Tab));
        _tabBtns = new Button[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            var t = tabs[i];
            var (_, btn, _) = UIFactory.RoundedButtonWithLabel(
                t.ToString(), t.ToString(), bar.transform, _tabOff, 84f, 30f, 13, Color.white);
            btn.onClick.AddListener((UnityAction)(() => SelectTab(t)));
            _tabBtns[i] = btn;
        }
    }

    private static void BuildScrollList()
    {
        var wrapper = new GameObject("ScrollWrapper");
        wrapper.transform.SetParent(_root.transform, false);
        // Image forces a RectTransform so the VLG can drive this child's height
        wrapper.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var le = wrapper.AddComponent<LayoutElement>();
        le.flexibleHeight = 1f;
        le.minHeight      = 100f;

        _listContent = UIFactory.ScrollableVerticalList("List", wrapper.transform, out _scrollRect);
    }

    // ── Tab logic ─────────────────────────────────────────────────────────────

    private static void SelectTab(Tab t)
    {
        _tab = t;
        RefreshTabs();
        PopulateList();
    }

    private static void RefreshTabs()
    {
        var tabs = (Tab[])Enum.GetValues(typeof(Tab));
        for (int i = 0; i < _tabBtns.Length; i++)
        {
            var img = ((Selectable)_tabBtns[i]).targetGraphic as Image;
            if (img != null) img.color = tabs[i] == _tab ? _tabOn : _tabOff;
        }
    }

    // ── List population ───────────────────────────────────────────────────────

    private static void PopulateList()
    {
        UIFactory.ClearChildren(_listContent.gameObject.transform);
        _scrollRect.verticalNormalizedPosition = 1f;

        var ov = BuildingPreview.HasOverrides(_locId) ? BuildingPreview.GetOrCreate(_locId) : null;

        switch (_tab)
        {
            case Tab.Wall:
                // Full material list — all valid Materials.Find() strings for exterior walls
                foreach (var o in _wallOpts)
                {
                    var v = o.V;
                    SwatchRow(o.L, o.S, ov?.WallMaterialName == v,
                        () => Apply(x => x.WallMaterialName = v));
                }
                break;

            case Tab.Floor:
                foreach (var o in _floorOpts)
                {
                    var v = o.V;
                    SwatchRow(o.L, o.S, ov?.FloorMaterialName == v,
                        () => Apply(x => x.FloorMaterialName = v));
                }
                break;

            case Tab.Ceiling:
                foreach (var o in _ceilingOpts)
                {
                    var v = o.V;
                    SwatchRow(o.L, o.S, ov?.CeilingMaterialName == v,
                        () => Apply(x => x.CeilingMaterialName = v));
                }
                break;

            case Tab.Roof:
                // ── Style ──────────────────────────────────────────────────
                SectionHeader("Roof Style");
                foreach (var o in _roofOpts)
                {
                    var v = o.V;
                    var sel = ov?.RoofStyle == v;
                    SwatchRow(o.L, sel ? _tabOn : _swatchNeutral, sel,
                        () => Apply(x => x.RoofStyle = v));
                }
                // ── Roof surface material ──────────────────────────────────
                // Applied to AddHipRoof(roofMaterial:) or AddParapetRoof(parapetMaterial:)
                // via BuildingDesign.RoofMaterial — see BuildingDesign.cs:Apply()
                SectionHeader("Roof Material");
                foreach (var o in _wallOpts)
                {
                    var v = o.V;
                    SwatchRow(o.L, o.S, ov?.RoofMaterialName == v,
                        () => Apply(x => x.RoofMaterialName = v));
                }
                break;

            case Tab.Door:
                // ── Door prefab ────────────────────────────────────────────
                // Resolved in BuildingPreview.ResolveDoorPrefab(), placed via PrefabPlacer
                SectionHeader("Door Style");
                foreach (var o in _doorOpts)
                {
                    var v = o.V;
                    var sel = ov?.DoorPrefabKey == v;
                    SwatchRow(o.L, sel ? _tabOn : _swatchNeutral, sel,
                        () => Apply(x => x.DoorPrefabKey = v));
                }
                // ── Door-frame trim material ───────────────────────────────
                // Passed to BuildingBuilder.AddDoorFrames() via BuildingDesign.TrimMaterial
                SectionHeader("Door Frame Material");
                foreach (var o in _wallOpts)
                {
                    var v = o.V;
                    SwatchRow(o.L, o.S, ov?.TrimMaterialName == v,
                        () => Apply(x => x.TrimMaterialName = v));
                }
                break;

            case Tab.Trim:
                // ── Structural toggles ─────────────────────────────────────
                // Each calls the corresponding BuildingBuilder.Add*() method
                // in LocationSpawner.SpawnBuilding after design.Apply()
                SectionHeader("Structural Details");
                ToggleRow("Base Molding",        ov?.BaseMolding       == true, () => Apply(x => x.BaseMolding       = !(x.BaseMolding       ?? false)));
                ToggleRow("Corner Pillars",       ov?.CornerPillars     == true, () => Apply(x => x.CornerPillars     = !(x.CornerPillars     ?? false)));
                ToggleRow("Corner Trim",          ov?.CornerTrim        == true, () => Apply(x => x.CornerTrim        = !(x.CornerTrim        ?? false)));
                ToggleRow("Roof Trim",            ov?.RoofTrim          == true, () => Apply(x => x.RoofTrim          = !(x.RoofTrim          ?? false)));
                ToggleRow("Secondary Roof Trim",  ov?.SecondaryRoofTrim == true, () => Apply(x => x.SecondaryRoofTrim = !(x.SecondaryRoofTrim ?? false)));
                // ── Lighting ambience toggle ───────────────────────────────
                // Calls BuildingBuilder.AddAmbientLighting() — fills room with soft fill light
                SectionHeader("Ambient Lighting");
                ToggleRow("Ambient Lighting",     ov?.AmbientLighting   == true, () => Apply(x => x.AmbientLighting   = !(x.AmbientLighting   ?? false)));
                break;

            case Tab.Lights:
                // ── Point light colour ─────────────────────────────────────
                // Passed to BuildingBuilder.AddLights(color:) via BuildingDesign.LightColor
                SectionHeader("Light Colour");
                foreach (var o in _lightOpts)
                {
                    var col = o.V;
                    var sel = ov?.LightColor.HasValue == true && ColorClose(ov.LightColor.Value, col);
                    SwatchRow(o.L, o.S, sel, () => Apply(x => x.LightColor = col));
                }
                // ── Point light intensity ──────────────────────────────────
                // Passed to BuildingBuilder.AddLights(intensity:) via BuildingDesign.LightIntensity
                SectionHeader("Light Intensity");
                foreach (var o in _intensityOpts)
                {
                    var val = o.V;
                    var sel = ov?.LightIntensity.HasValue == true
                              && Mathf.Abs(ov.LightIntensity.Value - val) < 0.05f;
                    SwatchRow(o.L, o.S, sel, () => Apply(x => x.LightIntensity = val));
                }
                break;

            case Tab.Size:
                var location = PropertySystem.Find(_locId);
                if (location == null) break;

                var size = BuildingPreview.GetEffectiveRoomSize(location);
                SectionHeader($"Current: {size.x:F1}w x {size.z:F1}d x {size.y:F1}h");
                SizeStepRow("Width -0.5m",  -0.5f,  0f,    0f);
                SizeStepRow("Width +0.5m",   0.5f,  0f,    0f);
                SizeStepRow("Depth -0.5m",   0f,   -0.5f,  0f);
                SizeStepRow("Depth +0.5m",   0f,    0.5f,  0f);
                SizeStepRow("Height -0.25m", 0f,    0f,   -0.25f);
                SizeStepRow("Height +0.25m", 0f,    0f,    0.25f);
                ToggleRow("Reset Size", false, () => Apply(x =>
                {
                    x.RoomWidth = null;
                    x.RoomDepth = null;
                    x.RoomHeight = null;
                }));
                break;

            case Tab.Position:
                var posLocation = PropertySystem.Find(_locId);
                if (posLocation == null) break;

                var world = BuildingPreview.GetEffectiveWorldPosition(posLocation);
                SectionHeader($"Origin: {world.x:F1}, {world.y:F1}, {world.z:F1}");
                PositionStepRow("X -0.5m", -0.5f, 0f, 0f);
                PositionStepRow("X +0.5m",  0.5f, 0f, 0f);
                PositionStepRow("Z -0.5m",  0f, 0f, -0.5f);
                PositionStepRow("Z +0.5m",  0f, 0f,  0.5f);
                PositionStepRow("Y -0.25m", 0f, -0.25f, 0f);
                PositionStepRow("Y +0.25m", 0f,  0.25f, 0f);
                ToggleRow("Set Origin Here", false, SetOriginHere);
                ToggleRow("Reset Position", false, () => Apply(x =>
                {
                    x.WorldX = null;
                    x.WorldY = null;
                    x.WorldZ = null;
                }));
                break;
        }
    }

    // ── Row builders ──────────────────────────────────────────────────────────

    private static void SectionHeader(string label)
    {
        var go = UIFactory.Panel("Hdr_" + label, _listContent.gameObject.transform,
            new Color(0.08f, 0.08f, 0.08f));
        go.AddComponent<LayoutElement>().minHeight = 28f;
        var txt = UIFactory.Text("Lbl", "  " + label.ToUpper(),
            go.transform, 11, TextAnchor.MiddleLeft, FontStyle.Bold);
        txt.color = new Color(0.50f, 0.50f, 0.50f);
        var tr = ((Component)txt).GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
    }

    private static void SwatchRow(string label, Color swatch, bool selected, Action onClick)
    {
        var row = UIFactory.CreateQuestRow(label, _listContent.gameObject.transform,
            out var icon, out var text);

        icon.GetComponent<Image>().color = swatch;
        UIFactory.Text("Name", label, text.transform, 16, TextAnchor.MiddleLeft, FontStyle.Bold);
        if (selected)
            UIFactory.Text("Check", "✓ Active", text.transform, 13,
                TextAnchor.MiddleLeft, FontStyle.Italic).color = new Color(0.35f, 0.90f, 0.45f);

        row.GetComponent<Image>().color = selected ? _rowOn : _rowOff;
        row.GetComponent<Button>().onClick.AddListener((UnityAction)onClick);
    }

    private static void ToggleRow(string label, bool on, Action onClick)
    {
        var row = UIFactory.CreateQuestRow(label, _listContent.gameObject.transform,
            out var icon, out var text);

        icon.GetComponent<Image>().color = on
            ? new Color(0.20f, 0.80f, 0.35f)
            : new Color(0.35f, 0.35f, 0.35f);
        UIFactory.Text("Name", label, text.transform, 16, TextAnchor.MiddleLeft, FontStyle.Bold);
        UIFactory.Text("State", on ? "ON" : "OFF", text.transform, 14,
            TextAnchor.MiddleLeft, FontStyle.Bold).color = on
                ? new Color(0.35f, 0.90f, 0.45f)
                : new Color(0.60f, 0.60f, 0.60f);

        row.GetComponent<Image>().color = on ? _rowOn : _rowOff;
        row.GetComponent<Button>().onClick.AddListener((UnityAction)onClick);
    }

    private static void SizeStepRow(string label, float widthDelta, float depthDelta, float heightDelta)
    {
        ToggleRow(label, false, () =>
        {
            var location = PropertySystem.Find(_locId);
            if (location == null) return;
            var size = BuildingPreview.GetEffectiveRoomSize(location);
            Apply(x =>
            {
                x.RoomWidth = Mathf.Clamp(size.x + widthDelta, 3f, 30f);
                x.RoomDepth = Mathf.Clamp(size.z + depthDelta, 3f, 30f);
                x.RoomHeight = Mathf.Clamp(size.y + heightDelta, 2.2f, 8f);
            });
        });
    }

    private static void PositionStepRow(string label, float xDelta, float yDelta, float zDelta)
    {
        ToggleRow(label, false, () =>
        {
            var location = PropertySystem.Find(_locId);
            if (location == null) return;
            var world = BuildingPreview.GetEffectiveWorldPosition(location);
            Apply(x =>
            {
                x.WorldX = world.x + xDelta;
                x.WorldY = world.y + yDelta;
                x.WorldZ = world.z + zDelta;
            });
        });
    }

    private static void SetOriginHere()
    {
        var player = Player.Local;
        if (player == null) return;
        var pos = player.transform.position;

        Apply(x =>
        {
            x.WorldX = pos.x;
            x.WorldY = pos.y;
            x.WorldZ = pos.z;
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Apply(Action<BuildingOverrides> mutate)
    {
        mutate(BuildingPreview.GetOrCreate(_locId));
        LocationSpawner.RebuildBuilding(_locId);
        PopulateList();
    }

    private static bool ColorClose(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.02f
        && Mathf.Abs(a.g - b.g) < 0.02f
        && Mathf.Abs(a.b - b.b) < 0.02f;

    // ── Public API ────────────────────────────────────────────────────────────

    public static void ShowForLocation(string locId, string locName)
    {
        if (_root == null) Init();

        _locId = locId;
        _root.SetActive(true);
        _titleText.text = $"Building Customiser — {locName}";
        _tab = Tab.Wall;
        RefreshTabs();
        PopulateList();
        _prevLock    = Cursor.lockState;
        _prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        PlayerCamera.Instance?.SetCanLook(false);
    }

    public static void Hide()
    {
        _root.SetActive(false);
        Cursor.lockState = _prevLock;
        Cursor.visible   = _prevVisible;
        PlayerCamera.Instance?.SetCanLook(true);
    }
}
