public partial class BuildingSpot
{
    public bool IsAvailable => !isOccupied && CanBuyNow();
    public int UnlockCost => unlockCost;
}
