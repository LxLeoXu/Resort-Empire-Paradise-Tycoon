using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class BuildingClickHandler : MonoBehaviour
{
    private BuildingInfo buildingInfo;
    private Camera mainCamera;
    private Collider2D myCollider;

    private void Awake()
    {
        buildingInfo = GetComponentInParent<BuildingInfo>();
        myCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        bool wasPressed = false;
        Vector2 screenPos = Vector2.zero;

        // Podpora pre New Input System
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            wasPressed = true;
            screenPos = Pointer.current.position.ReadValue();
        }
#else
        // Podpora pre starý Input System
        if (Input.GetMouseButtonDown(0))
        {
            wasPressed = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (!wasPressed) return;

        // 1. Ak hráč klikol na UI tlačidlo alebo panel, ignorujeme to
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null) return;

        // 2. Vystrelíme lúč do 2D sveta na pozíciu kliknutia / dotyku
        Vector2 worldPoint = mainCamera.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        // 3. Skontrolujeme, či sme trafili presne tento Collider
        if (hit.collider != null && hit.collider == myCollider)
        {
            OnBuildingClicked();
        }
    }

    private void OnBuildingClicked()
    {
        Debug.Log($"<color=green>[KLIK FUNGUJE!]</color> Ťukol si na budovu: {(buildingInfo != null ? buildingInfo.buildingName : gameObject.name)}");

        if (buildingInfo == null || !buildingInfo.isBuilt)
        {
            Debug.LogWarning("Budova buď nemá BuildingInfo, alebo ešte nie je postavená!");
            return;
        }

        // Nájdeme akčné menu a otvoríme ho
        BuildingActionMenuUI menu = BuildingActionMenuUI.Instance;
        if (menu == null)
            menu = FindObjectOfType<BuildingActionMenuUI>(true);

        if (menu != null)
        {
            menu.Open(buildingInfo);
        }
        else
        {
            Debug.LogError("V scéne sa nenašiel objekt BuildingActionMenuUI v Canvas!");
        }
    }
}