using UnityEngine;

/// <summary>
/// Drives the island's shader animation (wind, water) only while the game runs: it feeds the
/// global _IslandTime and switches _IslandAnim on. In the Editor outside Play mode both stay 0,
/// so the island shows the still, pre-rendered picture.
/// </summary>
public class IslandAnimationClock : MonoBehaviour
{
    static readonly int TimeId = Shader.PropertyToID("_IslandTime");
    static readonly int AnimId = Shader.PropertyToID("_IslandAnim");

    void OnEnable()
    {
        Shader.SetGlobalFloat(AnimId, 1f);
    }

    void Update()
    {
        Shader.SetGlobalFloat(TimeId, Time.timeSinceLevelLoad);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(AnimId, 0f);
        Shader.SetGlobalFloat(TimeId, 0f);
    }
}
