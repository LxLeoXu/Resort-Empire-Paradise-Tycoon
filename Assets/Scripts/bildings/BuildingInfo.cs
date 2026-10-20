
using UnityEngine;

public class BuildingInfo : MonoBehaviour
{
    [Header("Building Information")]
    public string buildingName;
    public int buildCost;

    [Header("Building Status")]
    public bool isBuilt = true;

    private void Start()
    {
        Debug.Log(
            buildingName + " is ready. Cost: " + buildCost
        );
    }
}
