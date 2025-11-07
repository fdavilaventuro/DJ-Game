using UnityEngine;

public class DJTable : MonoBehaviour
{
    AudioSource audioSource;

    // ±6% range for tempo
    private const float MinPitch = 0.94f; // -6%
    private const float MaxPitch = 1.06f; // +6%

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void SetVolume(float volume)
    {
        audioSource.volume = volume;
    }

    public void SetPitch(float pitch)
    {
        pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
        audioSource.pitch = pitch;
    }

    public void Play()
    {
        audioSource.Play();
    }

    public void Stop()
    {
        audioSource.Stop();
    }

    public void Pause()
    {
        audioSource.Pause();
    }

    public void Toggle()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
        else
        {
            audioSource.Play();
        }
    }
}
