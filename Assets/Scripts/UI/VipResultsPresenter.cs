using UnityEngine;

// S8 is an instance of the existing S4 view, with its own VIP claim binding.
public sealed class VipResultsPresenter : MonoBehaviour
{
    private VipGuestController owner;
    private S4FigmaResultsUI view;
    public void Bind(VipGuestController controller) { owner = controller; }
    private void LateUpdate()
    {
        if (owner == null || !owner.IsReady || GamePersistence.IsRestoring) return;
        if (!owner.HasPendingResult) { if (view != null) view.HideResults(); return; }
        if (GamePersistence.IsPaused || (view != null && view.gameObject.activeInHierarchy)) return;
        // Do not obscure the ordinary result or other active modal windows.
        foreach (var modal in FindObjectsByType<ModalWindow>(FindObjectsSortMode.None))
            if (modal.gameObject.activeInHierarchy) return;
        if (view == null)
        {
            foreach (var candidate in FindObjectsByType<S4FigmaResultsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.IsVipVariant) continue;
                if (candidate.gameObject.activeInHierarchy) return;
                view = candidate.CreateVipVariant(owner);
                break;
            }
        }
        if (view != null) view.ShowVipResults(owner.PendingResult);
    }
    private void OnDestroy() { if (view != null) Destroy(view.gameObject); }
}
