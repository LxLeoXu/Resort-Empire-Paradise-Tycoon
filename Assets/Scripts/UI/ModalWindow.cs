using UnityEngine;

public class ModalWindow : MonoBehaviour
{
    public void Open()  => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
}