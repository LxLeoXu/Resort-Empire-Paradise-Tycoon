using System.Collections;
using TMPro;
using UnityEngine;

public class WorkerRelocationFeedbackUI : MonoBehaviour
{
    [Tooltip("Objekt s Ivanovým pozadím, ktorý sa po presune na chvíľu ukáže.")]
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float displayDuration = 3f;

    private WorkerRoster roster;
    private Coroutine hideRoutine;

    private void OnEnable()
    {
        roster = WorkerRoster.Instance;

        if (roster != null)
            roster.WorkerMoved += OnWorkerMoved;

        if (messageRoot != null)
            messageRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (roster != null)
            roster.WorkerMoved -= OnWorkerMoved;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);
    }

    private void OnWorkerMoved(
        WorkerRoster.Worker worker,
        BuildingService previous,
        BuildingService destination)
    {
        if (destination == null || messageRoot == null)
            return;

        BuildingInfo info = destination.GetComponentInChildren<BuildingInfo>(true);

        string buildingName = info != null && !string.IsNullOrEmpty(info.buildingName)
            ? info.buildingName
            : destination.name;

        if (messageText != null)
            messageText.text = buildingName + " is catching up";

        messageRoot.SetActive(true);

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideMessage());
    }

    private IEnumerator HideMessage()
    {
        yield return new WaitForSecondsRealtime(displayDuration);

        if (messageRoot != null)
            messageRoot.SetActive(false);

        hideRoutine = null;
    }
}