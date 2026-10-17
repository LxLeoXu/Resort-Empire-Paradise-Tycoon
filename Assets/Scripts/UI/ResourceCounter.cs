
using System.Globalization;
using TMPro;
using UnityEngine;

public class ResourceCounter : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private TMP_Text valueText;

    private ResourceManager manager;

    private void Start()
    {
        manager = ResourceManager.Instance;

        if (manager == null || valueText == null)
        {
            Debug.LogError("ResourceCounter: Missing manager or text!");
            return;
        }

        manager.OnResourceChanged += OnResourceChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.OnResourceChanged -= OnResourceChanged;
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (type == resourceType)
            Refresh();
    }

    private void Refresh()
    {
        int amount = manager.GetResource(resourceType);

        switch (resourceType)
        {
            case ResourceType.Energy:
                valueText.text =
                    $"{amount}/{ResourceManager.MaxEnergy}";
                break;

            case ResourceType.ResortValue:
                valueText.text =
                    $"Resort Value: {amount:N0}";
                break;

            case ResourceType.Coins:
                valueText.text =
                    amount.ToString("N0", CultureInfo.InvariantCulture);
                break;

            default:
                valueText.text = amount.ToString();
                break;
        }
    }
}
