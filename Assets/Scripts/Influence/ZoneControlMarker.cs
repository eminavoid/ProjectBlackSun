using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placeholder visual: etiqueta de secta controladora sobre la zona.
/// </summary>
[DisallowMultipleComponent]
public class ZoneControlMarker : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private float labelScale = 0.08f;

    private TextMesh label;
    private FactionId? shownFaction;

    public void Refresh(ZoneInfluenceState state)
    {
        if (state == null || state.Status != ZoneControlStatus.Controlled || !state.Controller.HasValue)
        {
            SetVisible(false);
            shownFaction = null;
            return;
        }

        EnsureLabel();
        FactionId faction = state.Controller.Value;
        if (shownFaction != faction)
        {
            label.text = FactionIdUtil.ShortLabel(faction);
            label.color = ColorFor(faction);
            shownFaction = faction;
        }

        SetVisible(true);
    }

    private void EnsureLabel()
    {
        if (label != null) return;

        GameObject go = new GameObject("ControlMarker");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = worldOffset;
        go.transform.localScale = Vector3.one * labelScale;

        label = go.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = 0.5f;
        label.color = Color.white;
        label.text = string.Empty;
    }

    private void SetVisible(bool visible)
    {
        if (label != null) label.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (label == null || !label.gameObject.activeSelf) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
    }

    private static Color ColorFor(FactionId faction)
    {
        switch (faction)
        {
            case FactionId.Player: return new Color(0.95f, 0.85f, 0.2f);
            case FactionId.Rival1: return new Color(0.9f, 0.3f, 0.3f);
            case FactionId.Rival2: return new Color(0.3f, 0.6f, 0.95f);
            case FactionId.Rival3: return new Color(0.4f, 0.9f, 0.4f);
            case FactionId.Rival4: return new Color(0.85f, 0.4f, 0.9f);
            default: return Color.white;
        }
    }
}
