using UnityEngine;

public partial class BuildingSpot
{
    public bool IsAvailable => !isOccupied && CanBuyNow();
    public int UnlockCost => unlockCost;
    public int RequiredWave => requiredWave;
    public string AreaTitle => areaTitle;
    public Sprite AreaPreview => areaPreview;
    public GameObject S9Window => s9Window;
    public int ResortValueGain => resortValueGain;
}
