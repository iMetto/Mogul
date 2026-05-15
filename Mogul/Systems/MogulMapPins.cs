using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Map;
using Il2CppScheduleOne.Property;
using MelonLoader;
using UnityEngine;

namespace Mogul.Systems;

public static class MogulMapPins
{
    private static readonly Dictionary<string, GameObject> _pins = new();
    private static GameObject _cachedUIPrefab;

    public static int Count => _pins.Count;

    public static void Initialize()
    {
        MogulNetwork.OnDataChanged += _ => SyncPins();
    }

    public static void SyncPins()
    {
        CleanupAll();

        foreach (var locId in MogulNetwork.Data.RegisteredLocationIds)
        {
            var location = PropertySystem.Find(locId);
            if (location == null) continue;
            CreatePin("property:" + locId, location.Name + "\n(Mogul)", BuildingPreview.GetEffectiveWorldPosition(location));
        }

        var activeQuest = MogulQuestSystem.Find(MogulNetwork.Data.ActiveQuestId);
        if (activeQuest != null && activeQuest.WorldPosition != Vector3.zero)
            CreatePin("quest:" + activeQuest.Id, activeQuest.Title + "\n(Mogul job)", activeQuest.WorldPosition);
    }

    public static void CleanupAll()
    {
        foreach (var kvp in _pins)
            if (kvp.Value != null)
                UnityEngine.Object.Destroy(kvp.Value);
        _pins.Clear();
    }

    private static void CreatePin(string key, string label, Vector3 worldPos)
    {
        if (_pins.ContainsKey(key)) return;

        try
        {
            var go = new GameObject("Mogul_MapPin_" + key.Replace(':', '_'));
            go.SetActive(false);
            go.transform.position = new Vector3(worldPos.x, 0f, worldPos.z);

            var poi = go.AddComponent<POI>();
            var uiPrefab = GetUIPrefab();
            if (uiPrefab != null)
                poi.UIPrefab = uiPrefab;
            poi.SetMainText(label);
            go.SetActive(true);

            _pins[key] = go;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[Mogul] Map pin '{key}' failed: {ex.Message}");
        }
    }

    private static GameObject GetUIPrefab()
    {
        if (_cachedUIPrefab != null) return _cachedUIPrefab;

        var props = UnityEngine.Object.FindObjectsOfType<Property>();
        if (props == null) return null;

        for (int i = 0; i < props.Length; i++)
        {
            var uiPrefab = props[i]?.PoI?.UIPrefab;
            if (uiPrefab == null) continue;
            _cachedUIPrefab = uiPrefab;
            return _cachedUIPrefab;
        }

        return null;
    }
}
