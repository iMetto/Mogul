using System.Collections.Generic;
using System.Linq;
using Mogul.Data;
using S1API.Money;
using S1MAPI.Building.Structural;
using UnityEngine;

namespace Mogul.Systems;

public static class PropertySystem
{
    // All locations available for purchase in the Mogul app.
    // Coordinates and descriptions will be filled in once we explore the map.
    public static readonly IReadOnlyList<MogulLocation> Catalog = new List<MogulLocation>
    {
        new MogulLocation
        (
            "loc_westville_01",
         "Westville Corner",
          "Quiet corner in Westville — low profile.",
             8000f,
               new Vector3(-165f, -3.0f,  74f),
                 WallSide.East, new Vector3(5f, 3f, 4f)
                 ),
        new MogulLocation
        (
            "loc_downtown_01",
          "Downtown Spot",
              "A prime location in the heart of downtown.",
               15000f,
               new Vector3(115f,   1.0f,  -1f),
                WallSide.North, new Vector3(8f, 3f, 6f)
                ),
        new MogulLocation
        (
            "loc_hills_01",
             "Hills Warehouse",
               "Isolated industrial space in the hills.",
                  10000f, new Vector3(74f, 5.0f, -67f),
                    WallSide.East, new Vector3(12f, 4f, 10f)
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
        if (loc == null)         return "Unknown location.";
        if (IsOwned(locationId)) return "Already owned.";
        if (Money.GetCashBalance() < loc.Price)
            return $"Not enough cash. Need ${loc.Price:N0}.";

        Money.ChangeCashBalance(-loc.Price, visualizeChange: true, playCashSound: true);
        MogulNetwork.RequestAction(MogulActions.PurchaseWithDesign, $"{locationId}:{designId}");
        return null;
    }

    public static string TryPurchase(string locationId)
    {
        MogulLocation loc = Find(locationId);
        if (loc == null)
            return "Unknown location.";

        if (IsOwned(locationId))
            return "Already owned.";

        if (Money.GetCashBalance() < loc.Price)
            return $"Not enough cash. Need ${loc.Price:N0}.";

        Money.ChangeCashBalance(-loc.Price, visualizeChange: true, playCashSound: true);
        MogulNetwork.RequestAction(MogulActions.RegisterLocation, locationId);
        return null;
    }
}
