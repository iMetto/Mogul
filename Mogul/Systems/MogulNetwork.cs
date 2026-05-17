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
        EnsureDataCollections(_localData);
        OnDataChanged?.Invoke(_localData);
    }

    // Client-side: if we're connected and have a synced snapshot from the host, adopt it
    // and return true so callers (e.g. disk loader) can skip their own load.
    public static bool TryRefreshFromSync()
    {
        if (IsHost || _syncVar == null) return false;
        _localData = _syncVar.Value;
        EnsureDataCollections(_localData);
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

    public static bool RequestFulfillOnlineOrder(string orderId, out string error)
    {
        error = null;
        if (!IsHost)
        {
            _client?.BroadcastMessage(new MogulActionMessage { Action = MogulActions.FulfillOnlineOrder, Payload = orderId });
            return true;
        }

        EnsureDataCollections(_localData);
        if (OnlineOrderSystem.TryFulfillOrder(_localData, orderId, out error))
        {
            MelonLogger.Msg($"[Mogul] Online order fulfilled: {orderId}");
            Commit();
            return true;
        }

        if (!string.IsNullOrEmpty(error))
            MelonLogger.Warning($"[Mogul] Online order fulfill failed ({orderId}): {error}");
        return false;
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
            EnsureDataCollections(_localData);
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
        EnsureDataCollections(_localData);
        OnDataChanged?.Invoke(_localData);
    }

    private static void ApplyAction(string action, string payload)
    {
        EnsureDataCollections(_localData);
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
                        if (!PropertySystem.IsPurchasable(locId, _localData))
                            break;
                        if (!_localData.RegisteredLocationIds.Contains(locId))
                        {
                            _localData.RegisteredLocationIds.Add(locId);
                            _localData.LocationDesigns[locId] = designId;
                            _localData.LocationObjectPlacements.Remove(locId);
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
                if (MogulQuestSystem.Find(payload) != null)
                {
                    _localData.ActiveQuestId = payload;
                    _localData.ActiveQuestProgress = 0;
                    changed = true;
                }
                break;

            case MogulActions.AdvanceQuest:
                if (int.TryParse(payload, out int steps))
                {
                    _localData.ActiveQuestProgress += steps;
                    changed = true;
                }
                break;

            case MogulActions.CompleteQuest:
                {
                    var quest = MogulQuestSystem.Find(payload);
                    if (quest == null) break;
                    if (MogulQuestSystem.IsClaimed(quest, _localData)) break;
                    if (!MogulQuestSystem.IsComplete(quest, _localData)) break;

                    MogulQuestSystem.Claim(quest, _localData);
                    if (_localData.ActiveQuestId == quest.Id)
                    {
                        _localData.ActiveQuestId = null;
                        _localData.ActiveQuestProgress = 0;
                    }
                    changed = true;
                    break;
                }

            case MogulActions.RecordObjectiveEvent:
                {
                    int sep = payload.LastIndexOf(':');
                    if (sep > 0 && int.TryParse(payload.Substring(sep + 1), out int amount) && amount > 0)
                    {
                        string key = payload.Substring(0, sep);
                        MogulQuestSystem.AddProgress(_localData, key, amount);
                        changed = true;
                    }
                    break;
                }

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

            case MogulActions.HireEmployee:
                {
                    int sep = payload.IndexOf(':');
                    if (sep > 0 && Enum.TryParse<EmployeeRole>(payload.Substring(sep + 1), out var role))
                    {
                        if (role != EmployeeRole.Cashier && role != EmployeeRole.Budtender)
                            break;

                        string locId = payload.Substring(0, sep);
                        if (!_localData.RegisteredLocationIds.Contains(locId))
                            break;

                        if (!_localData.LocationEmployees.TryGetValue(locId, out var employees))
                        {
                            employees = new System.Collections.Generic.List<HiredEmployeeData>();
                            _localData.LocationEmployees[locId] = employees;
                        }

                        bool alreadyHired = false;
                        foreach (var employee in employees)
                            if (employee.Role == role)
                            {
                                alreadyHired = true;
                                break;
                            }
                        if (alreadyHired) break;

                        employees.Add(new HiredEmployeeData
                        {
                            Id = $"{locId}_{role}_{employees.Count + 1}",
                            Role = role,
                            DisplayName = role switch
                            {
                                EmployeeRole.Cashier   => "Casey Cashier",
                                EmployeeRole.Budtender => "Bailey Budtender",
                                _                      => role.ToString(),
                            },
                        });
                        changed = true;
                    }
                    break;
                }

            case MogulActions.FireEmployee:
                {
                    int sep = payload.IndexOf(':');
                    if (sep > 0 && Enum.TryParse<EmployeeRole>(payload.Substring(sep + 1), out var role))
                    {
                        if (role != EmployeeRole.Cashier && role != EmployeeRole.Budtender)
                            break;

                        string locId = payload.Substring(0, sep);
                        if (!_localData.RegisteredLocationIds.Contains(locId))
                            break;

                        if (!_localData.LocationEmployees.TryGetValue(locId, out var employees))
                            break;

                        for (int i = employees.Count - 1; i >= 0; i--)
                        {
                            if (employees[i] == null || employees[i].Role != role) continue;
                            employees.RemoveAt(i);
                            changed = true;
                            break;
                        }

                        if (employees.Count == 0)
                            _localData.LocationEmployees.Remove(locId);
                    }
                    break;
                }

            case MogulActions.AddVirtualInventory:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int qty) && qty > 0)
                    {
                        string locId = parts[0];
                        string itemId = parts[1];
                        if (!_localData.LocationVirtualInventory.TryGetValue(locId, out var inv))
                        {
                            inv = new System.Collections.Generic.Dictionary<string, int>();
                            _localData.LocationVirtualInventory[locId] = inv;
                        }
                        inv.TryGetValue(itemId, out int existing);
                        inv[itemId] = existing + qty;
                        changed = true;
                    }
                    break;
                }

            case MogulActions.StartBudtenderOrder:
                {
                    if (StrainMixingSystem.TryParseOrderPayload(payload, out var request))
                    {
                        string locId = request.LocationId;
                        string baseProductId = request.BaseProductId;
                        if (!_localData.RegisteredLocationIds.Contains(locId)) break;
                        if (_localData.LocationBudtenderOrders.ContainsKey(locId)) break;
                        if (!EmployeeProduction.TryGetBudtenderProduct(baseProductId, out _)) break;
                        if (request.IngredientIds.Count > StrainMixingSystem.MaxIngredientSlots) break;
                        if (!_localData.LocationEmployees.TryGetValue(locId, out var employees)) break;
                        if (EmployeeProduction.CountRole(employees, EmployeeRole.Budtender) <= 0) break;
                        if (!StrainMixingSystem.TryResolveRecipeProduct(baseProductId, request.IngredientIds, out var productId, out var displayName, out var mixError))
                        {
                            MelonLogger.Warning($"[Mogul] Budtender order rejected: {mixError ?? "mix failed"}");
                            break;
                        }

                        int currentDay = EmployeeSystem.GetCurrentGameDay();
                        int currentTime = EmployeeSystem.GetCurrentGameTime();

                        _localData.LocationBudtenderOrders[locId] = new BudtenderOrderData
                        {
                            ProductId = productId,
                            BaseProductId = baseProductId,
                            IngredientIds = request.IngredientIds,
                            DisplayName = displayName,
                            Quantity = EmployeeProduction.BudtenderStackQuantity,
                            StartDay = currentDay,
                            StartTime = currentTime,
                            ReadyDay = EmployeeProduction.GetReadyDay(currentDay, currentTime),
                        };
                        changed = true;
                    }
                    break;
                }

            case MogulActions.CompleteBudtenderOrder:
                {
                    string locId = payload;
                    if (string.IsNullOrEmpty(locId)) break;
                    if (!_localData.LocationBudtenderOrders.TryGetValue(locId, out var order)) break;
                    if (!EmployeeProduction.IsOrderComplete(order, EmployeeSystem.GetCurrentGameDay(), EmployeeSystem.GetCurrentGameTime())) break;

                    string storageError = "building not spawned";
                    bool stored = LocationSpawner.TryGetSpawnedBuilding(locId, out var buildingRoot)
                        && StorageScanner.TryAddProductStack(buildingRoot, order.ProductId, order.Quantity, out storageError);
                    if (!stored)
                    {
                        if (!_localData.LocationVirtualInventory.TryGetValue(locId, out var inv))
                        {
                            inv = new System.Collections.Generic.Dictionary<string, int>();
                            _localData.LocationVirtualInventory[locId] = inv;
                        }
                        inv.TryGetValue(order.ProductId, out int existing);
                        inv[order.ProductId] = existing + order.Quantity;
                        MelonLogger.Warning($"[Mogul] Budtender storage deposit failed for {locId}: {storageError ?? "unknown"}; added virtual stock");
                    }
                    _localData.LocationBudtenderOrders.Remove(locId);
                    MelonLogger.Msg($"[Mogul] Budtender completed {order.Quantity} {StrainMixingSystem.BuildOrderDisplayName(order)} at {locId} ({(stored ? "storage" : "virtual")})");
                    changed = true;
                    break;
                }

            case MogulActions.SetObjectPlacement:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 6
                        && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                        && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                        && float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z)
                        && float.TryParse(parts[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float yaw))
                    {
                        string locId = parts[0];
                        string objectId = parts[1];
                        if (!_localData.RegisteredLocationIds.Contains(locId) || string.IsNullOrEmpty(objectId))
                            break;

                        if (!_localData.LocationObjectPlacements.TryGetValue(locId, out var placements))
                        {
                            placements = new System.Collections.Generic.Dictionary<string, MogulObjectPlacementData>();
                            _localData.LocationObjectPlacements[locId] = placements;
                        }

                        placements[objectId] = new MogulObjectPlacementData
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            Yaw = yaw,
                        };
                        changed = true;
                    }
                    break;
                }

            case MogulActions.ClearObjectPlacements:
                {
                    if (!string.IsNullOrEmpty(payload))
                        changed = _localData.LocationObjectPlacements.Remove(payload);
                    break;
                }

            case MogulActions.GenerateOnlineOrder:
                {
                    changed = OnlineOrderSystem.TryGenerateOrder(_localData, out var order);
                    if (changed)
                        MelonLogger.Msg($"[Mogul] Online order {order.Id} created for {order.LocationId}: ${order.Total:F0}");
                    break;
                }

            case MogulActions.FulfillOnlineOrder:
                {
                    if (OnlineOrderSystem.TryFulfillOrder(_localData, payload, out var error))
                    {
                        MelonLogger.Msg($"[Mogul] Online order fulfilled: {payload}");
                        changed = true;
                    }
                    else if (!string.IsNullOrEmpty(error))
                    {
                        MelonLogger.Warning($"[Mogul] Online order fulfill failed ({payload}): {error}");
                    }
                    break;
                }

            case MogulActions.DismissOnlineOrder:
                {
                    changed = OnlineOrderSystem.TryDismissOrder(_localData, payload);
                    break;
                }

            case MogulActions.SetLocationPriceMultiplier:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 2
                        && _localData.RegisteredLocationIds.Contains(parts[0])
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
                    {
                        PricingSystem.SetLocationMultiplier(_localData, parts[0], multiplier);
                        changed = true;
                    }
                    break;
                }

            case MogulActions.SetProductPriceMultiplier:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 4
                        && _localData.RegisteredLocationIds.Contains(parts[0])
                        && int.TryParse(parts[2], out var quality)
                        && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var multiplier))
                    {
                        PricingSystem.SetProductMultiplier(_localData, parts[0], parts[1], quality, multiplier);
                        changed = true;
                    }
                    break;
                }

            case MogulActions.SetProductManualPrice:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 4
                        && _localData.RegisteredLocationIds.Contains(parts[0])
                        && int.TryParse(parts[2], out var quality)
                        && float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var price))
                    {
                        PricingSystem.SetManualPrice(_localData, parts[0], parts[1], quality, price);
                        changed = true;
                    }
                    break;
                }

            case MogulActions.ClearProductManualPrice:
                {
                    var parts = payload.Split(':');
                    if (parts.Length == 3
                        && _localData.RegisteredLocationIds.Contains(parts[0])
                        && int.TryParse(parts[2], out var quality))
                    {
                        PricingSystem.ClearManualPrice(_localData, parts[0], parts[1], quality);
                        changed = true;
                    }
                    break;
                }
        }

        if (changed)
            Commit();
    }

    private static void EnsureDataCollections(MogulSaveData data)
    {
        data.RegisteredLocationIds ??= new System.Collections.Generic.List<string>();
        data.LocationDesigns ??= new System.Collections.Generic.Dictionary<string, string>();
        data.CompletedQuestIds ??= new System.Collections.Generic.List<string>();
        data.ObjectiveProgress ??= new System.Collections.Generic.Dictionary<string, int>();
        data.UnlockedFeatureIds ??= new System.Collections.Generic.List<string>();
        data.RegisterBalances ??= new System.Collections.Generic.Dictionary<string, float>();
        data.LocationEmployees ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<HiredEmployeeData>>();
        data.LocationVirtualInventory ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>();
        data.LocationBudtenderProductionDay ??= new System.Collections.Generic.Dictionary<string, int>();
        data.LocationBudtenderOrders ??= new System.Collections.Generic.Dictionary<string, BudtenderOrderData>();
        data.LocationObjectPlacements ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, MogulObjectPlacementData>>();
        data.OnlineOrders ??= new System.Collections.Generic.List<OnlineOrderData>();
        foreach (var order in data.OnlineOrders)
            if (order != null)
                order.Lines ??= new System.Collections.Generic.List<OnlineOrderLineData>();
        data.LocationPriceMultipliers ??= new System.Collections.Generic.Dictionary<string, float>();
        data.LocationProductPrices ??= new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, ProductPriceData>>();
                foreach (var map in data.LocationProductPrices.Values)
                    if (map != null)
                {
                    var keys = new System.Collections.Generic.List<string>(map.Keys);
                    foreach (var key in keys)
                    {
                        map[key] ??= new ProductPriceData();
                        map[key].Multiplier = PricingSystem.ClampMultiplier(map[key].Multiplier);
                    }
                }
    }

    private static void Commit()
    {
        if (_syncVar != null && IsHost)
            _syncVar.Value = _localData;

        if (IsHost && _client != null)
            _client.BroadcastMessage(new MogulSyncMessage { Json = JsonConvert.SerializeObject(_localData) });

        OnDataChanged?.Invoke(_localData);
    }

}
