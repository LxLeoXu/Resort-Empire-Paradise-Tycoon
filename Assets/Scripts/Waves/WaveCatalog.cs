using System;
using UnityEngine;

[Serializable]
public sealed class WaveDefinition
{
    [Min(1)] public int guests;
    [Min(0)] public int rooms;
    [Min(0)] public int drinks;
    [Min(0)] public int food;
    [Min(0)] public int relax;
    [Min(0)] public int oceanView;

    public WaveDefinition(int guests, int rooms, int drinks, int food, int relax, int oceanView)
    {
        this.guests = guests;
        this.rooms = rooms;
        this.drinks = drinks;
        this.food = food;
        this.relax = relax;
        this.oceanView = oceanView;
    }

    public int GetCount(NeedType type) => type switch
    {
        NeedType.Rooms => rooms,
        NeedType.Drinks => drinks,
        NeedType.Food => food,
        NeedType.Relax => relax,
        NeedType.OceanView => oceanView,
        _ => 0
    };

    public int TotalNeeds => rooms + drinks + food + relax + oceanView;
}

[CreateAssetMenu(menuName = "Paradise Tycoon/Wave Catalog")]
public sealed class WaveCatalog : ScriptableObject
{
    [SerializeField] private WaveDefinition[] waves =
    {
        new(12, 8, 8, 6, 0, 0),
        new(18, 12, 16, 10, 0, 0),
        new(24, 16, 18, 14, 0, 0),
        new(26, 16, 18, 14, 8, 0),
        new(34, 20, 22, 18, 16, 0),
        new(40, 25, 28, 22, 20, 0),
        new(42, 25, 28, 22, 20, 10),
        new(52, 31, 35, 28, 25, 16)
    };

    public int Count => waves.Length;
    public WaveDefinition GetWave(int index) => waves[index];
}
