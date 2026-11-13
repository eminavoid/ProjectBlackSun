using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class DayPassage : MonoBehaviour
{
    [Tooltip("The sun circles the city once while the turn passes, as long as GameTime's Turn Start Delay.")]
    [SerializeField, Range(0f, 1f)] private float nightLight = 0.2f;
    [SerializeField, Range(0f, 1f)] private float nightAmbient = 0.45f;
    [Tooltip("Multiplies the sun's own color: dusk, night and dawn. Keep both ends white.")]
    [SerializeField] private Gradient tint = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(new Color(1f, 0.62f, 0.42f), 0.25f),
            new GradientColorKey(new Color(0.45f, 0.55f, 1f), 0.5f),
            new GradientColorKey(new Color(1f, 0.78f, 0.78f), 0.75f),
            new GradientColorKey(Color.white, 1f)
        }
    };

    private Light sun;
    private Vector3 dayAngles;
    private float dayIntensity;
    private Color dayColor;
    private Color skyAmbient;
    private Color equatorAmbient;
    private Color groundAmbient;
    private Tween passage;

    private void Awake()
    {
        sun = GetComponent<Light>();
        dayAngles = transform.eulerAngles;
        dayIntensity = sun.intensity;
        dayColor = sun.color;
        skyAmbient = RenderSettings.ambientSkyColor;
        equatorAmbient = RenderSettings.ambientEquatorColor;
        groundAmbient = RenderSettings.ambientGroundColor;
    }

    private void OnEnable() => GameTime.OnTurnEnded += Pass;

    private void OnDisable()
    {
        GameTime.OnTurnEnded -= Pass;
        passage?.Kill();
        Apply(1f);
    }

    private void Pass()
    {
        float duration = GameTime.TurnStartDelay;
        if (duration <= 0f) return;

        passage?.Kill();
        passage = DOVirtual.Float(0f, 1f, duration, Apply)
            .SetEase(Ease.InOutSine)
            .OnKill(() => Apply(1f))
            .SetLink(gameObject);
    }

    private void Apply(float t)
    {
        float night = Mathf.Sin(t * Mathf.PI);
        night *= night;

        transform.rotation = Quaternion.Euler(dayAngles.x, dayAngles.y + 360f * t, dayAngles.z);
        sun.intensity = dayIntensity * Mathf.Lerp(1f, nightLight, night);
        sun.color = dayColor * tint.Evaluate(t);

        float ambient = Mathf.Lerp(1f, nightAmbient, night);
        RenderSettings.ambientSkyColor = skyAmbient * ambient;
        RenderSettings.ambientEquatorColor = equatorAmbient * ambient;
        RenderSettings.ambientGroundColor = groundAmbient * ambient;
    }
}
