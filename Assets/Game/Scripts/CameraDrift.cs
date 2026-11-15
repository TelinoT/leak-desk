using UnityEngine;

/// <summary>Slow, looping drift for the office backdrop camera so the desk feels alive behind the UI.</summary>
public class CameraDrift : MonoBehaviour
{
    [SerializeField] Vector3 amplitude = new(1.6f, 0.4f, 1.2f);
    [SerializeField] Vector3 rotationAmplitude = new(1.2f, 3f, 0f);
    [SerializeField] float period = 40f;

    Vector3 basePos;
    Quaternion baseRot;

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
    }
}
