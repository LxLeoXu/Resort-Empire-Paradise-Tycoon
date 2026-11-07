using System;
using UnityEngine;

/// <summary>Authored walking routes along the paths painted on the island.</summary>
public sealed class IslandGuestPaths : MonoBehaviour
{
    [Serializable]
    public sealed class Route
    {
        public NeedType need;
        public Transform[] points = Array.Empty<Transform>();
    }

    [SerializeField] private Route[] routes = Array.Empty<Route>();

    public Vector3[] GetRoute(NeedType need)
    {
        foreach (var route in routes)
        {
            if (route.need != need) continue;
            var positions = new Vector3[route.points.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                if (route.points[i] == null)
                {
                    Debug.LogError($"IslandGuestPaths: missing point for {need}.", this);
                    return Array.Empty<Vector3>();
                }
                positions[i] = route.points[i].position;
            }
            return positions;
        }
        Debug.LogError($"IslandGuestPaths: no walking route for {need}.", this);
        return Array.Empty<Vector3>();
    }

    private void OnDrawGizmosSelected()
    {
        for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            var route = routes[routeIndex];
            Gizmos.color = Color.HSVToRGB(routeIndex / (float)Mathf.Max(1, routes.Length), 0.75f, 1f);
            Transform previous = null;
            foreach (var point in route.points)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.position, 0.045f);
                if (previous != null) Gizmos.DrawLine(previous.position, point.position);
                previous = point;
            }
        }
    }
}
