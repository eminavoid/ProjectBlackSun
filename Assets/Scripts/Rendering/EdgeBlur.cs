using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, VolumeComponentMenu("Faith Eclipse/Edge Blur")]
public class EdgeBlur : VolumeComponent
{
    [Tooltip("Blur strength at the screen's edges. 0 turns the effect off.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Widest blur, in thousandths of the screen height.")]
    public ClampedFloatParameter radius = new ClampedFloatParameter(5f, 0f, 20f);
    [Tooltip("How far from the center, 0 to 1 toward the edges, the view stays sharp.")]
    public ClampedFloatParameter sharpArea = new ClampedFloatParameter(0.6f, 0f, 1.5f);
    [Tooltip("How gradually the blur grows past the sharp area.")]
    public ClampedFloatParameter falloff = new ClampedFloatParameter(0.6f, 0.05f, 1.5f);

    public bool IsActive() => intensity.value > 0f && radius.value > 0f;
}
