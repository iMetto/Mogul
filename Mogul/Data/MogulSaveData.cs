using System.Collections.Generic;

namespace Mogul.Data;

public class MogulSaveData
{
    public int Reach { get; set; } = 0;
    public List<string> RegisteredLocationIds { get; set; } = new List<string>();
    public Dictionary<string, string> LocationDesigns { get; set; } = new Dictionary<string, string>();
    public string ActiveQuestId { get; set; } = null;
    public int ActiveQuestProgress { get; set; } = 0;
    public Dictionary<string, float> RegisterBalances { get; set; } = new Dictionary<string, float>();
}
