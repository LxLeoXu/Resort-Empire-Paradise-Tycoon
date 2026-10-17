using System.Globalization;
using TMPro;
using UnityEngine;

public class CoinsCounter : MonoBehaviour
{
    [SerializeField] TMP_Text coinsText;
    [SerializeField] int coins = 12450;

    void Start() => Refresh();

    public void Add100()
    {
        coins += 100;
        Refresh();
    }

    void Refresh() =>
        coinsText.text = coins.ToString("N0", CultureInfo.InvariantCulture);
}