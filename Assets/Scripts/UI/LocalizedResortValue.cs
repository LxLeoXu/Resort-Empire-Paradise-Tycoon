
using UnityEngine;
using UnityEngine.Localization.Components;

[RequireComponent(typeof(LocalizeStringEvent))]
public class LocalizedResortValue : MonoBehaviour
{
    private LocalizeStringEvent localization;
    private ResourceManager manager;

    private void Awake()
    {
        localization = GetComponent<LocalizeStringEvent>();
    }

    private void Start()
    {
        manager = ResourceManager.Instance;

        if (manager == null)
        {
            Debug.LogError("ResourceManager neexistuje!");
            return;
        }

        localization.StringReference.Arguments = new object[]
        {
            manager.GetResource(ResourceType.ResortValue)
        };

        manager.OnResourceChanged += OnResourceChanged;
        localization.RefreshString();
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (type != ResourceType.ResortValue)
            return;

        localization.StringReference.Arguments = new object[]
        {
            amount
        };

        localization.RefreshString();
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.OnResourceChanged -= OnResourceChanged;
    }
}
