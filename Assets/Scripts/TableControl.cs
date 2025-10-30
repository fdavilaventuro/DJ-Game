using UnityEngine;

public class TableControl : MonoBehaviour
{
    public Vinyl vinyl { get; private set; }
    public bool isPlaying { get; private set; }
    public float volume { get; private set; }
    public float pitch { get; private set; }
    public AudioSource audioSource { get; private set; }

    // llaman a esto cuando coloquen el disco físicamente
    public void SetVinyl(Vinyl v)
    {
        vinyl = v;
    }

    // esto se llama cuando quiten el disco
    public void ClearVinyl()
    {
        vinyl = null;
        audioSource.Stop();
        isPlaying = false;
    }

    // cuando se muevan los sliders se cambia esto
    public void SetVolume(float v)
    {
        volume = v;
        audioSource.volume = volume;
    }

    // esto tambien
    public void SetPitch(float p)
    {
        pitch = p;
        audioSource.pitch = pitch;
    }

    // boton de play
    public void Play()
    {
        isPlaying = true;
        audioSource.Play();
    }

    // el otro boton, o el mismo pero manteniendo state
    public void Pause()
    {
        isPlaying = false;
        audioSource.Pause();

    }
}