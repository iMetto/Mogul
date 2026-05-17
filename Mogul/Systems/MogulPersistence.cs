using System;
using System.IO;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using MelonLoader;
using Mogul.Data;
using Newtonsoft.Json;
using S1API.Lifecycle;

namespace Mogul.Systems;

// Disk persistence for Mogul state.
// Reads/writes JSON to the game's per-save folder. Defers to MogulNetwork as the
// in-memory authority via GetSnapshot() / ReplaceData(). Clients that already have
// host-synced data skip the disk read.
public static class MogulPersistence
{
    public static void Initialize()
    {
        GameLifecycle.OnLoadComplete += LoadFromDisk;
        GameLifecycle.OnSaveStart += SaveToDisk;
    }

    public static void ResetData()
    {
        MogulNetwork.ReplaceData(new MogulSaveData());
        LocationSpawner.ClearSpawned();
    }

    private static string GetSavePath()
    {
        var loadMgr = Singleton<LoadManager>.Instance;
        string activePath = loadMgr?.ActiveSaveInfo?.SavePath;
        if (!string.IsNullOrEmpty(activePath))
            return Path.Combine(activePath, "Mogul", "save.json");

        var mgr = Singleton<SaveManager>.Instance;
        if (mgr == null) return null;
        if (string.IsNullOrEmpty(mgr.SaveName)) return null;
        return Path.Combine(mgr.PlayersSavePath, mgr.SaveName, "Mogul", "save.json");
    }

    private static void LoadFromDisk()
    {
        if (MogulNetwork.TryRefreshFromSync()) return;

        try
        {
            string path = GetSavePath();
            if (path == null || !File.Exists(path))
            {
                MogulNetwork.ReplaceData(new MogulSaveData());
                return;
            }
            var loaded = JsonConvert.DeserializeObject<MogulSaveData>(File.ReadAllText(path))
                         ?? new MogulSaveData();
            MogulNetwork.ReplaceData(loaded);
            MelonLogger.Msg("[Mogul] Save loaded from " + path);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] LoadFromDisk failed: " + ex.Message);
            MogulNetwork.ReplaceData(new MogulSaveData());
        }
    }

    private static void SaveToDisk()
    {
        try
        {
            string path = GetSavePath();
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(MogulNetwork.GetSnapshot()));
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] SaveToDisk failed: " + ex.Message);
        }
    }
}
