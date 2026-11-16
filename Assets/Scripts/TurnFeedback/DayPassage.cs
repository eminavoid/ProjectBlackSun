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

    [Header("Eclipse Gloom")]
    [Tooltip("From this many turns left, each day starts a little darker until the eclipse.")]
    [SerializeField, Min(1)] private int gloomTurns = 5;
    [SerializeField, Range(0f, 1f)] private float eclipseLight = 0.5f;
    [SerializeField, Range(0f, 1f)] private float eclipseAmbient = 0.55f;
    [SerializeField] private Color eclipseTint = new Color(1f, 0.6f, 0.45f, 1f);

    private Light sun;
    private GameLoopEvents gameLoop;
    private float gloomFrom;
    private float gloomTo;
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

    private void Start()
    {
        gameLoop = FindAnyObjectByType<GameLoopEvents>();
        gloomFrom = gloomTo = Gloom(TurnsLeft(0));
        Apply(1f);
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
        gloomFrom = gloomTo;
        gloomTo = Gloom(TurnsLeft(1));
        if (duration <= 0f)
        {
            Apply(1f);
            return;
        }

        passage?.Kill();
        passage = DOVirtual.Float(0f, 1f, duration, Apply)
            .SetEase(Ease.InOutSine)
            .OnKill(() => Apply(1f))
            .SetLink(gameObject);
    }

    private int TurnsLeft(int turnsAhead)
    {
        return gameLoop == null ? int.MaxValue : gameLoop.TurnsUntilEclipse - turnsAhead;
    }

    private float Gloom(int turnsLeft)
    {
        if (turnsLeft > gloomTurns) return 0f;
        return Mathf.Clamp01((gloomTurns + 1 - turnsLeft) / (float)gloomTurns);
    }

    private void Apply(float t)
    {
        float night = Mathf.Sin(t * Mathf.PI);
        night *= night;
        float gloom = Mathf.Lerp(gloomFrom, gloomTo, t);

        transform.rotation = Quaternion.Euler(dayAngles.x, dayAngles.y + 360f * t, dayAngles.z);
        sun.intensity = dayIntensity * Mathf.Lerp(1f, nightLight, night) * Mathf.Lerp(1f, eclipseLight, gloom);
        sun.color = dayColor * tint.Evaluate(t) * Color.Lerp(Color.white, eclipseTint, gloom);

        float ambient = Mathf.Lerp(1f, nightAmbient, night) * Mathf.Lerp(1f, eclipseAmbient, gloom);
        RenderSettings.ambientSkyColor = skyAmbient * ambient;
        RenderSettings.ambientEquatorColor = equatorAmbient * ambient;
        RenderSettings.ambientGroundColor = groundAmbient * ambient;
    }
}
