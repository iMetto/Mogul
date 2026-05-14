using System.Collections.Generic;
using System.Linq;
using Mogul.Data;
using S1API.Money;
using S1MAPI.Building.Structural;
using S1MAPI.Core;
using S1MAPI.S1;
using UnityEngine;

namespace Mogul.Systems;

public static class PropertySystem
{
    // All locations available for purchase in the Mogul app.
    // Coordinates and descriptions will be filled in once we explore the map.
    public static readonly IReadOnlyList<MogulLocation> Catalog = new List<MogulLocation>
    {
        new MogulLocation(
            "loc_westville_01",
            "Westville Corner",
            "Quiet corner in Westville — low profile.",
            8000f,
            new Vector3(-171.44f, -3.0f, 70f),
            WallSide.East, new Vector3(12f, 3f, 8f),
            deskOffset: new Vector3(10f, 0f, 6.5f),
            deskRotation: Quaternion.Euler(0f, 90f, 0f),
            sellDesk: new SellDeskConfig(
                registerLocalPos: new Vector3(-0.13f, 0.95f, 0.12f),
                registerLocalRotation: Quaternion.Euler(0f, 0f, 0f)),
            storageRacks:
            [
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(1.5f, 0f, 3f)),
            ],
            maxInteriorSlots: 0
        ),
        new MogulLocation(
            "loc_downtown_01",
            "Downtown Spot",
            "A prime location in the heart of downtown.",
            15000f,
            new Vector3(109f, 1.0f, -6.29f),
            WallSide.North, new Vector3(8f, 3f, 12f),
            deskOffset: new Vector3(1.5f, 0f, 10.5f),
            deskRotation: Quaternion.Euler(0f, 90f, 0f),
            storageRacks:
            [
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(4f, 0f, 1.5f)),
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(6f, 0f, 1.5f)),
            ],
            maxInteriorSlots: 0
        ),
        new MogulLocation(
            "loc_hills_01",
            "Hills Warehouse",
            "Isolated industrial space in the hills.",
            10000f,
            new Vector3(71.93f, 5.0f, -76f),
            WallSide.East, new Vector3(14f, 4f, 16f),
            deskOffset: new Vector3(12.5f, 0f, 14.5f),
            deskRotation: Quaternion.Euler(0f, 90f, 0f),
            storageRacks:
            [
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(1.5f, 0f, 4f)),
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(1.5f, 0f, 8f)),
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(1.5f, 0f, 12f)),
            ],
            maxInteriorSlots: 0
        ),
    };

    public static MogulLocation Find(string id) =>
        Catalog.FirstOrDefault(l => l.Id == id);

    public static bool IsOwned(string id) =>
        MogulNetwork.Data.RegisteredLocationIds.Contains(id);

    // Returns null on success, or an error string to display.
    // Uses a single atomic action so SpawnBuilding always sees the correct design.
    public static string TryPurchaseWithDesign(string locationId, string designId)
    {
        MogulLocation loc = Find(locationId);
        if (loc == null) return "Unknown location.";
        if (IsOwned(locationId)) return "Already owned.";
        if (Money.GetOnlineBalance() < loc.Price)
            return $"Not enough balance. Need ${loc.Price:N0}.";

        Money.CreateOnlineTransaction("Property Purchase", -loc.Price, 1, loc.Name);
        MogulNetwork.RequestAction(MogulActions.PurchaseWithDesign, $"{locationId}:{designId}");
        return null;
    }
}
