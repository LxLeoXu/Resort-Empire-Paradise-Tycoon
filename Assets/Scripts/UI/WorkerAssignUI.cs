using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorkerAssignUI : MonoBehaviour
{
    [Header("Referencie kariet")]
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private WorkerCardUI cardTemplate;
    [SerializeField] private bool closeAfterMove = false;

    [Header("Animácia Unroll (Okno)")]
    [Tooltip("Sem môžeš pretiahnuť objekt Window (inak sa nájde sám).")]
    [SerializeField] private RectTransform windowRect;
    [Tooltip("Sem môžeš pretiahnuť objekt Overlay (inak sa nájde sám).")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private float animDuration = 0.26f;

    private readonly List<WorkerCardUI> cardPool = new();
    private BuildingService selectedSourceBuilding;
    private WorkerRoster roster;
    private Canvas rootCanvas;
    private RectTransform canvasRect;

    // Plávajúci worker pod prstom pri ťahaní
    private GameObject dragAvatar;
    private RectTransform dragAvatarRect;
    private Image dragAvatarImage;

    private Vector2 openedWindowPos;
    private Vector2 ClosedWindowPos => new Vector2(openedWindowPos.x, openedWindowPos.y - (windowRect != null ? windowRect.rect.height : 600f) - 300f);
    private Coroutine animRoutine;
    private bool initialized;

    public BuildingService SelectedSource => selectedSourceBuilding;

    private void Awake()
    {
        AutoWire();
        CreateDragAvatar();
    }

    private void AutoWire()
    {
        if (initialized) return;
        initialized = true;

        if (windowRect == null)
            windowRect = transform.Find("Window") as RectTransform;

        if (windowRect != null)
            openedWindowPos = windowRect.anchoredPosition;

        if (overlayGroup == null)
        {
            Transform ov = transform.Find("Overlay");
            if (ov != null)
            {
                overlayGroup = ov.GetComponent<CanvasGroup>();
                if (overlayGroup == null) overlayGroup = ov.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (cardsContainer == null)
            cardsContainer = transform.Find("Window/BuildingCards");

        if (cardTemplate == null && cardsContainer != null)
            cardTemplate = cardsContainer.GetComponentInChildren<WorkerCardUI>(true);

        if (cardTemplate != null && !cardPool.Contains(cardTemplate))
            cardPool.Add(cardTemplate);

        if (cardsContainer == null && cardTemplate != null)
            cardsContainer = cardTemplate.transform.parent;

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            canvasRect = rootCanvas.GetComponent<RectTransform>();
    }

    private void CreateDragAvatar()
    {
        if (dragAvatar != null) return;

        dragAvatar = new GameObject("S3_DragWorkerAvatar", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        dragAvatar.transform.SetParent(transform, false);

        dragAvatarRect = dragAvatar.GetComponent<RectTransform>();
        dragAvatarRect.sizeDelta = new Vector2(46f, 46f);

        dragAvatarImage = dragAvatar.GetComponent<Image>();
        dragAvatarImage.raycastTarget = false;

        var group = dragAvatar.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        dragAvatar.SetActive(false);
    }

    private void OnEnable()
    {
        AutoWire();
        selectedSourceBuilding = null;
        roster = WorkerRoster.Instance;
        if (roster != null) roster.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (roster != null) roster.Changed -= Refresh;
        selectedSourceBuilding = null;
        if (dragAvatar != null) dragAvatar.SetActive(false);
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }
    }

    public void Open(BuildingInfo buildingInfo) => DoOpen();
    public void OpenForNeed(NeedType need) => DoOpen();

    private void DoOpen()
    {
        AutoWire();

        // 1. DÔLEŽITÉ: Presunie S3 na vrch hierarchie v Canvas, aby bolo NAD S2!
        transform.SetAsLastSibling();

        selectedSourceBuilding = null;
        gameObject.SetActive(true);
        Refresh();

        // 2. Spustenie animácie vysunutia / unroll
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateWindow(true));
    }

    public void Close()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateWindow(false));
    }

    private IEnumerator AnimateWindow(bool opening)
    {
        if (windowRect == null)
        {
            if (!opening) gameObject.SetActive(false);
            yield break;
        }

        Vector2 startPos = opening ? ClosedWindowPos : windowRect.anchoredPosition;
        Vector2 targetPos = opening ? openedWindowPos : ClosedWindowPos;

        float startAlpha = overlayGroup != null ? overlayGroup.alpha : 1f;
        float targetAlpha = opening ? 1f : 0f;

        windowRect.anchoredPosition = startPos;
        if (overlayGroup != null) overlayGroup.alpha = startAlpha;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);

            // EaseOutCubic pre plynulý dojazd
            float ease = opening ? (1f - Mathf.Pow(1f - t, 3f)) : (t * t);

            windowRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, ease);

            if (overlayGroup != null)
                overlayGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        windowRect.anchoredPosition = targetPos;
        if (overlayGroup != null) overlayGroup.alpha = targetAlpha;

        if (!opening)
            gameObject.SetActive(false);

        animRoutine = null;
    }

    public void Refresh()
    {
        if (roster == null) return;
        AutoWire();

        var buildings = WorkerRoster.GetOrderedBuildings(FindObjectsByType<BuildingService>(FindObjectsSortMode.None));

        while (cardPool.Count < buildings.Count && cardTemplate != null && cardsContainer != null)
            cardPool.Add(Instantiate(cardTemplate, cardsContainer));

        for (int i = 0; i < cardPool.Count; i++)
        {
            if (cardPool[i] == null) continue;
            bool active = i < buildings.Count;
            cardPool[i].gameObject.SetActive(active);
            if (active)
                cardPool[i].Bind(buildings[i], selectedSourceBuilding, roster, this);
        }
    }

    // =====================================================
    // 1. OVLÁDANIE ŤUKNUTÍM (TAP-TAP)
    // =====================================================
    public void HandleCardClicked(BuildingService clicked)
    {
        if (clicked == null || roster == null) return;

        int workersInClicked = roster.CountIn(clicked);
        int maxSlots = clicked.MaxWorkers;

        if (selectedSourceBuilding == null)
        {
            if (workersInClicked > 0)
            {
                selectedSourceBuilding = clicked;
                Refresh();
            }
            return;
        }

        if (clicked == selectedSourceBuilding)
        {
            selectedSourceBuilding = null;
            Refresh();
            return;
        }

        if (workersInClicked < maxSlots)
        {
            bool moved = roster.MoveOneWorker(selectedSourceBuilding, clicked);
            selectedSourceBuilding = null;
            Refresh();

            if (moved && closeAfterMove)
                Close();
            return;
        }

        if (workersInClicked > 0)
        {
            selectedSourceBuilding = clicked;
            Refresh();
        }
    }

    // =====================================================
    // 2. OVLÁDANIE PRETIAHNUTÍM (DRAG & DROP)
    // =====================================================
    public void StartDraggingWorker(BuildingService source, Sprite workerSprite, PointerEventData eventData)
    {
        if (source == null || roster == null || roster.CountIn(source) <= 0) return;

        selectedSourceBuilding = source;
        Refresh();

        if (dragAvatar != null)
        {
            dragAvatar.SetActive(true);
            dragAvatar.transform.SetAsLastSibling();

            if (dragAvatarImage != null && workerSprite != null)
                dragAvatarImage.sprite = workerSprite;

            UpdateDragPosition(eventData);
        }
    }

    public void UpdateDragPosition(PointerEventData eventData)
    {
        if (dragAvatar == null || !dragAvatar.activeSelf) return;

        if (canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);
            dragAvatarRect.anchoredPosition = localPoint;
        }
        else
        {
            dragAvatar.transform.position = eventData.position;
        }
    }

    public void EndDraggingWorker(BuildingService source, PointerEventData eventData)
    {
        if (dragAvatar != null)
            dragAvatar.SetActive(false);

        var raycastResults = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(eventData, raycastResults);

        BuildingService dropTarget = null;
        foreach (var r in raycastResults)
        {
            var card = r.gameObject.GetComponentInParent<WorkerCardUI>();
            if (card != null && card.Building != null)
            {
                dropTarget = card.Building;
                break;
            }
        }

        if (dropTarget != null && dropTarget != source && roster != null)
        {
            if (roster.CountIn(dropTarget) < dropTarget.MaxWorkers)
            {
                roster.MoveOneWorker(source, dropTarget);
            }
        }

        selectedSourceBuilding = null;
        Refresh();
    }
}