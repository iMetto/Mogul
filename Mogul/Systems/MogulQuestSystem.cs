using System;
using System.Collections.Generic;
using Mogul.Data;
using S1API.Money;
using UnityEngine;

namespace Mogul.Systems;

public enum MogulObjectiveType
{
    Quest,
    Task,
}

public enum MogulObjectiveEvent
{
    CashThreshold,
    KnockoutNpc,
    KillNpc,
    PickpocketNpc,
    SellDrug,
    SellPawnItem,
    DumpBody,
    HandOutDrug,
    DropOffDrug,
}

public class MogulQuestDefinition
{
    public string Id { get; init; } = "";
    public MogulObjectiveType Type { get; init; } = MogulObjectiveType.Task;
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Objective { get; init; } = "";
    public int Target { get; init; } = 1;
    public int ReachReward { get; init; }
    public MogulObjectiveEvent Event { get; init; }
    public string TargetId { get; init; } = "";
    public string LocationId { get; init; } = "";
    public float WorldX { get; init; }
    public float WorldY { get; init; }
    public float WorldZ { get; init; }
    public Vector3 WorldPosition => new(WorldX, WorldY, WorldZ);
    public float Radius { get; init; } = 5f;
    public Func<MogulSaveData, int> Progress { get; init; }
    public Func<MogulSaveData, bool> IsAvailable { get; init; } = _ => true;
    public Action<MogulSaveData> OnClaim { get; init; }
}

public static class MogulQuestSystem
{
    public const string UnlockPropertiesTab = "properties_tab";
    public const string UnlockWestvillePurchase = "westville_purchase";
    public const string UnlockWestvilleUpgrade = "westville_upgrade";
    public const string RevealDowntown = "downtown_revealed";
    public const string UnlockDowntownPurchase = "downtown_purchase";

    public static readonly IReadOnlyList<MogulQuestDefinition> Quests = new List<MogulQuestDefinition>
    {
        new()
        {
            Id          = "loose_end_01",
            Type        = MogulObjectiveType.Quest,
            Title       = "Loose End",
            Description = "A runner has been talking sideways about the operation. Find them at the marked spot and make sure that conversation stops.",
            Objective   = "Take out the mark",
            Target      = 1,
            ReachReward = 300,
            Event       = MogulObjectiveEvent.KnockoutNpc,
            TargetId    = "loose_end_mark_01",
            WorldX      = -148f,
            WorldY      = -3.13f,
            WorldZ      = 68f,
            Radius      = 15f,
            IsAvailable = _ => true,
        },
        new()
        {
            Id = "westville_statement",
            Type = MogulObjectiveType.Quest,
            Title = "Open For Business",
            Description = "Show the operation has enough money behind it. Hit three grand between cash and the app, then claim your first property lead.",
            Objective = "Have $3,000 total",
            Target = 1,
            ReachReward = 500,
            Event = MogulObjectiveEvent.CashThreshold,
            TargetId = "cash_threshold_3000",
            LocationId = "westville",
            WorldX = -167.08f,
            WorldY = -3.13f,
            WorldZ = 73.55f,
            Radius = 30f,
            Progress = data => GetProgress(data, "cash_threshold_3000"),
            IsAvailable = data => GetProgress(data, "cash_threshold_3000") >= 1,
            OnClaim = data =>
            {
                Unlock(data, UnlockPropertiesTab);
                Unlock(data, UnlockWestvillePurchase);
            },
        },
        new()
        {
            Id          = "shipment_westville_01",
            Type        = MogulObjectiveType.Quest,
            Title       = "Dead Drop",
            Description = "A contact has a storage unit near the property. Get forty packs of OG Kush inside it. No names, no trail.",
            Objective   = "Drop 40 OG Kush at the storage unit",
            Target      = 40,
            ReachReward = 1200,
            Event       = MogulObjectiveEvent.DropOffDrug,
            TargetId    = EmployeeProduction.TestBudtenderProductId,
            WorldX      = -158f,
            WorldY      = -3.13f,
            WorldZ      = 85f,
            Radius      = 5f,
            IsAvailable = data => HasClaimed(data, "westville_statement"),
            OnClaim     = _ => MogulDropZoneSpawner.OnQuestClaimed("shipment_westville_01"),
        },
        new()
        {
            Id = "wet_badge",
            Type = MogulObjectiveType.Quest,
            Title = "Wet The Badge",
            Description = "Westville is eating. Heat follows money. Drop a cop at the marked spot, drag the lesson to the water, and let the current carry the paperwork.",
            Objective = "Drop a cop and dump the body",
            Target = 2,
            ReachReward = 1500,
            Event = MogulObjectiveEvent.DumpBody,
            TargetId = "police_water_drop_01",
            LocationId = "westville_water",
            WorldX = -132f,
            WorldY = -3f,
            WorldZ = 104f,
            Radius = 10f,
            Progress = data => GetProgress(data, BuildEventKey(MogulObjectiveEvent.KnockoutNpc, "police_water_drop_01"))
                              + GetProgress(data, BuildEventKey(MogulObjectiveEvent.DumpBody, "police_water_drop_01")),
            IsAvailable = data => HasClaimed(data, "westville_statement")
                                 && data.Reach >= 2500
                                 && CountVirtualProduct(data, EmployeeProduction.TestBudtenderProductId) >= 40,
            OnClaim = data =>
            {
                Unlock(data, UnlockWestvilleUpgrade);
                Unlock(data, RevealDowntown);
            },
        },
        new()
        {
            Id = "meet_and_greet",
            Type = MogulObjectiveType.Quest,
            Title = "Meet And Greet",
            Description = "Make enough OG Kush to stop looking small. Bring eighty packs to the handoff and let the crowd learn your name for free.",
            Objective = "Hand out 80 OG Kush",
            Target = 80,
            ReachReward = 3500,
            Event = MogulObjectiveEvent.HandOutDrug,
            TargetId = EmployeeProduction.TestBudtenderProductId,
            LocationId = "downtown_handoff",
            WorldX = 105f,
            WorldY = 1.15f,
            WorldZ = -3.29f,
            Radius = 12f,
            IsAvailable = data => HasClaimed(data, "wet_badge")
                                 && data.Reach >= 7500
                                 && CountVirtualProduct(data, EmployeeProduction.TestBudtenderProductId) >= 80,
            OnClaim = data => Unlock(data, UnlockDowntownPurchase),
        },
    };

