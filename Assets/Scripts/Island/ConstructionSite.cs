using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ConstructionSite : MonoBehaviour
{
    [SerializeField] private BuildingSpot spot;
    [SerializeField] private float findRadius = 1f;
    [SerializeField] private GameObject[] art;
    [SerializeField] private Collider2D[] tapAreas;

    [Header("Build \"+\" when the spot is available")]
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite haloSprite;
    [SerializeField] private Sprite flashSprite;
    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private Sprite chipSprite;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private TMP_FontAsset priceFont;

    [Header("Celebration after the purchase in S9")]
    [SerializeField] private UnlockCelebration.Style celebration;

    private BuildReadyBadge badge;
    private Camera mainCamera;
    private bool wasOccupied;
    private float s9SeenOpen = -10f;

    private void Start()
    {
        if (spot == null)
        {
            float best = findRadius;
            foreach (var s in FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(s.transform.position, transform.position);
                if (d < best)
                {
                    best = d;
                    spot = s;
                }
            }
        }
        if (spot != null && readySprite != null)
        {
            var plus = spot.GetComponentInChildren<FollowWorldPoint>(true);
            var icon = plus != null ? plus.transform.Find("Icon") : null;
            if (icon != null && icon.TryGetComponent(out Image image))
            {
                badge = plus.gameObject.AddComponent<BuildReadyBadge>();
                badge.Init(image, readySprite, haloSprite, flashSprite, sparkSprite, chipSprite, coinSprite, priceFont,
                           spot.UnlockCost);
            }
        }
        wasOccupied = spot != null && spot.isOccupied;
        Refresh();
    }

    private void Update()
    {
        WatchPurchase();
        Refresh();
        if (SpotOpen && WasTapped())
            spot.OpenS9();
    }

    private void WatchPurchase()
    {
        if (spot == null) return;
        if (spot.S9Window != null && spot.S9Window.activeInHierarchy)
            s9SeenOpen = Time.unscaledTime;
        bool occupied = spot.isOccupied;
        // only a purchase made in S9 (not a building restored from the save)
        if (occupied && !wasOccupied && !GamePersistence.IsRestoring && Time.unscaledTime - s9SeenOpen < 1f &&
            celebration != null && celebration.ribbon != null)
            UnlockCelebration.Play(celebration, spot.AreaTitle, spot.SpawnedBuilding, spot.ResortValueGain);
        wasOccupied = occupied;
    }

    private bool SpotOpen => spot != null && !spot.isOccupied && spot.gameObject.activeInHierarchy;

    private void Refresh()
    {
        bool show = spot == null || !spot.isOccupied;
        foreach (var a in art)
            if (a != null && a.activeSelf != show)
                a.SetActive(show);
        if (badge != null)
            badge.SetReady(SpotOpen && spot.IsAvailable);
    }

    private bool WasTapped()
    {
        Vector2 screenPos;
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return false;
        screenPos = Pointer.current.position.ReadValue();
#else
        if (!Input.GetMouseButtonDown(0)) return false;
        screenPos = Input.mousePosition;
#endif
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;
        Vector2 world = mainCamera.ScreenToWorldPoint(screenPos);
        if (Physics2D.Raycast(world, Vector2.zero).collider != null) return false;
        foreach (var area in tapAreas)
            if (area != null && area.isActiveAndEnabled && area.OverlapPoint(world))
                return true;
        return false;
    }
}
