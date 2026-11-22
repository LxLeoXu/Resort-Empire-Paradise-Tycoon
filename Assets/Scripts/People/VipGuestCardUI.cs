
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VipGuestCardUI : MonoBehaviour
{
    [Header("Card")]
    [SerializeField] private GameObject cardRoot;
    [SerializeField] private GameObject requestsRoot;

    [Header("VIP")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text guestNameText;
    [SerializeField] private TMP_Text requestSummaryText;
    [SerializeField] private S7_VipRequestBoardUI vipRequestBoard;

    [Header("Requirements")]
    [SerializeField] private TMP_Text[] requirementTexts;
    [SerializeField] private Button[] requirementButtons;

    [Header("Buttons")]
    [SerializeField] private Button openRequestsButton;
    [SerializeField] private Button maybeLaterButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button closeRequestsButton;
    [SerializeField] private Button acceptButton;

    private VipGuestController controller;

    private void Awake()
    {
        if (maybeLaterButton != null)
            maybeLaterButton.onClick.AddListener(MaybeLater);

        if (declineButton != null)
            declineButton.onClick.AddListener(Decline);

        if (closeRequestsButton != null)
            closeRequestsButton.onClick.AddListener(CloseRequests);

        if (acceptButton != null)
            acceptButton.onClick.AddListener(Accept);

        if (requirementButtons != null)
        {
            for (int i = 0; i < requirementButtons.Length; i++)
            {
                int index = i;

                if (requirementButtons[i] != null)
                {
                    requirementButtons[i].onClick.AddListener(
                        () => FulfillRequirement(index));
                }
            }
        }

        if (openRequestsButton != null)
        {
            openRequestsButton.interactable = true;
            openRequestsButton.onClick.AddListener(OpenRequests);
        }

        if (requestsRoot != null)
            requestsRoot.SetActive(false);
    }

    private void OpenRequests()
    {
        if (controller == null) return;

        if (vipRequestBoard == null)
            vipRequestBoard = FindFirstObjectByType<S7_VipRequestBoardUI>(FindObjectsInactive.Include);

        if (vipRequestBoard != null)
        {
            Hide();
            vipRequestBoard.Show(controller);
        }
        else
        {
            Debug.LogError("VipGuestCardUI: S7_VipRequestBoardUI sa nenašiel v scéne!", this);
        }
    }

    public void Show(VipGuestController vipController)
    {
        if (vipController == null)
            return;

        if (controller != null)
            controller.Changed -= Refresh;

        controller = vipController;
        controller.Changed += Refresh;

        if (cardRoot != null && !cardRoot.activeSelf)
            cardRoot.SetActive(true);

        if (requestsRoot != null)
            requestsRoot.SetActive(false);

        Refresh();
    }

    public void Hide()
    {
        if (requestsRoot != null)
            requestsRoot.SetActive(false);

        if (cardRoot != null)
            cardRoot.SetActive(false);
    }

    private void CloseRequests()
    {
        if (requestsRoot != null)
            requestsRoot.SetActive(false);
    }

    private void MaybeLater()
    {
        if (controller != null && controller.TryMaybeLater())
            Hide();
    }

    private void Decline()
    {
        if (controller != null && controller.TryDecline())
            Hide();
    }

    private void Accept()
    {
        if (controller != null && controller.TryAccept())
            Hide();
        else
            Refresh();
    }

    private void FulfillRequirement(int index)
    {
        if (controller == null ||
            controller.Definition == null ||
            index < 0 ||
            index >= controller.Definition.requirements.Count)
            return;

        string requirementId =
            controller.Definition.requirements[index].id;

        if (!controller.TryFulfillRequirement(requirementId))
        {
            Debug.LogWarning(
                "VIP requirement could not be fulfilled: " +
                controller.LastError);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (controller == null ||
            controller.Definition == null)
            return;

        var definition = controller.Definition;
        var visit = controller.CurrentVisit;

        if (definition.requirements == null ||
            visit.fulfilled == null)
            return;

        if (portraitImage != null)
            portraitImage.sprite = definition.portrait;

        if (guestNameText != null)
            guestNameText.text = definition.displayName;

        bool allRequiredFulfilled =
            definition.requirements
                .Where(r => !r.bonus)
                .All(r => visit.fulfilled.Contains(r.id));

        if (requestSummaryText != null)
        {
            int completed = definition.requirements.Count(
                r => visit.fulfilled.Contains(r.id));

            requestSummaryText.text =
                "REQUESTS " +
                completed +
                "/" +
                definition.requirements.Count;
        }

        if (requirementTexts != null)
        {
            for (int i = 0; i < requirementTexts.Length; i++)
            {
                if (requirementTexts[i] == null)
                    continue;

                if (i >= definition.requirements.Count)
                {
                    requirementTexts[i].gameObject.SetActive(false);
                    continue;
                }

                var requirement = definition.requirements[i];

                bool fulfilled =
                    visit.fulfilled.Contains(requirement.id);

                requirementTexts[i].gameObject.SetActive(true);

                requirementTexts[i].text =
                    requirement.description +
                    " - " +
                    requirement.materials +
                    " Materials" +
                    (fulfilled ? " - COMPLETED" : "");
            }
        }

        if (requirementButtons != null)
        {
            for (int i = 0; i < requirementButtons.Length; i++)
            {
                if (requirementButtons[i] == null)
                    continue;

                if (i >= definition.requirements.Count)
                {
                    requirementButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var requirement = definition.requirements[i];

                bool fulfilled =
                    visit.fulfilled.Contains(requirement.id);

                requirementButtons[i].gameObject.SetActive(true);

                requirementButtons[i].interactable =
                    controller.IsWaiting &&
                    !fulfilled &&
                    ResourceManager.Instance != null &&
                    ResourceManager.Instance.HasEnough(
                        ResourceType.Materials,
                        requirement.materials);
            }
        }

        if (acceptButton != null)
        {
            acceptButton.interactable =
                controller.IsWaiting &&
                allRequiredFulfilled;
        }
    }

    private void OnDestroy()
    {
        if (controller != null)
            controller.Changed -= Refresh;
    }
}
