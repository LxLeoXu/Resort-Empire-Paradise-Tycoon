using TMPro;
using UnityEngine;

public sealed class GemsHUDCounter : MonoBehaviour
{
    [SerializeField]
    private TMP_Text gemsText;

    private int lastAmount = int.MinValue;

    private void OnEnable()
    {
        lastAmount = int.MinValue;
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (gemsText == null)
            return;

        ResourceManager manager =
            ResourceManager.Instance;

        if (manager == null)
        {
            if (lastAmount != int.MinValue)
            {
                lastAmount = int.MinValue;
                gemsText.text = "--";
            }

            return;
        }

        int amount =
            manager.GetResource(ResourceType.Gems);

        if (amount == lastAmount)
            return;

        lastAmount = amount;

        gemsText.text = amount.ToString("N0");
    }
}