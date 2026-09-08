using UnityEngine;

/// <summary>
/// Deprecated: el hover de stats sobre el nodo seleccionado lo reemplaza <see cref="MapStatsPanel"/>.
/// Si queda un componente viejo en escena, se apaga y borra el TMP.
/// </summary>
[DisallowMultipleComponent]
public class ZoneControlMarker : MonoBehaviour
{
    public void Deprecate()
    {
        DestroyLabel();
        enabled = false;
    }

    public void Refresh(ZoneInfluenceState state)
    {
        Deprecate();
    }

    private void Awake()
    {
        Deprecate();
    }

    private void OnEnable()
    {
        Deprecate();
    }

    private void OnDestroy()
    {
        DestroyLabel();
    }

    private void DestroyLabel()
    {
        DestroyChild("ControlMarker");
        DestroyChild("ControlMarkerOutline");
    }

    private void DestroyChild(string childName)
    {
        Transform leftover = transform.Find(childName);
        if (leftover == null) return;
        if (Application.isPlaying) Destroy(leftover.gameObject);
        else DestroyImmediate(leftover.gameObject);
    }
}
