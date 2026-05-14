using System;
using System.Collections.Generic;
using Mogul.Data;

namespace Mogul.Systems;

public class MogulQuestDefinition
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Objective { get; init; } = "";
    public int Target { get; init; } = 1;
    public int ReachReward { get; init; }
    public Func<MogulSaveData, int> Progress { get; init; } = _ => 0;
}

public static class MogulQuestSystem
{
    public static readonly IReadOnlyList<MogulQuestDefinition> Quests = new List<MogulQuestDefinition>
    {
        new()
        {
            Id = "first_property",
            Title = "Open A Front",
            Description = "Buy your first Mogul property and get the operation out of your pockets.",
            Objective = "Own 1 property",
            Target = 1,
            ReachReward = 250,
            Progress = data => data?.RegisteredLocationIds?.Count ?? 0,
        },
        new()
        {
            Id = "hire_cashier",
            Title = "Leave The Register",
            Description = "Hire a cashier so customers can be served without you standing behind the counter.",
            Objective = "Hire 1 cashier",
            Target = 1,
            ReachReward = 150,
            Progress = data => CountEmployees(data, EmployeeRole.Cashier),
        },
        new()
        {
            Id = "hire_budtender",
            Title = "Start The Back Room",
            Description = "Hire a budtender and start the OG Kush test grow.",
            Objective = "Hire 1 budtender",
            Target = 1,
            ReachReward = 200,
            Progress = data => CountEmployees(data, EmployeeRole.Budtender),
        },
        new()
        {
            Id = "first_test_crop",
            Title = "First Test Crop",
            Description = "Let the budtender produce a full day of virtual OG Kush stock.",
            Objective = "Produce 20 OG Kush",
            Target = EmployeeProduction.BudtenderOgKushPerDay,
            ReachReward = 300,
            Progress = CountVirtualOgKush,
        },
    };

    public static MogulQuestDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var quest in Quests)
            if (quest.Id == id)
                return quest;
        return null;
    }

    public static int GetProgress(MogulQuestDefinition quest, MogulSaveData data)
    {
        if (quest == null || data == null) return 0;
        return Math.Max(0, quest.Progress(data));
    }

    public static bool IsComplete(MogulQuestDefinition quest, MogulSaveData data) =>
        quest != null && GetProgress(quest, data) >= quest.Target;

    public static bool IsClaimed(MogulQuestDefinition quest, MogulSaveData data) =>
        quest != null && data?.CompletedQuestIds != null && data.CompletedQuestIds.Contains(quest.Id);

    public static void RequestTrack(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        MogulNetwork.RequestAction(MogulActions.SetActiveQuest, questId);
    }

    public static void RequestClaim(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        MogulNetwork.RequestAction(MogulActions.CompleteQuest, questId);
    }

    private static int CountEmployees(MogulSaveData data, EmployeeRole role)
    {
        if (data?.LocationEmployees == null) return 0;

        int count = 0;
        foreach (var employees in data.LocationEmployees.Values)
        {
            if (employees == null) continue;
            foreach (var employee in employees)
                if (employee != null && employee.Role == role)
                    count++;
        }
        return count;
    }

    private static int CountVirtualOgKush(MogulSaveData data)
    {
        if (data?.LocationVirtualInventory == null) return 0;

        int count = 0;
        foreach (var inventory in data.LocationVirtualInventory.Values)
            if (inventory != null && inventory.TryGetValue(EmployeeProduction.TestBudtenderProductId, out int qty))
                count += qty;
        return count;
    }
}
