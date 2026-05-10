using System;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppSteamworks;
using MelonLoader;
using Mogul.Data;
using Newtonsoft.Json;
using S1API.Money;
using SteamNetworkLib;
using SteamNetworkLib.Sync;

namespace Mogul.Systems;

// Transport for Mogul state. Owns the live in-memory snapshot and the Steam sync layer.
// MP: host writes HostSyncVar → Steam lobby data → all clients receive automatically.
// Disk persistence lives in MogulPersistence and reaches in via GetSnapshot/ReplaceData.
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

    public static MogulSaveData GetSnapshot() => _localData;

    public static void ReplaceData(MogulSaveData data)
    {
        _localData = data ?? new MogulSaveData();
        OnDataChanged?.Invoke(_localData);
    }

    // Client-side: if we're connected and have a synced snapshot from the host, adopt it
    // and return true so callers (e.g. disk loader) can skip their own load.
    public static bool TryRefreshFromSync()
    {
        if (IsHost || _syncVar == null) return false;
        _localData = _syncVar.Value;
        OnDataChanged?.Invoke(_localData);
        return true;
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
            _client.RegisterMessageHandler<MogulRegisterPayoutMessage>(OnRegisterPayout);
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

    // Anyone can press R on a register; cash goes to whoever pressed it.
    // Host is authoritative on the balance — if zero by the time the host applies, nothing pays.
    public static void RequestCollectRegister(string locationId)
    {
        if (IsHost)
            ApplyCollect(locationId, _client?.LocalPlayerId ?? CSteamID.Nil);
        else
            _client?.BroadcastMessage(new MogulActionMessage { Action = MogulActions.CollectRegister, Payload = locationId });
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

    private static void OnClientAction(MogulActionMessage msg, CSteamID sender)
    {
        if (!IsHost) return;
        if (msg.Action == MogulActions.CollectRegister)
        {
            ApplyCollect(msg.Payload, sender);
            return;
        }
        ApplyAction(msg.Action, msg.Payload);
    }

    // Host -> client targeted message. Only the requester gets credited so collecting is fair.
    private static void OnRegisterPayout(MogulRegisterPayoutMessage msg, CSteamID _)
    {
        if (msg.Amount <= 0f) return;
        Money.ChangeCashBalance(msg.Amount, visualizeChange: true, playCashSound: true);
        MelonLogger.Msg($"[Mogul] Collected ${msg.Amount:F2} from {msg.LocationId} (host-paid)");
    }

    // Host-side: validate balance, zero it, sync, then either pay locally (if host pressed R)
    // or P2P-message the requesting client so only they get the cash.
    private static void ApplyCollect(string locationId, CSteamID requester)
    {
        if (!IsHost) return;
        if (string.IsNullOrEmpty(locationId)) return;
        if (!_localData.RegisterBalances.TryGetValue(locationId, out var balance) || balance <= 0f) return;

        _localData.RegisterBalances[locationId] = 0f;
        Commit();

        bool requesterIsHost = _client == null
            || requester == CSteamID.Nil
            || requester == _client.LocalPlayerId;

        if (requesterIsHost)
        {
            Money.ChangeCashBalance(balance, visualizeChange: true, playCashSound: true);
            MelonLogger.Msg($"[Mogul] Collected ${balance:F2} from {locationId}");
        }
        else
        {
            _client.SendMessageToPlayerAsync(requester, new MogulRegisterPayoutMessage
            {
                LocationId = locationId,
                Amount = balance
            });
            MelonLogger.Msg($"[Mogul] Paid ${balance:F2} from {locationId} -> {requester}");
        }
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
                        string locId = payload.Substring(0, sep);
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
                        string locId = payload.Substring(0, sep);
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

            case MogulActions.AddRegisterSale:
                {
                    int sep = payload.IndexOf(':');
                    if (sep > 0 && float.TryParse(payload.Substring(sep + 1),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float amount) && amount > 0f)
                    {
                        string locId = payload.Substring(0, sep);
                        if (!_localData.RegisterBalances.ContainsKey(locId))
                            _localData.RegisterBalances[locId] = 0f;
                        _localData.RegisterBalances[locId] += amount;
                        MelonLogger.Msg($"[Mogul] Register +${amount:F2} for {locId} (pending: ${_localData.RegisterBalances[locId]:F2})");
                        changed = true;
                    }
                    break;
                }
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

    public static void DumpStoragePrefabs()
    {
        try
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null) { MelonLogger.Warning("[Mogul] DumpPrefabs: NetworkManager null"); return; }
            var prefabs = nm.SpawnablePrefabs;
            if (prefabs == null) { MelonLogger.Warning("[Mogul] DumpPrefabs: SpawnablePrefabs null"); return; }

            int count = prefabs.GetObjectCount();
            MelonLogger.Msg($"[Mogul] === Storage-related prefabs (total {count}) ===");
            for (int i = 0; i < count; i++)
            {
                var obj = prefabs.GetObject(true, i);
                if (obj == null) continue;
                var name = obj.gameObject.name;
                var lower = name.ToLower();
                if (lower.Contains("shelf") || lower.Contains("rack") || lower.Contains("storage")
                    || lower.Contains("display") || lower.Contains("cabinet") || lower.Contains("crate"))
                    MelonLogger.Msg($"[Mogul]   [{i}] {name}");
            }
            MelonLogger.Msg("[Mogul] === End storage prefab dump ===");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Mogul] DumpPrefabs failed: " + ex.Message);
        }
    }

}
