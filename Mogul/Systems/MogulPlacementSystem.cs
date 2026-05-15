using System;
using System.Collections.Generic;
using System.Globalization;
using MelonLoader;
using Mogul.Data;
using UnityEngine;

namespace Mogul.Systems;

public static class MogulPlacementSystem
{
    public const string Desk = "desk";
    public const string Cashier = "cashier";
    public const string GrowTent = "grow_tent";
    public const string Storage0 = "storage_0";

    private static readonly string[] EditableObjects = { Desk, Cashier, GrowTent, Storage0 };
    private static readonly Dictionary<string, PlacementTransform> _original = new();
    private static readonly Dictionary<string, PlacementTransform> _working = new();

    private static string _locationId;
    private static int _selectedIndex;
    private static bool _active;
    private static GameObject _cashierMarker;

    private struct PlacementTransform
    {
        public Vector3 Position;
        public float Yaw;

        public PlacementTransform(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }

    public static bool IsActive => _active;

    public static void Initialize()
    {
        MogulNetwork.OnDataChanged += _ => ApplySavedPlacements();
    }

    public static bool TryGetPlacement(string locationId, string objectId, out Vector3 localPos, out Quaternion localRot)
    {
        localPos = default;
        localRot = default;
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(objectId)) return false;
        if (!MogulNetwork.Data.LocationObjectPlacements.TryGetValue(locationId, out var placements)) return false;
        if (!placements.TryGetValue(objectId, out var data) || data == null) return false;

        localPos = new Vector3(data.X, data.Y, data.Z);
        localRot = Quaternion.Euler(0f, data.Yaw, 0f);
        return true;
    }

    public static void Begin(string locationId)
    {
        if (string.IsNullOrEmpty(locationId) || !PropertySystem.IsOwned(locationId)) return;
        if (!LocationSpawner.TryGetSpawnedBuilding(locationId, out var buildingRoot) || buildingRoot == null)
        {
            MelonLogger.Warning($"[Mogul] Move objects: no spawned building for {locationId}");
            return;
        }

        _locationId = locationId;
        _selectedIndex = 0;
        _active = true;
        _original.Clear();
        _working.Clear();

        foreach (var objectId in EditableObjects)
        {
            if (TryGetCurrentTransform(locationId, objectId, out var transform))
            {
                _original[objectId] = transform;
                _working[objectId] = transform;
            }
        }

        EnsureCashierMarker(buildingRoot);
        UpdateCashierMarker();
        MelonLogger.Msg($"[Mogul] Move objects started for {locationId}");
    }

    public static void ToggleNearest(Vector3 worldPos)
    {
        if (_active)
        {
            Cancel();
            return;
        }

        if (!LocationGeometry.TryFindNearestLocation(worldPos, out var location) || location == null)
        {
            MelonLogger.Warning("[Mogul] Move objects: no nearby Mogul location");
            return;
        }

        if (!PropertySystem.IsOwned(location.Id))
        {
            MelonLogger.Warning($"[Mogul] Move objects: {location.Id} is not owned");
            return;
        }

        Begin(location.Id);
    }

