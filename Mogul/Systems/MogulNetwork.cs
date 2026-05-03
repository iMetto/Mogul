using System;
using System.IO;
using Il2CppSteamworks;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Persistence;
using MelonLoader;
using Mogul.Data;
using Newtonsoft.Json;
using S1API.Lifecycle;
using SteamNetworkLib;
using SteamNetworkLib.Sync;

namespace Mogul.Systems;

// Single source of truth for all Mogul state.
// SP: data lives in the game's own save folder (per save-file).
// MP: host writes HostSyncVar → Steam lobby data → all clients receive automatically.
public static class MogulNetwork
{
    private static SteamNetworkClient _client;
    private static HostSyncVar<MogulSaveData> _syncVar;
    private static MogulSaveData _localData = new MogulSaveData();

    public static bool IsHost => _client == null || !_client.IsInLobby || _client.IsHost;

    public static string GetDesignId(string locationId) =>
        _localData.LocationDesigns.TryGetValue(locationId, out var d) ? d : "industrial";
    public static MogulSaveData Data => _localData;

    public static event Action<MogulSaveData> OnDataChanged;

    public static void Initialize()
    {
        GameLifecycle.OnLoadComplete += LoadFromDisk;
        GameLifecycle.OnSaveStart += SaveToDisk;
    }

    public static void InitializeSteam()
    {
        if (_client != null) return;
        _client = new SteamNetworkClient();
        try
        {
            _client.Initialize();
            _client.OnLobbyJoined += (_, _) => OnJoinedLobby();
            _client.OnLobbyLeft += (_, _) => _syncVar = null;
            _client.RegisterMessageHandler<MogulActionMessage>(OnClientAction);
            _client.RegisterMessageHandler<MogulSyncMessage>(OnSyncReceived);
            MelonLogger.Msg("[Mogul] SteamNetworkClient initialized.");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] SteamNetworkClient init skipped: " + ex.Message);
            _client = null;
        }
    }

    public static void Tick()
    {
        _client?.ProcessIncomingMessages();
    }

    // Host (or SP): applies immediately.
    // Client: sends the request to the host via P2P.
    public static void RequestAction(string action, string payload = "")
    {
        if (IsHost)
            ApplyAction(action, payload);
        else
            _client?.BroadcastMessage(new MogulActionMessage { Action = action, Payload = payload });
    }

    private static void OnJoinedLobby()
    {
        _syncVar = _client.CreateHostSyncVar("mogul_save", _localData);
        _syncVar.OnValueChanged += (_, newVal) =>
        {
            _localData = newVal;
            OnDataChanged?.Invoke(_localData);
        };

        if (!IsHost)
        {
            _localData = _syncVar.Value;
            OnDataChanged?.Invoke(_localData);
        }
    }

    private static void OnClientAction(MogulActionMessage msg, CSteamID _)
    {
        if (!IsHost) return;
        ApplyAction(msg.Action, msg.Payload);
    }

    private static void OnSyncReceived(MogulSyncMessage msg, CSteamID _)
    {
        if (IsHost) return;
        _localData = JsonConvert.DeserializeObject<MogulSaveData>(msg.Json) ?? new MogulSaveData();
        OnDataChanged?.Invoke(_localData);
    }

    private static void ApplyAction(string action, string payload)
    {
        bool changed = false;

        switch (action)
        {
            case MogulActions.RegisterLocation:
                if (!string.IsNullOrEmpty(payload) && !_localData.RegisteredLocationIds.Contains(payload))
                {
                    _localData.RegisteredLocationIds.Add(payload);
                    changed = true;
                }
                break;

            case MogulActions.UnregisterLocation:
                changed = _localData.RegisteredLocationIds.Remove(payload);
                break;

            case MogulActions.SetLocationDesign:
            {
                int sep = payload.IndexOf(':');
                if (sep > 0)
                {
                    string locId    = payload.Substring(0, sep);
                    string designId = payload.Substring(sep + 1);
                    _localData.LocationDesigns[locId] = designId;
                    changed = true;
                }
                break;
            }

            case MogulActions.PurchaseWithDesign:
            {
                // payload = "locationId:designId" — atomically registers the location
                // and sets its design so SpawnBuilding always sees the correct design.
                int sep = payload.IndexOf(':');
                if (sep > 0)
                {
                    string locId    = payload.Substring(0, sep);
                    string designId = payload.Substring(sep + 1);
                    if (!_localData.RegisteredLocationIds.Contains(locId))
                    {
                        _localData.RegisteredLocationIds.Add(locId);
                        _localData.LocationDesigns[locId] = designId;
                        changed = true;
                    }
                }
                break;
            }

            case MogulActions.AddReach:
                if (int.TryParse(payload, out int gain) && gain > 0)
                {
                    _localData.Reach += gain;
                    changed = true;
                }
                break;

            case MogulActions.SetActiveQuest:
                _localData.ActiveQuestId = payload;
                _localData.ActiveQuestProgress = 0;
                changed = true;
                break;

            case MogulActions.AdvanceQuest:
                if (int.TryParse(payload, out int steps))
                {
                    _localData.ActiveQuestProgress += steps;
                    changed = true;
                }
                break;
        }

        if (changed)
            Commit();
    }

    private static void Commit()
    {
        if (_syncVar != null && IsHost)
            _syncVar.Value = _localData;

        if (IsHost && _client != null)
            _client.BroadcastMessage(new MogulSyncMessage { Json = JsonConvert.SerializeObject(_localData) });

        OnDataChanged?.Invoke(_localData);
    }

    private static string GetSavePath()
    {
        var mgr = Singleton<SaveManager>.Instance;
        if (mgr == null) return null;
        return Path.Combine(mgr.PlayersSavePath, mgr.SaveName, "Mogul", "save.json");
    }

    private static void LoadFromDisk()
    {
        if (!IsHost && _syncVar != null)
        {
            _localData = _syncVar.Value;
            OnDataChanged?.Invoke(_localData);
            return;
        }
        try
        {
            string path = GetSavePath();
            if (path == null || !File.Exists(path))
            {
                _localData = new MogulSaveData();
                return;
            }
            _localData = JsonConvert.DeserializeObject<MogulSaveData>(File.ReadAllText(path))
                         ?? new MogulSaveData();
            MelonLogger.Msg("[Mogul] Save loaded from " + path);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] LoadFromDisk failed: " + ex.Message);
            _localData = new MogulSaveData();
        }
    }

    private static void SaveToDisk()
    {
        try
        {
            string path = GetSavePath();
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(_localData));
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] SaveToDisk failed: " + ex.Message);
        }
    }
}
