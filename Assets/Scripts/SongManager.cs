using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SongManager : MonoBehaviour
{
    public TMP_Dropdown songDropdown;
    public DJTable djTable;
    public RawImage coverImage;
    public Texture fallbackTexture;

    private List<string> songPaths = new List<string>();

    void Start()
    {
        LoadSongs();
        PopulateDropdown();
        coverImage.texture = fallbackTexture;
    }

    void LoadSongs()
    {
        string folderPath = Path.Combine(Application.streamingAssetsPath, "Music");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Music folder not found at {folderPath}");
            return;
        }

        string[] files = Directory.GetFiles(folderPath);
        foreach (string file in files)
        {
            if (file.EndsWith(".ogg"))
                songPaths.Add(file);
        }

        Debug.Log($"Loaded {songPaths.Count} songs.");
    }

    void PopulateDropdown()
    {
        if (songDropdown == null) return;

        songDropdown.ClearOptions();
        List<string> songNames = new List<string> { "None Playing" };
        foreach (string path in songPaths)
        {
            string displayName = null;

            if (djTable != null)
            {
                displayName = djTable.GetTrackDisplayNameFromFile(path);
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = Path.GetFileNameWithoutExtension(path);
            }

            songNames.Add(displayName);
        }

        songDropdown.AddOptions(songNames);
        songDropdown.onValueChanged.AddListener(OnSongSelected);
    }

    void OnSongSelected(int index)
    {
        if (index == 0)
        {
            djTable.Stop();
            return;
        }

        string selectedPath = songPaths[index - 1];
        djTable.LoadTrack(selectedPath);
        coverImage.texture = djTable.ReadCoverArtFromSound() ?? fallbackTexture;
        djTable.Play();
    }
}
