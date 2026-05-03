using System.Text;
using SteamNetworkLib.Models;
using Newtonsoft.Json;

namespace Mogul.Systems;

// String constants for all actions clients can request from the host.
public static class MogulActions
{
    public const string RegisterLocation = "register_location";
    public const string UnregisterLocation = "unregister_location";
    public const string AddReach = "add_reach";
    public const string SetLocationDesign = "set_location_design";
    public const string PurchaseWithDesign = "purchase_with_design";
    public const string SetActiveQuest = "set_quest";
    public const string AdvanceQuest = "advance_quest";
}

// P2P message sent from client → host to request a state change.
public class MogulActionMessage : P2PMessage
{
    public override string MessageType => "MOGUL_ACTION";

    public string Action { get; set; } = "";
    public string Payload { get; set; } = "";

    public override byte[] Serialize()
    {
        string json = "{" + CreateJsonBase($"\"Action\":\"{Action}\",\"Payload\":\"{Payload}\"") + "}";
        return Encoding.UTF8.GetBytes(json);
    }

    public override void Deserialize(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        ParseJsonBase(json);
        Action = ExtractJsonValue(json, "Action");
        Payload = ExtractJsonValue(json, "Payload");
    }
}

public class MogulSyncMessage : P2PMessage
{
    public override string MessageType => "MOGUL_SYNC";
    public string Json { get; set; } = "";

    public override byte[] Serialize()
    {
        string json = "{" + CreateJsonBase($"\"Json\":{JsonConvert.SerializeObject(Json)}") + "}";
        return Encoding.UTF8.GetBytes(json);
    }

    public override void Deserialize(byte[] data)
    {
        string json = Encoding.UTF8.GetString(data);
        ParseJsonBase(json);
        Json = ExtractJsonValue(json, "Json");
    }
}