    public static readonly IReadOnlyList<MogulQuestDefinition> Tasks = new List<MogulQuestDefinition>
    {
        Task("dip_pockets_westville", "Light Fingers", "A quiet lift travels farther than a loud sale. Pick the marked pocket and keep moving.", "Pickpocket the mark", 1, 180, MogulObjectiveEvent.PickpocketNpc, "task_pickpocket_westville"),
        Task("sleep_debt_collector", "Past Due", "Some people only understand interest when it hits the floor. Drop the marked collector.", "Knock out the collector", 1, 220, MogulObjectiveEvent.KnockoutNpc, "task_collector_01"),
        Task("move_ogkush_12", "Green Samples", "Push twelve packs of OG Kush through street sales. No speeches, just product.", "Sell 12 OG Kush", 12, 240, MogulObjectiveEvent.SellDrug, EmployeeProduction.TestBudtenderProductId),
        Task("pawn_clean_watch", "No Receipts", "Take the marked piece to the pawn shop and make it someone else's memory.", "Sell the marked item", 1, 160, MogulObjectiveEvent.SellPawnItem, "stolen_watch"),
        Task("quiet_regular", "Customer Service", "A regular is talking sideways. Find the mark, end the conversation, leave before applause.", "Knock out the regular", 1, 210, MogulObjectiveEvent.KnockoutNpc, "task_regular_01"),
        Task("move_sourdiesel_10", "Diesel Run", "Ten Sour Diesel packs, ten new rumors. Keep the price firm.", "Sell 10 Sour Diesel", 10, 260, MogulObjectiveEvent.SellDrug, "sourdiesel"),
        Task("pawn_burner_phone", "Dead Line", "A burner with history still has resale value. Pawn it and close the loop.", "Pawn the burner", 1, 170, MogulObjectiveEvent.SellPawnItem, "burner_phone"),
        Task("pickpocket_clubrat", "Cover Charge", "The club rat owes the room. Lift the cash without making a scene.", "Pickpocket the club rat", 1, 190, MogulObjectiveEvent.PickpocketNpc, "task_clubrat_01"),
        Task("move_greencrack_10", "Fast Green", "Move ten Green Crack packs to people who do not ask twice.", "Sell 10 Green Crack", 10, 280, MogulObjectiveEvent.SellDrug, "greencrack"),
        Task("drop_runner", "Bad Courier", "A courier is shopping your route. Put them down and change the story.", "Knock out the courier", 1, 230, MogulObjectiveEvent.KnockoutNpc, "task_runner_01"),
        Task("pawn_chain", "Cold Chain", "Fence the marked chain before anyone gets sentimental.", "Pawn the chain", 1, 180, MogulObjectiveEvent.SellPawnItem, "stolen_chain"),
        Task("pickpocket_beach", "Beach Tax", "The beach mark is careless with cash. Correct that.", "Pickpocket the beach mark", 1, 200, MogulObjectiveEvent.PickpocketNpc, "task_beach_01"),
        Task("move_gdp_8", "Purple Favor", "Eight Granddaddy Purple packs to the right hands. Make it feel exclusive.", "Sell 8 Granddaddy Purple", 8, 300, MogulObjectiveEvent.SellDrug, "granddaddypurple"),
        Task("sleep_loudmouth", "Volume Down", "The loudmouth is bad for business. Drop them and let silence advertise.", "Knock out the loudmouth", 1, 220, MogulObjectiveEvent.KnockoutNpc, "task_loudmouth_01"),
        Task("pawn_ring", "Loose Stone", "Pawn the marked ring. Nobody needs the story attached to it.", "Pawn the ring", 1, 190, MogulObjectiveEvent.SellPawnItem, "stolen_ring"),
        Task("pickpocket_suit", "Soft Target", "The suit counts money like nobody is watching. Watch closer.", "Pickpocket the suit", 1, 210, MogulObjectiveEvent.PickpocketNpc, "task_suit_01"),
        Task("move_ogkush_20", "House Green", "Twenty OG Kush packs through the street. Familiar product, cleaner rhythm.", "Sell 20 OG Kush", 20, 360, MogulObjectiveEvent.SellDrug, EmployeeProduction.TestBudtenderProductId),
        Task("drop_bouncer", "Door Problem", "A bouncer is leaning on your people. Put the weight back on him.", "Knock out the bouncer", 1, 260, MogulObjectiveEvent.KnockoutNpc, "task_bouncer_01"),
        Task("pawn_camera", "Bad Angle", "Pawn the camera before its owner remembers what was on it.", "Pawn the camera", 1, 190, MogulObjectiveEvent.SellPawnItem, "stolen_camera"),
        Task("pickpocket_dealer", "Market Research", "A rival dealer's pockets know more than their mouth. Take notes in cash.", "Pickpocket the rival", 1, 240, MogulObjectiveEvent.PickpocketNpc, "task_rival_dealer_01"),
    };

