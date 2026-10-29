using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the building's guest places are (SCRUM-192: every building shows 3 of them in its sprite).
/// Each place is an empty child Place_1, Place_2, ... sitting on the seat drawn in the sprite, listed in the
/// order guests should take them. This only marks the places; seating guests, occupancy and the queue in front
/// of the building belong to the guest logic (SCRUM-244).
/// </summary>
public class GuestPlaces : MonoBehaviour
{
    [Tooltip("Miesta pre hostí v poradí, v akom ich hostia obsadzujú (Place_1, Place_2, ...).")]
    [SerializeField] private List<Transform> places = new List<Transform>();

    public int Count => places.Count;
    public IReadOnlyList<Transform> Places => places;

    public Vector3 GetPosition(int index) => places[index].position;

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
