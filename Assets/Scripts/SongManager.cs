using System.IO;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SongManager : MonoBehaviour
{
    public AudioSource audioSource; // assign in Inspector
    public string musicFolder = "Music  ";
    public TMP_Dropdown songDropdown; // optional UI for selection

    private List<string> songPaths = new List<string>();

    void Start()
    {
        LoadSongs();
        PopulateDropdown();
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
            if (file.EndsWith(".mp3") || file.EndsWith(".wav") || file.EndsWith(".ogg"))
            {
                songPaths.Add(file);
            }
        }

        Debug.Log($"Loaded {songPaths.Count} songs.");
    }

    void PopulateDropdown()
    {
        if (songDropdown == null) return;

        songDropdown.ClearOptions();
        List<string> songNames = new List<string>();

        // Add default "None Playing" option
        songNames.Add("None Playing");
        songDropdown.AddOptions(songNames);

        // Add the songs
        foreach (string path in songPaths)
            songDropdown.options.Add(new TMP_Dropdown.OptionData(Path.GetFileNameWithoutExtension(path)));

        songDropdown.value = 0; // default to "None Playing"
        songDropdown.RefreshShownValue();

        // Listen for changes
        songDropdown.onValueChanged.AddListener(OnSongSelected);
    }

    void OnSongSelected(int index)
    {
        if (index == 0)
        {
            // "None Playing" selected
            audioSource.Stop();
            audioSource.clip = null;
            Debug.Log("Playback stopped (None Playing selected).");
            return;
        }

        // Adjust index since index 0 = "None Playing"
        int songIndex = index - 1;

        if (songIndex >= 0 && songIndex < songPaths.Count)
        {
            Debug.Log($"Selected: {songDropdown.options[index].text}, File: {songPaths[songIndex]}");
            StartCoroutine(LoadAndPlay(songPaths[songIndex]));
        }
        else
        {
            Debug.LogWarning($"Invalid song index {songIndex}. Song list count: {songPaths.Count}");
        }
    }


    System.Collections.IEnumerator LoadAndPlay(string path)
    {
        string url = "file:///" + path.Replace("\\", "/");
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error loading audio from {url}: {www.error}");
            }
            else
            {
                var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
            }
        }
    }
}
