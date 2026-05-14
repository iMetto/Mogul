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
    public Dictionary<string, float> RegisterBalances { get; set; } = new Dictionary<string, float>();
    public Dictionary<string, List<HiredEmployeeData>> LocationEmployees { get; set; } = new Dictionary<string, List<HiredEmployeeData>>();
    public Dictionary<string, Dictionary<string, int>> LocationVirtualInventory { get; set; } = new Dictionary<string, Dictionary<string, int>>();
    public Dictionary<string, int> LocationBudtenderProductionDay { get; set; } = new Dictionary<string, int>();
}
