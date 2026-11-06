using UnityEngine;

/// <summary>
/// Living glow for a collectible: pulses its light (a slow breath, or a lub-dub heartbeat),
/// bobs the model and, for the heart, swells the model on each beat.
/// </summary>
public class DropGlow : MonoBehaviour
{
    public Light glowLight;
    public Transform model;
    [Tooltip("Pulses per second.")]
    public float pulseSpeed = 0.6f;
    public bool heartbeat;
    [Tooltip("Model scale swell at the top of each pulse.")]
    public float beatScale;
    public float bobHeight = 0.05f;

    float baseIntensity, phase;
    Vector3 baseModelPosition, baseModelScale;

    void Start()
    {
        if (glowLight != null) baseIntensity = glowLight.intensity;
        if (model != null) { baseModelPosition = model.localPosition; baseModelScale = model.localScale; }
        phase = Random.value; // drops side by side don't pulse in lockstep
    }

    void Update()
    {
        float t = Time.time * pulseSpeed + phase;
        float pulse = heartbeat ? Heartbeat(t) : 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI);
        if (glowLight != null) glowLight.intensity = baseIntensity * (0.65f + 0.7f * pulse);
        if (model == null) return;
        model.localScale = baseModelScale * (1f + beatScale * pulse);
        // Bob in world metres whatever the parent's scale (the heart root is scaled 0.04).
        model.localPosition = baseModelPosition + model.parent.InverseTransformVector(Vector3.up * bobHeight * Mathf.Sin(t * Mathf.PI));
    }

    // Two quick beats ("lub-dub") then rest, once per cycle.
    static float Heartbeat(float t)
    {
        float cycle = t - Mathf.Floor(t);
        return Mathf.Max(Spike(cycle, 0.08f), 0.7f * Spike(cycle, 0.3f));
    }

    static float Spike(float cycle, float at) => Mathf.Exp(-Mathf.Pow((cycle - at) / 0.06f, 2f));
}
