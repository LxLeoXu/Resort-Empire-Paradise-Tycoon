using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorkerCardUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Farby okraja (Border)")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Color normalBorderColor = new Color(0.42f, 0.23f, 0.08f, 1f); // Hnedá
    [SerializeField] private Color selectedBorderColor = new Color(0.06f, 0.65f, 0.91f, 1f); // Modrá (Selected)
    [SerializeField] private Color emptyBorderColor = new Color(0.98f, 0.45f, 0.09f, 1f); // Oranžová (Empty)

    private Image buildingIcon;
    private TMP_Text buildingName;
    private GameObject noManagerBadge;
    private GameObject managerBadge;
    private TMP_Text managerBadgeText;
    private Transform workerBadge;

    private readonly List<Image> slotIcons = new();
    private Sprite occupiedSprite;
    private Sprite emptySprite;

    private BuildingService cardBuilding;
    private WorkerAssignUI parentAssignUI;
    private bool isDragging;
    private bool captured;

    public BuildingService Building => cardBuilding;

    private void Awake() => Capture();

    private void Capture()
    {
        if (captured) return;
        captured = true;

        if (borderImage == null) borderImage = GetComponent<Image>();

        buildingIcon = transform.Find("BuildingIcon")?.GetComponent<Image>();
        buildingName = transform.Find("BuildingName")?.GetComponent<TMP_Text>();
        noManagerBadge = transform.Find("NoManagerBadge")?.gameObject;
        managerBadge = transform.Find("ManagerBadge")?.gameObject;
        if (managerBadge != null)
            managerBadgeText = managerBadge.GetComponentInChildren<TMP_Text>(true);
        workerBadge = transform.Find("WorkerBadge");

        if (workerBadge != null)
        {
            foreach (Transform child in workerBadge)
            {
                var img = child.GetComponent<Image>();
                if (img != null) slotIcons.Add(img);
            }
        }

        if (slotIcons.Count > 0 && slotIcons[0] != null) occupiedSprite = slotIcons[0].sprite;
        if (slotIcons.Count > 1 && slotIcons[1] != null) emptySprite = slotIcons[1].sprite;
    }

    public void Bind(BuildingService building, BuildingService selectedSource, WorkerRoster roster, WorkerAssignUI assignUI)
    {
        Capture();
        cardBuilding = building;
        parentAssignUI = assignUI;
        if (building == null || roster == null) return;

        var info = building.GetComponentInChildren<BuildingInfo>(true);
        if (buildingName != null)
            buildingName.text = info != null && !string.IsNullOrEmpty(info.buildingName)
                ? info.buildingName
                : building.name.Replace("(Clone)", string.Empty).Trim();

        if (buildingIcon != null && info != null && info.buildingIcon != null)
            buildingIcon.sprite = info.buildingIcon;

        int workers = roster.CountIn(building);
        int maxSlots = Mathf.Max(1, building.MaxWorkers);

        bool isSelected = building == selectedSource;
        bool isEmpty = workers == 0;

        // VIZUÁL OKRAJA:
        // 1. Ak je vybratá -> MODRÁ
        // 2. Ak je prázdna (0 pracovníkov) -> ORANŽOVÁ
        // 3. Ostatné unselected s pracovníkom -> HNEDÁ
        if (borderImage != null)
        {
            borderImage.color = isSelected
                ? selectedBorderColor
                : (isEmpty ? emptyBorderColor : normalBorderColor);
        }

        // Manažér badge
        var managerRole = WorkerRoster.GetManagerRoleForNeed(building.Need);
        bool hasManager = managerRole != VipRequiredRole.None && roster.IsRoleAvailable(managerRole);
        if (managerBadge != null) managerBadge.SetActive(hasManager);
        if (noManagerBadge != null) noManagerBadge.SetActive(!hasManager);
        if (hasManager && managerBadgeText != null)
            managerBadgeText.text = $"{WorkerRoster.GetRoleDisplayName(managerRole)} x 1.2";

        // Sloty pracovníkov
        if (workerBadge != null && slotIcons.Count > 0)
        {
            var template = slotIcons.Count > 1 && slotIcons[1] != null ? slotIcons[1] : slotIcons[0];
            while (slotIcons.Count < maxSlots && template != null)
                slotIcons.Add(Instantiate(template, workerBadge));

            for (int i = 0; i < slotIcons.Count; i++)
            {
                if (slotIcons[i] == null) continue;
                bool active = i < maxSlots;
                slotIcons[i].gameObject.SetActive(active);
                if (active)
                    slotIcons[i].sprite = (i < workers ? occupiedSprite : emptySprite) ?? slotIcons[i].sprite;
            }
        }
    }

    // =====================================================
    // EVENTY PRE DOTYK / PREŤAHOVANIE (DRAG & DROP)
    // =====================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (cardBuilding == null || parentAssignUI == null) return;

        // Ťahať sa dá len z budovy, ktorá má aspoň 1 pracovníka
        int workers = WorkerRoster.Instance != null ? WorkerRoster.Instance.CountIn(cardBuilding) : 0;
        if (workers <= 0) return;

        isDragging = true;
        parentAssignUI.StartDraggingWorker(cardBuilding, occupiedSprite, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || parentAssignUI == null) return;
        parentAssignUI.UpdateDragPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || parentAssignUI == null) return;
        isDragging = false;
        parentAssignUI.EndDraggingWorker(cardBuilding, eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ak to bol drag, neklikáme
        if (isDragging) return;

        if (parentAssignUI != null && cardBuilding != null)
            parentAssignUI.HandleCardClicked(cardBuilding);
    }
}