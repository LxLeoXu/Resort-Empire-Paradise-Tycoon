using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the building's art for its level (BuildingInfo.currentLevel 1-5). Each level lists the objects it
/// shows, its guest places (GuestPlaces hands out 3 / 5 / 7 / 7 / 10 seats), its click area and the island props
/// it changes. If level 4 or 5 art is not assigned yet, it safely falls back to the highest configured level (level 3).
/// </summary>
[ExecuteAlways]
public class BuildingLevels : MonoBehaviour
{
    [System.Serializable]
    public class Level
    {
        [Tooltip("Objekty, ktoré táto úroveň zobrazuje (objekty ostatných úrovní sa skryjú).")]
        public GameObject[] show = new GameObject[0];
        [Tooltip("Miesta pre hostí tejto úrovne v poradí, v akom ich hostia obsadzujú.")]
        public Transform[] places = new Transform[0];
        [Tooltip("Plocha na kliknutie tejto úrovne: lokálna pozícia SelectionArea a veľkosť jej BoxCollider2D.")]
        public Vector3 selectionPosition;
        public Vector2 selectionSize;
    }

    [System.Serializable]
    public class IslandChange
    {
        [Tooltip("Cesta k objektu ostrova v scéne, napr. Island/Props/Bush_05.")]
        public string path;
        [Tooltip("Od tejto úrovne platí zmena.")]
        public int fromLevel = 2;
        [Tooltip("Skryť objekt (inak sa posunie o Offset).")]
        public bool hide;
        public Vector2 offset;
    }

    [Tooltip("Úrovne 1 až 5 (ak 4 a 5 zatiaľ nemajú obrázky, použije sa obrázok úrovne 3).")]
    public Level[] levels = new Level[5];
    public BoxCollider2D selection;
    public IslandChange[] islandChanges = new IslandChange[0];

    private BuildingInfo info;
    private GuestPlaces guestPlaces;
    private int shown;
    private Transform[] islandTargets;
    private Vector3[] islandRest;

    private void OnEnable()
    {
        info = GetComponent<BuildingInfo>();
        if (info == null) info = GetComponentInParent<BuildingInfo>();
        if (info == null) info = GetComponentInChildren<BuildingInfo>(true);
        guestPlaces = GetComponentInChildren<GuestPlaces>(true);
        shown = 0;
        Refresh();
    }

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        shown = 0;
        Refresh();
    }

    private void Update() => Refresh();
    public void RefreshAfterLoad() { shown = 0; Refresh(); }

    private void Refresh()
    {
        if (info == null || levels == null || levels.Length == 0) return;

        int maxLevel = Mathf.Min(BuildingInfo.MaxLevel, info.EffectiveMaxLevel);
        int level = Mathf.Clamp(info.currentLevel, 1, maxLevel);
        if (level == shown) return;
        shown = level;

        int targetIndex = level - 1;
        int visualIndex = FindVisualLevelIndex(targetIndex);
        if (visualIndex >= 0)
        {
            var activeShow = new HashSet<GameObject>();
            if (levels[visualIndex].show != null)
            {
                foreach (var go in levels[visualIndex].show)
                    if (go != null) activeShow.Add(go);
            }

            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null || levels[i].show == null) continue;
                foreach (var go in levels[i].show)
                    if (go != null && !activeShow.Contains(go) && go.activeSelf)
                        go.SetActive(false);
            }

            foreach (var go in activeShow)
                if (!go.activeSelf) go.SetActive(true);
        }

        if (!Application.isPlaying) return;

        int placesIndex = FindPlacesLevelIndex(targetIndex);
        if (guestPlaces != null && placesIndex >= 0)
            guestPlaces.SetPlaces(levels[placesIndex].places);

        int selectionIndex = FindSelectionLevelIndex(targetIndex);
        if (selection != null && selectionIndex >= 0)
        {
            var sel = levels[selectionIndex];
            selection.transform.localPosition = sel.selectionPosition;
            selection.size = sel.selectionSize;
        }

        ChangeIsland(level);
    }

    private int FindVisualLevelIndex(int targetIndex)
    {
        int start = Mathf.Clamp(targetIndex, 0, levels.Length - 1);
        for (int i = start; i >= 0; i--)
        {
            var lvl = levels[i];
            if (lvl == null || lvl.show == null) continue;
            foreach (var go in lvl.show)
                if (go != null) return i;
        }
        return -1;
    }

    private int FindPlacesLevelIndex(int targetIndex)
    {
        int start = Mathf.Clamp(targetIndex, 0, levels.Length - 1);
        for (int i = start; i >= 0; i--)
        {
            var lvl = levels[i];
            if (lvl == null || lvl.places == null || lvl.places.Length == 0) continue;
            foreach (var t in lvl.places)
                if (t != null) return i;
        }
        return -1;
    }

    private int FindSelectionLevelIndex(int targetIndex)
    {
        int start = Mathf.Clamp(targetIndex, 0, levels.Length - 1);
        for (int i = start; i >= 0; i--)
        {
            var lvl = levels[i];
            if (lvl != null && lvl.selectionSize != Vector2.zero) return i;
        }
        return -1;
    }

    private void ChangeIsland(int level)
    {
        if (islandTargets == null)
        {
            islandTargets = new Transform[islandChanges.Length];
            islandRest = new Vector3[islandChanges.Length];
            for (int i = 0; i < islandChanges.Length; i++)
            {
                var go = GameObject.Find(islandChanges[i].path);
                if (go == null) continue;
                islandTargets[i] = go.transform;
                islandRest[i] = go.transform.position;
            }
        }
        for (int i = 0; i < islandChanges.Length; i++)
        {
            var target = islandTargets[i];
            if (target == null) continue;
            var change = islandChanges[i];
            bool applies = level >= change.fromLevel;
            if (change.hide)
                target.gameObject.SetActive(!applies);
            else
                target.position = islandRest[i] + (applies ? (Vector3)change.offset : Vector3.zero);
        }
    }
}