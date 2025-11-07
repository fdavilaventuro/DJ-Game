using UnityEngine;

public class Crossfader : MonoBehaviour
{
    [SerializeField] AudioSource leftSource;
    [SerializeField] AudioSource rightSource;

    public void whenValueChanged(float v)
    {
        leftSource.volume = Mathf.Clamp(leftSource.volume - v, 0, 1);
        rightSource.volume = Mathf.Clamp(rightSource.volume + v, 0, 1);
    }
}
