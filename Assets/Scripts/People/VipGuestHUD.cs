using UnityEngine;
using UnityEngine.UI;

public class VipGuestHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VipGuestController controller;
    [SerializeField] private Button vipButton;
    [SerializeField] private VipGuestCardUI vipCard;

    private void Awake()
    {
        vipButton.onClick.AddListener(OpenCard);
    }

    private void OnEnable()
    {
        if (controller == null) return;

        controller.GuestArrived += OnVipArrived;
        controller.VisitRestored += OnVipRestored;
        controller.Changed += Refresh;

        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (controller == null) return;

        controller.GuestArrived -= OnVipArrived;
        controller.VisitRestored -= OnVipRestored;
        controller.Changed -= Refresh;
    }

    private void OnVipArrived(VipGuestDefinition definition)
    {
        Refresh();
        vipCard.Show(controller);
    }

    private void OnVipRestored(VipGuestDefinition definition)
    {
        Refresh();
        vipCard.Show(controller);
    }

    private void OpenCard()
    {
        if (controller != null && controller.IsWaiting)
            vipCard.Show(controller);
    }

    private void Refresh()
    {
        if (vipButton == null || controller == null)
            return;

        vipButton.gameObject.SetActive(controller.IsWaiting);
    }
}