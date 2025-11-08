using UnityEngine;

public class Crossfader : MonoBehaviour
{
    [SerializeField] AudioSource leftSource;
    [SerializeField] AudioSource rightSource;

    private float leftBaseVolume;
    private float rightBaseVolume;

    void Start()
    {
        leftBaseVolume = leftSource.volume;
        rightBaseVolume = rightSource.volume;
    }

    public void whenValueChanged(float v)
    {
        leftSource.volume = leftBaseVolume * Mathf.Clamp01(1 - v);
        rightSource.volume = rightBaseVolume * Mathf.Clamp01(1 + v);
    }
}
