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
            new Vector3(-167.08f, -3.13f, 73.55f),
            WallSide.East, new Vector3(7.5f, 3f, 5.5f),
            deskOffset: new Vector3(1.05f, 0f, 1.25f),
            deskRotation: Quaternion.Euler(0f, 270f, 0f),
            sellDesk: new SellDeskConfig(
                registerLocalPos: new Vector3(-0.13f, 0.95f, 0.12f),
                registerLocalRotation: Quaternion.Euler(0f, 0f, 0f),
                staffLocalPos: new Vector3(0.80f, 0f, 0.45f),
                staffLocalRotation: Quaternion.Euler(0f, 5.6f, 0f)),
            growTent: new GrowTentConfig(
                localPos: new Vector3(3.94f, 0.25f, 5.00f),
                rotation: Quaternion.Euler(0f, 180.4f, 0f)),
            storageRacks:
            [
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(1.25f, 0.4f, 5.05f), Quaternion.Euler(0f, 0f, 0f)),
            ],
            maxInteriorSlots: 0
        ),
        new MogulLocation(
            "loc_downtown_01",
            "Downtown Spot",
            "A prime location in the heart of downtown.",
            15000f,
            new Vector3(105f, 1.15f, -3.29f),
            WallSide.North, new Vector3(7f, 3f, 9f),
            deskOffset: new Vector3(1.50f, 0f, 4.50f),
            deskRotation: Quaternion.Euler(0f, 0f, 0f),
            sellDesk: new SellDeskConfig(
                registerLocalPos: new Vector3(-0.13f, 0.95f, 0.12f),
                registerLocalRotation: Quaternion.Euler(0f, 0f, 0f),
                staffLocalPos: new Vector3(0.70f, 0f, 4.63f),
                staffLocalRotation: Quaternion.Euler(0f, 105f, 0f)),
            growTent: new GrowTentConfig(
                localPos: new Vector3(6.50f, 0.25f, 4.80f),
                rotation: Quaternion.Euler(0f, 271f, 0f)),
            storageRacks:
            [
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(3.48f, 0f, 0.51f), Quaternion.Euler(0f, 0f, 0f)),
                new StorageRackConfig(new PrefabRef("StorageRack_Large"), new Vector3(5.10f, 0f, 0.51f), Quaternion.Euler(0f, 0f, 0f)),
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
            sellDesk: new SellDeskConfig(
                registerLocalPos: new Vector3(-0.13f, 0.95f, 0.12f),
                registerLocalRotation: Quaternion.Euler(0f, 0f, 0f)),
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

    public static bool IsVisible(string id, MogulSaveData data = null)
    {
        data ??= MogulNetwork.Data;
        if (id == "loc_westville_01")
            return MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockPropertiesTab);
        if (id == "loc_downtown_01")
            return MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.RevealDowntown)
                || MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockDowntownPurchase);
        return MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockDowntownPurchase);
    }

    public static bool IsPurchasable(string id, MogulSaveData data = null)
    {
        data ??= MogulNetwork.Data;
        if (id == "loc_westville_01")
            return MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockWestvillePurchase);
        if (id == "loc_downtown_01")
            return MogulQuestSystem.IsUnlocked(data, MogulQuestSystem.UnlockDowntownPurchase);
        return false;
    }

    // Returns null on success, or an error string to display.
    // Uses a single atomic action so SpawnBuilding always sees the correct design.
    public static string TryPurchaseWithDesign(string locationId, string designId)
    {
        MogulLocation loc = Find(locationId);
        if (loc == null) return "Unknown location.";
        if (IsOwned(locationId)) return "Already owned.";
        if (!IsPurchasable(locationId)) return "Locked. Handle the work first.";
        if (Money.GetOnlineBalance() < loc.Price)
            return $"Not enough balance. Need ${loc.Price:N0}.";

        Money.CreateOnlineTransaction("Property Purchase", -loc.Price, 1, loc.Name);
        MogulNetwork.RequestAction(MogulActions.PurchaseWithDesign, $"{locationId}:{designId}");
        return null;
    }
}
