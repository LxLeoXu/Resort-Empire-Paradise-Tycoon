
using System.Globalization;
using TMPro;
using UnityEngine;

public class CoinsCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text coinsText;

    private ResourceManager resourceManager;

    private void Start()
    {
        resourceManager = ResourceManager.Instance;

        if (resourceManager == null)
        {
            Debug.LogError("ResourceManager not found!");
            return;
        }

        resourceManager.OnResourceChanged += OnResourceChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (resourceManager != null)
            resourceManager.OnResourceChanged -= OnResourceChanged;
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Coins)
            Refresh();
    }

    private void Refresh()
    {
        if (coinsText == null || resourceManager == null)
            return;

        coinsText.text = resourceManager
            .GetResource(ResourceType.Coins)
            .ToString("N0", CultureInfo.InvariantCulture);
    }

    public void Add100()
    {
        resourceManager?.AddResource(ResourceType.Coins, 100);
    }
}