    public static void Tick()
    {
        if (!_active) return;
        if (!LocationSpawner.TryGetSpawnedBuilding(_locationId, out var buildingRoot) || buildingRoot == null)
        {
            Cancel();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Save();
            return;
        }

        SelectFromNumberKeys();

        var objectId = SelectedObjectId();
        if (objectId == null || !_working.TryGetValue(objectId, out var current)) return;

        float moveStep = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 0.05f : 0.25f;
        float yawStep = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1f : 5f;
        Vector3 delta = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) delta += Vector3.forward * moveStep;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) delta += Vector3.back * moveStep;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) delta += Vector3.left * moveStep;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) delta += Vector3.right * moveStep;
        if (Input.GetKeyDown(KeyCode.PageUp)) delta += Vector3.up * moveStep;
        if (Input.GetKeyDown(KeyCode.PageDown)) delta += Vector3.down * moveStep;

        bool changed = delta != Vector3.zero;
        if (changed)
            current.Position = ClampToRoom(current.Position + delta, buildingRoot);

        if (Input.GetKeyDown(KeyCode.Q))
        {
            current.Yaw -= yawStep;
            changed = true;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            current.Yaw += yawStep;
            changed = true;
        }

        if (changed)
        {
            current.Yaw = NormalizeYaw(current.Yaw);
            _working[objectId] = current;
            ApplyLiveTransform(_locationId, objectId, current);
            if (objectId == Cashier)
                UpdateCashierMarker();
        }
    }

    public static void DrawGui()
    {
        if (!_active) return;

        string objectId = SelectedObjectId() ?? "(none)";
        string label = ObjectLabel(objectId);
        string posText = "";
        if (_working.TryGetValue(objectId, out var t))
            posText = $"local=({t.Position.x:F2}, {t.Position.y:F2}, {t.Position.z:F2}) yaw={t.Yaw:F1}";

        var rect = new Rect(20f, 20f, 560f, 176f);
        GUI.Box(rect, "Mogul Move Objects");
        GUI.Label(new Rect(180f, 48f, 380f, 22f), $"{_locationId} · {label}");
        GUI.Label(new Rect(180f, 70f, 380f, 22f), posText);
        GUI.Label(new Rect(180f, 98f, 380f, 22f), "WASD/arrows move | Q/E rotate | PgUp/PgDn height");
        GUI.Label(new Rect(180f, 120f, 380f, 22f), "Enter save | Esc or F9 cancel | hold Shift for fine steps");
        GUI.Label(new Rect(36f, 48f, 120f, 20f), "Objects");

        for (int i = 0; i < EditableObjects.Length; i++)
        {
            string id = EditableObjects[i];
            bool available = _working.ContainsKey(id);
            string prefix = i == _selectedIndex ? "> " : "";
            string buttonLabel = $"{i + 1}. {prefix}{ObjectLabel(id)}";
            var buttonRect = new Rect(36f, 72f + i * 24f, 124f, 22f);

            GUI.enabled = available;
            if (GUI.Button(buttonRect, buttonLabel))
                _selectedIndex = i;
            GUI.enabled = true;
        }
    }

    private static void SelectFromNumberKeys()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectIndex(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectIndex(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectIndex(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectIndex(3);
    }

    private static void SelectIndex(int index)
    {
        if (index < 0 || index >= EditableObjects.Length) return;
        if (!_working.ContainsKey(EditableObjects[index])) return;
        _selectedIndex = index;
    }

    private static string SelectedObjectId()
    {
        if (_selectedIndex < 0 || _selectedIndex >= EditableObjects.Length) return null;
        return EditableObjects[_selectedIndex];
    }

    private static bool TryGetCurrentTransform(string locationId, string objectId, out PlacementTransform transform)
    {
        transform = default;

        if (TryGetPlacement(locationId, objectId, out var savedPos, out var savedRot))
        {
            transform = new PlacementTransform(savedPos, savedRot.eulerAngles.y);
            return true;
        }

        var location = PropertySystem.Find(locationId);
        if (location == null) return false;

        switch (objectId)
        {
            case Desk:
                var (deskPos, deskRot) = SellDesk.ComputeBaseDeskTransform(location);
                transform = new PlacementTransform(deskPos, deskRot.eulerAngles.y);
                return true;
            case Cashier:
                if (SellDesk.TryGetDefaultStaffAnchor(locationId, out var staffPos, out var staffRot))
                {
                    transform = new PlacementTransform(staffPos, staffRot.eulerAngles.y);
                    return true;
                }
                return false;
            case GrowTent:
                transform = new PlacementTransform(EmployeeSystem.GetDefaultGrowTentLocalPosition(location), EmployeeSystem.GetDefaultGrowTentLocalRotation(location).eulerAngles.y);
                return true;
            case Storage0:
                if (LocationSpawner.TryGetRackLocalTransform(locationId, 0, out var rackPos, out var rackRot))
                {
                    transform = new PlacementTransform(rackPos, rackRot.eulerAngles.y);
                    return true;
                }
                if (location.StorageRacks.Length > 0)
                {
                    transform = new PlacementTransform(location.StorageRacks[0].LocalPos + new Vector3(0f, 0.5f, 0f), location.StorageRacks[0].Rotation.eulerAngles.y);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static void ApplyLiveTransform(string locationId, string objectId, PlacementTransform transform)
    {
        var rot = Quaternion.Euler(0f, transform.Yaw, 0f);
        switch (objectId)
        {
            case Desk:
                SellDesk.SetLiveDeskTransform(locationId, transform.Position, rot);
                CustomerManager.ClearQueueCache();
                break;
            case Cashier:
                EmployeeSystem.SetLiveCashierTransform(locationId, transform.Position, rot);
                UpdateCashierMarker();
                break;
            case GrowTent:
                EmployeeSystem.SetLiveGrowTentTransform(locationId, transform.Position, rot);
                break;
            case Storage0:
                LocationSpawner.SetRackLiveTransform(locationId, 0, transform.Position, rot);
                break;
        }
    }

    private static void Save()
    {
        DumpWorkingPlacements("saved");
        foreach (var kvp in _working)
        {
            string payload = string.Join(":",
                _locationId,
                kvp.Key,
                kvp.Value.Position.x.ToString("F3", CultureInfo.InvariantCulture),
                kvp.Value.Position.y.ToString("F3", CultureInfo.InvariantCulture),
                kvp.Value.Position.z.ToString("F3", CultureInfo.InvariantCulture),
                NormalizeYaw(kvp.Value.Yaw).ToString("F2", CultureInfo.InvariantCulture));
            MogulNetwork.RequestAction(MogulActions.SetObjectPlacement, payload);
        }

        _active = false;
        DestroyCashierMarker();
        _original.Clear();
        _working.Clear();
        MelonLogger.Msg($"[Mogul] Move objects saved for {_locationId}");
        _locationId = null;
    }

    private static void Cancel()
    {
        DumpWorkingPlacements("cancelled");
        foreach (var kvp in _original)
            ApplyLiveTransform(_locationId, kvp.Key, kvp.Value);

        MelonLogger.Msg($"[Mogul] Move objects cancelled for {_locationId}");
        _active = false;
        DestroyCashierMarker();
        _original.Clear();
        _working.Clear();
        _locationId = null;
    }

    private static void ApplySavedPlacements()
    {
        foreach (var location in PropertySystem.Catalog)
        {
            SellDesk.ApplyPlacementOverrides(location.Id);
            EmployeeSystem.ApplyPlacementOverrides(location.Id);
            LocationSpawner.ApplyRackPlacementOverrides(location.Id);
        }
    }

    private static Vector3 ClampToRoom(Vector3 pos, GameObject buildingRoot)
    {
        var location = PropertySystem.Find(_locationId);
        if (location == null) return pos;

        var roomSize = BuildingPreview.GetEffectiveRoomSize(location);
        pos.x = Mathf.Clamp(pos.x, 0.2f, Mathf.Max(0.2f, roomSize.x - 0.2f));
        pos.y = Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, roomSize.y + 1.5f));
        pos.z = Mathf.Clamp(pos.z, 0.2f, Mathf.Max(0.2f, roomSize.z - 0.2f));
        return pos;
    }

    private static void EnsureCashierMarker(GameObject buildingRoot)
    {
        DestroyCashierMarker();
        if (buildingRoot == null || !_working.ContainsKey(Cashier)) return;

        _cashierMarker = new GameObject("Mogul_CashierPlacementMarker");
        _cashierMarker.transform.SetParent(buildingRoot.transform, false);

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "CashierMarker_Pole";
        pole.transform.SetParent(_cashierMarker.transform, false);
        pole.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        pole.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
        SetMarkerColor(pole, new Color(1f, 0.85f, 0.05f, 0.95f));

        var forward = GameObject.CreatePrimitive(PrimitiveType.Cube);
        forward.name = "CashierMarker_Forward";
        forward.transform.SetParent(_cashierMarker.transform, false);
        forward.transform.localPosition = new Vector3(0f, 1.55f, 0.45f);
        forward.transform.localScale = new Vector3(0.12f, 0.12f, 0.9f);
        SetMarkerColor(forward, new Color(0.05f, 0.7f, 1f, 0.95f));
    }

    private static void UpdateCashierMarker()
    {
        if (_cashierMarker == null) return;
        if (!_working.TryGetValue(Cashier, out var transform)) return;

        _cashierMarker.transform.localPosition = transform.Position;
        _cashierMarker.transform.localRotation = Quaternion.Euler(0f, transform.Yaw, 0f);
    }

    private static void DestroyCashierMarker()
    {
        if (_cashierMarker != null)
            UnityEngine.Object.Destroy(_cashierMarker);
        _cashierMarker = null;
    }

    private static void SetMarkerColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
            renderer.material.color = color;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);
    }

    private static void DumpWorkingPlacements(string reason)
    {
        MelonLogger.Msg($"[Mogul] Placement dump ({reason}) for {_locationId}:");
        foreach (var objectId in EditableObjects)
        {
            if (!_working.TryGetValue(objectId, out var t)) continue;
            MelonLogger.Msg(
                $"[Mogul] [PLACE] {_locationId} {objectId} local=({t.Position.x:F2}, {t.Position.y:F2}, {t.Position.z:F2}) yaw={NormalizeYaw(t.Yaw):F1}");
        }
    }

    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw < 0f) yaw += 360f;
        return yaw;
    }

    private static string ObjectLabel(string objectId) => objectId switch
    {
        Desk => "Counter/Register",
        Cashier => "Cashier Anchor",
        GrowTent => "Grow Tent",
        Storage0 => "Storage Rack",
        _ => objectId,
    };
}
