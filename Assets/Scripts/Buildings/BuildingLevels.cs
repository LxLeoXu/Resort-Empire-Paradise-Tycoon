using UnityEngine;

/// <summary>
/// Shows the building's art for its level (BuildingInfo.currentLevel 1-3, SCRUM-198). Each level lists the objects it
/// shows (level 1: the building's own sprite child and shadow; levels 2 / 3: the Level2 / Level3 objects), its guest
/// places (GuestPlaces then hands out 3 / 5 / 7 seats), its click area and the island props it changes (a bush that
/// would stand on a new terrace). The art also switches in the Editor, so setting Current Level on BuildingInfo
/// previews it; the places, the click area and the island change only in Play mode, so a preview never edits the scene.
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

    [Tooltip("Úroveň 1, 2, 3.")]
    public Level[] levels = new Level[3];
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
        guestPlaces = GetComponentInChildren<GuestPlaces>(true);
        shown = 0;
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (info == null || levels == null || levels.Length == 0) return;
        int level = Mathf.Clamp(info.currentLevel, 1, levels.Length);
        if (level == shown) return;
        shown = level;
        for (int i = 0; i < levels.Length; i++)
            if (i != level - 1) Show(levels[i], false);
        Show(levels[level - 1], true);
        if (!Application.isPlaying) return;

        var current = levels[level - 1];
        if (current == null) return;
        if (guestPlaces != null && current.places.Length > 0) guestPlaces.SetPlaces(current.places);
        if (selection != null && current.selectionSize != Vector2.zero)
        {
            selection.transform.localPosition = current.selectionPosition;
            selection.size = current.selectionSize;
        }
        ChangeIsland(level);
    }

    private static void Show(Level level, bool on)
    {
        if (level == null) return;
        foreach (var go in level.show)
            if (go != null && go.activeSelf != on) go.SetActive(on);
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
