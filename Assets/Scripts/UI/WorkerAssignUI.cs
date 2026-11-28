using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerAssignUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private WorkerCardUI[] cards;
    [SerializeField] private Button hireManagerButton;
    [SerializeField] private TMP_Text managerLabel;

    private BuildingService building;
    private string currentBuildingName = string.Empty;
    private WorkerRoster roster;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        if (hireManagerButton != null) hireManagerButton.onClick.AddListener(HireManager);
    }

    private void OnEnable()
    {
        roster = WorkerRoster.Instance;
        roster.Changed += Refresh;
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (roster != null) roster.Changed -= Refresh;
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
    }

    public void Open(BuildingInfo buildingInfo)
    {
        if (buildingInfo == null) return;

        BuildingService service = buildingInfo.GetComponentInParent<BuildingService>();
        if (service == null) service = buildingInfo.GetComponentInChildren<BuildingService>(true);
        if (service == null)
        {
            Debug.LogError("WorkerAssignUI: budova nema BuildingService.", buildingInfo);
            return;
        }

        string buildingName = string.IsNullOrEmpty(buildingInfo.buildingName)
            ? buildingInfo.gameObject.name
            : buildingInfo.buildingName;

        Show(service, buildingName);
    }

    public void OpenForNeed(NeedType need)
    {
        BuildingService[] services = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
        foreach (BuildingService s in services)
        {
            if (s.Need != need) continue;
            BuildingInfo info = s.GetComponentInChildren<BuildingInfo>(true);
            Show(s, info != null && !string.IsNullOrEmpty(info.buildingName) ? info.buildingName : s.name);
            return;
        }
        Debug.LogWarning("WorkerAssignUI: pre potrebu " + need + " nestoji ziadna budova.");
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Show(BuildingService service, string buildingName)
    {
        building = service;
        currentBuildingName = buildingName;
        gameObject.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        if (building == null || roster == null) return;

        RefreshManager();

        if (titleText != null)
        {
            int assigned = roster.CountIn(building);
            int maxSlots = building.MaxWorkers;
            titleText.text = $"ASSIGN STAFF - {currentBuildingName.ToUpperInvariant()} ({assigned}/{maxSlots})";
        }

        var workers = roster.Workers;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            bool has = i < workers.Count;
            cards[i].gameObject.SetActive(has);
            if (has) cards[i].Bind(workers[i], building);
        }
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Coins) RefreshManager();
    }

    private void HireManager()
    {
        building?.TryHireManager();
        RefreshManager();
    }

    private void RefreshManager()
    {
        if (hireManagerButton == null || managerLabel == null) return;
        var offer = building != null ? building.ManagerOffer : null;
        hireManagerButton.gameObject.SetActive(offer != null);
        if (offer == null) return;
        string role = $"{offer.displayName} (×{offer.speedMultiplier:0.##})";
        managerLabel.text = building.HasManager ? $"{role} • Hired" : $"Hire {role} • {offer.coinPrice} coins";
        hireManagerButton.interactable = !building.HasManager && ResourceManager.Instance != null &&
            ResourceManager.Instance.HasEnough(ResourceType.Coins, offer.coinPrice);
    }
}
