using TMPro;
using UnityEngine;

public class SuperAwesomeSlider : MonoBehaviour
{
    public float minX = -0.04f;
    public float maxX = 0.04f;
    public float delta = 0.5f;
    public float last = 0.5f;
    public TextMeshPro tmp;
    public AudioSource source;
    public float minPitch = 1.02f;
    public float maxPitch = 0.98f;

    void Update()
    {
        float norm = transform.position.x + maxX;
        delta = Mathf.Clamp(norm / (maxX - minX), 0.0f, 1.0f);
        tmp.text = "Volume: " + delta.ToString("F2");
        float roundedFloat = Mathf.Round(delta * 100.0f) / 100.0f;
        float step = Mathf.Round(roundedFloat * 10.0f) / 10.0f;

        if (Mathf.Abs(step - last) >= 0.1f)
        {
            Debug.Log("Played sound");
            float pitch = Mathf.Lerp(minPitch, maxPitch, delta);
            source.pitch = pitch;
            source.Play();
            last = delta;
        }
    }
}
