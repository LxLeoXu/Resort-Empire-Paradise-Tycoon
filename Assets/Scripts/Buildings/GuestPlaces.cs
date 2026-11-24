using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the building's guest places are.
/// Each place is an empty child Place_1, Place_2, ... sitting on the seat drawn in the sprite, listed in the
/// order guests should take them.
/// </summary>
public class GuestPlaces : MonoBehaviour
{
    [Tooltip("Miesta pre hostí v poradí, v akom ich hostia obsadzujú (Place_1, Place_2, ...).")]
    [SerializeField] private List<Transform> places = new List<Transform>();

    public int Count => places.Count;
    public IReadOnlyList<Transform> Places => places;

    public Vector3 GetPosition(int index)
    {
        if (places.Count == 0) return transform.position;
        if (index >= 0 && index < places.Count && places[index] != null)
            return places[index].position;

        // Bezpečný fallback pre Lv5 (10 miest), kým dočasná grafika Lv3 má len 7 transformov.
        int validIndex = ((index % places.Count) + places.Count) % places.Count;
        Vector3 basePos = places[validIndex] != null ? places[validIndex].position : transform.position;
        int overflow = index / places.Count;
        return basePos + new Vector3(0.08f * overflow, -0.04f * overflow, 0f);
    }

    /// <summary>Hands out other places: BuildingLevels gives the places of the building's current level.</summary>
    public void SetPlaces(Transform[] newPlaces)
    {
        places.Clear();
        if (newPlaces == null) return;
        foreach (var place in newPlaces)
            if (place != null) places.Add(place);
    }

    /// <summary>The need the building serves (BuildingInfo on the building's root).</summary>
    public NeedType Need
    {
        get
        {
            var info = GetComponentInParent<BuildingInfo>();
            return info != null ? info.offeredNeed : NeedType.None;
        }
    }

    private void Reset() => CollectChildren();

    private void OnValidate()
    {
        if (places.Count == 0 || places.Contains(null))
            CollectChildren();
    }

    [ContextMenu("Načítať miesta z detí (Place_1..n)")]
    private void CollectChildren()
    {
        places.Clear();
        foreach (Transform child in transform)
            places.Add(child);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f);
        for (int i = 0; i < places.Count; i++)
        {
            if (places[i] == null) continue;
            Gizmos.DrawWireSphere(places[i].position, 0.08f);
            UnityEditor.Handles.Label(places[i].position + Vector3.up * 0.15f, (i + 1).ToString());
        }
    }
#endif
}