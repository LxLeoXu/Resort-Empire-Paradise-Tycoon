using System.Collections.Generic;
using UnityEngine;

public class BuildingSpot : MonoBehaviour
{
    [Header("Zoznam dostupných budov (Prefaby)")]
    [Tooltip("Sem presuň prefaby budov z priečinka Prefabs/buildings")]
    public List<GameObject> buildingPrefabs = new List<GameObject>();

    [Header("Stav miesta")]
    public bool isOccupied = false;
    private GameObject spawnedBuilding;

    // Zachované pre kliknutie myšou priamo na objekt v scéne
    private void OnMouseDown()
    {
        BuildBuilding(0); // Postaví predvolenú budovu (index 0)
    }

    /// <summary>
    /// Hlavná metóda na postavenie budovy podľa zadaného indexu.
    /// Túto metódu môžeš priradiť priamo do UI tlačidla On Click().
    /// </summary>
    public void BuildBuilding(int buildingIndex)
    {
        if (isOccupied)
        {
            Debug.LogWarning("Toto miesto je už obsadené, nedá sa tu stavať!");
            return;
        }

        if (buildingPrefabs == null || buildingPrefabs.Count == 0)
        {
            Debug.LogError("V zozname Building Prefabs nie sú priradené žiadne prefaby!");
            return;
        }

        if (buildingIndex < 0 || buildingIndex >= buildingPrefabs.Count)
        {
            Debug.LogError($"Index {buildingIndex} je mimo rozsahu zoznamu prefabs!");
            return;
        }

        GameObject prefabToBuild = buildingPrefabs[buildingIndex];

        if (prefabToBuild != null)
        {
            // Vytvorí budovu presne na súradniciach tohto miesta
            spawnedBuilding = Instantiate(prefabToBuild, transform.position, Quaternion.identity);
            isOccupied = true;

            // Skryje ikonu '+' (stavenisko)
            gameObject.SetActive(false);

            Debug.Log($"Budova '{prefabToBuild.name}' bola úspešne postavená na stavenisku.");
        }
    }

    // Pomocné metódy pre UI tlačidlá (môžeš ich ľahko vybrať v On Click zozname):
    public void BuildFirstBuilding() { BuildBuilding(0); }
    public void BuildSecondBuilding() { BuildBuilding(1); }
    public void BuildThirdBuilding() { BuildBuilding(2); }
}