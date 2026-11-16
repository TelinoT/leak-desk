using UnityEngine;

/// <summary>Slow, looping drift for the office backdrop camera so the desk feels alive behind the UI.</summary>
public class CameraDrift : MonoBehaviour
{
    [SerializeField] Vector3 amplitude = new(1.6f, 0.4f, 1.2f);
    [SerializeField] Vector3 rotationAmplitude = new(1.2f, 3f, 0f);
    [SerializeField] float period = 40f;

    Vector3 basePos;
    Quaternion baseRot;
    float introT = 1f;
    static CameraDrift instance;

    /// <summary>Opening glide: start high above the office and settle into the desk view.</summary>
    public static void PlayIntro()
    {
        if (instance != null) instance.introT = 0f;
    }

    /// <summary>Slow push-in used behind the verdict.</summary>
    public static void PushIn()
    {
        if (instance != null) instance.pushT = 0f;
    }

    float pushT = -1f;

    void Awake() => instance = this;

    void Start()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    void LateUpdate()
    {
        float t = Time.time * Mathf.PI * 2f / period;
        transform.position = basePos + new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 0.7f) * amplitude.y, Mathf.Cos(t * 0.8f) * amplitude.z);
        transform.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.6f) * rotationAmplitude.x, Mathf.Sin(t) * rotationAmplitude.y, 0f);

        if (introT < 1f)
        {
            introT = Mathf.Min(1f, introT + Time.unscaledDeltaTime / 4.5f);
            float k = 1f - Mathf.SmoothStep(0f, 1f, introT);
            transform.position += new Vector3(-3f, 9f, -6f) * k;
            transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation * Quaternion.Euler(18f, -25f, 0f), k);
        }
        if (pushT >= 0f)
        {
            pushT = Mathf.Min(1f, pushT + Time.unscaledDeltaTime / 6f);
            transform.position += transform.forward * Mathf.SmoothStep(0f, 1f, pushT) * 3f;
        }
    }
}
