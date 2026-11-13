using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerAssignUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private WorkerCardUI[] cards;

    private BuildingService building;
    private WorkerRoster roster;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        roster = WorkerRoster.Instance;
        roster.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (roster != null) roster.Changed -= Refresh;
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
        if (titleText != null) titleText.text = "ASSIGN STAFF - " + buildingName.ToUpperInvariant();
        gameObject.SetActive(true);
        Refresh();
    }

    private void Refresh()
    {
        if (building == null || roster == null) return;

        var workers = roster.Workers;
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            bool has = i < workers.Count;
            cards[i].gameObject.SetActive(has);
            if (has) cards[i].Bind(workers[i], building);
        }
    }
}