    public static IEnumerable<MogulQuestDefinition> All
    {
        get
        {
            foreach (var quest in Quests) yield return quest;
            foreach (var task in Tasks) yield return task;
        }
    }

    public static void Tick()
    {
        if (!MogulNetwork.IsHost) return;

        float totalMoney;
        try
        {
            totalMoney = Money.GetCashBalance() + Money.GetOnlineBalance();
        }
        catch
        {
            return;
        }

        if (totalMoney >= 3000f && GetProgress(MogulNetwork.Data, "cash_threshold_3000") <= 0)
            MogulNetwork.RequestAction(MogulActions.RecordObjectiveEvent, "cash_threshold_3000:1");

        MogulQuestNpcSpawner.Tick();
        MogulDropZoneSpawner.Tick();
    }

    public static MogulQuestDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var objective in All)
            if (objective.Id == id)
                return objective;
        return null;
    }

    public static IReadOnlyList<MogulQuestDefinition> GetAvailable(MogulObjectiveType type, MogulSaveData data)
    {
        var source = type == MogulObjectiveType.Quest ? Quests : Tasks;
        var list = new List<MogulQuestDefinition>();
        foreach (var objective in source)
            if (objective.IsAvailable(data) && !IsClaimed(objective, data))
                list.Add(objective);
        return list;
    }

    public static int GetProgress(MogulQuestDefinition quest, MogulSaveData data)
    {
        if (quest == null || data == null) return 0;
        if (quest.Progress != null) return Math.Max(0, quest.Progress(data));
        return GetProgress(data, quest.Id);
    }

    public static bool IsComplete(MogulQuestDefinition quest, MogulSaveData data) =>
        quest != null && GetProgress(quest, data) >= quest.Target;

    public static bool IsAccepted(MogulQuestDefinition quest, MogulSaveData data) =>
        quest != null && data?.AcceptedQuestIds != null && data.AcceptedQuestIds.Contains(quest.Id);

    public static bool IsClaimed(MogulQuestDefinition quest, MogulSaveData data) =>
        quest != null && HasClaimed(data, quest.Id);

    public static bool HasClaimed(MogulSaveData data, string id) =>
        data?.CompletedQuestIds != null && data.CompletedQuestIds.Contains(id);

    public static bool IsUnlocked(string flag) =>
        IsUnlocked(MogulNetwork.Data, flag);

    public static bool IsUnlocked(MogulSaveData data, string flag) =>
        !string.IsNullOrEmpty(flag) && data?.UnlockedFeatureIds != null && data.UnlockedFeatureIds.Contains(flag);

    public static void RequestAccept(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        MogulNetwork.RequestAction(MogulActions.AcceptQuest, questId);
    }

    public static void RequestTrack(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        MogulNetwork.RequestAction(MogulActions.SetActiveQuest, questId);
    }

    public static void RequestUntrack() =>
        MogulNetwork.RequestAction(MogulActions.SetActiveQuest, string.Empty);

    public static void RequestClaim(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        MogulNetwork.RequestAction(MogulActions.CompleteQuest, questId);
    }

    public static void RequestRecordEvent(MogulObjectiveEvent objectiveEvent, string targetId, int amount = 1)
    {
        if (amount <= 0) return;
        MogulNetwork.RequestAction(MogulActions.RecordObjectiveEvent, BuildEventKey(objectiveEvent, targetId) + ":" + amount);
    }

    public static string BuildEventKey(MogulObjectiveEvent objectiveEvent, string targetId) =>
        objectiveEvent + ":" + (targetId ?? "");

    public static int GetProgress(MogulSaveData data, string key)
    {
        if (data?.ObjectiveProgress == null || string.IsNullOrEmpty(key)) return 0;
        return data.ObjectiveProgress.TryGetValue(key, out var value) ? value : 0;
    }

    public static void AddProgress(MogulSaveData data, string key, int amount)
    {
        if (data?.ObjectiveProgress == null || string.IsNullOrEmpty(key) || amount <= 0) return;
        data.ObjectiveProgress.TryGetValue(key, out int existing);
        data.ObjectiveProgress[key] = existing + amount;

        foreach (var objective in All)
            if (!string.IsNullOrEmpty(objective.TargetId)
                && key == BuildEventKey(objective.Event, objective.TargetId))
                AddProgress(data, objective.Id, amount);
    }

    public static void Claim(MogulQuestDefinition objective, MogulSaveData data)
    {
        if (objective == null || data == null) return;
        data.CompletedQuestIds.Add(objective.Id);
        data.Reach += objective.ReachReward;
        objective.OnClaim?.Invoke(data);
    }

    private static MogulQuestDefinition Task(
        string id,
        string title,
        string description,
        string objective,
        int target,
        int reachReward,
        MogulObjectiveEvent objectiveEvent,
        string targetId)
    {
        return new MogulQuestDefinition
        {
            Id = id,
            Type = MogulObjectiveType.Task,
            Title = title,
            Description = description,
            Objective = objective,
            Target = target,
            ReachReward = reachReward,
            Event = objectiveEvent,
            TargetId = targetId,
            IsAvailable = data => HasClaimed(data, "westville_statement"),
        };
    }

    private static void Unlock(MogulSaveData data, string flag)
    {
        if (data?.UnlockedFeatureIds == null || string.IsNullOrEmpty(flag)) return;
        if (!data.UnlockedFeatureIds.Contains(flag))
            data.UnlockedFeatureIds.Add(flag);
    }

    private static int CountVirtualProduct(MogulSaveData data, string productId)
    {
        if (data?.LocationVirtualInventory == null || string.IsNullOrEmpty(productId)) return 0;
        int count = 0;
        foreach (var inventory in data.LocationVirtualInventory.Values)
            if (inventory != null && inventory.TryGetValue(productId, out int qty))
                count += qty;
        return count;
    }
}
