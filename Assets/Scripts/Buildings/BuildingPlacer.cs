//using UnityEngine;
///using UnityEngine.InputSystem; // Pre nový Input System
//
//public class BuildingPlacer : MonoBehaviour
//{
//    [Header("Vybraná budova")]
//    public GameObject buildingPrefabToPlace; // Prefab z priečinka Prefabs/buildings/
//
//    [Header("Maska vrstvy (voliteľné)")]
//    public LayerMask spotLayerMask;

//    private Camera mainCamera;

  //  private void Awake()
    //{
    //    mainCamera = Camera.main;
    //}

 //   private void Update()
 //   {
        // Kontrola kliknutia myšou / dotyku (funguje s novým Pointer / Mouse vstupom)
  //      if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
    ///    {
       //     Vector2 mousePosition = Pointer.current.position.ReadValue();
         //   RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(mousePosition), Vector2.zero, Mathf.Infinity, spotLayerMask);
//
  //          if (hit.collider != null)
    //        {
      //          // Hľadáme skript BuildingSpot na kliknutom objekte
        //        BuildingSpot spot = hit.collider.GetComponent<BuildingSpot>();
//
  //              if (spot != null)
    //            {
      //              // Volanie funkcie TryBuild - teraz funguje správne v podmienke IF
        //            if (spot.TryBuild(buildingPrefabToPlace))
          //          {
            //            Debug.Log("Budova bola úspešne postavená!");
              //      }
                //    else
                  //  {
                    //    Debug.Log("Stavba zlyhala (miesto je obsadené alebo chýba prefab).");
  //                  }
    //            }
      //      }
 //       }
   // }
//}