using UnityEngine;

// estos los crean y se puede crear una db global con un singleton
[CreateAssetMenu(fileName = "VinylData", menuName = "Scriptable Objects/VinylData")]
public class VinylData : ScriptableObject
{
    public string title;
    public AudioClip clip;
    public float bpm;
    public float offset;
    public Texture2D cover;
}
