using System.Collections.Generic;

namespace Mogul.Data;

public class MogulSaveData
{
    public int Reach { get; set; } = 0;
    public List<string> RegisteredLocationIds { get; set; } = new List<string>();
    public Dictionary<string, string> LocationDesigns { get; set; } = new Dictionary<string, string>();
    public string ActiveQuestId { get; set; } = null;
    public int ActiveQuestProgress { get; set; } = 0;
    public List<string> CompletedQuestIds { get; set; } = new List<string>();
    public Dictionary<string, int> ObjectiveProgress { get; set; } = new Dictionary<string, int>();
    public List<string> UnlockedFeatureIds { get; set; } = new List<string>();
    public Dictionary<string, float> RegisterBalances { get; set; } = new Dictionary<string, float>();
    public Dictionary<string, List<HiredEmployeeData>> LocationEmployees { get; set; } = new Dictionary<string, List<HiredEmployeeData>>();
    public Dictionary<string, Dictionary<string, int>> LocationVirtualInventory { get; set; } = new Dictionary<string, Dictionary<string, int>>();
    public Dictionary<string, int> LocationBudtenderProductionDay { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, BudtenderOrderData> LocationBudtenderOrders { get; set; } = new Dictionary<string, BudtenderOrderData>();
    public Dictionary<string, Dictionary<string, MogulObjectPlacementData>> LocationObjectPlacements { get; set; } = new Dictionary<string, Dictionary<string, MogulObjectPlacementData>>();
}

public class BudtenderOrderData
{
    public string ProductId { get; set; } = "";
    public string BaseProductId { get; set; } = "";
    public List<string> IngredientIds { get; set; } = new List<string>();
    public string DisplayName { get; set; } = "";
    public int Quantity { get; set; }
    public int StartDay { get; set; }
    public int StartTime { get; set; }
    public int ReadyDay { get; set; }
}

public class MogulObjectPlacementData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
}
