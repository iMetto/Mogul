using S1MAPI.Building.Structural;
using UnityEngine;

namespace Mogul.Data;

public class MogulLocation
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public float Price { get; }
    public Vector3 WorldPosition { get; }
    public WallSide Door { get; }
    public Vector3 RoomSize { get; }


    public MogulLocation(string id, string name, string description, float price, Vector3 worldPosition, WallSide door, Vector3 roomSize)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        WorldPosition = worldPosition;
        Door = door;
        RoomSize = roomSize;
    }
}
