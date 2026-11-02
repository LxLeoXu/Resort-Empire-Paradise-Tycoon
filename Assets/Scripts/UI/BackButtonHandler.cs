using UnityEngine;
using UnityEngine.InputSystem; // ak používaš nový Input System

public class BackButtonHandler : MonoBehaviour
{
    [SerializeField] ModalWindow quitModal;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            quitModal.Open();
    }
}