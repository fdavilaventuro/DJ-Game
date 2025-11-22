using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections; // añadido para coroutine

public class SongManager : MonoBehaviour
{
    public TMP_Dropdown songDropdown;
    public DJTable djTable;
    public RawImage coverImage;
    public Texture fallbackTexture;
    public TextMeshProUGUI trackInfoText;

    private List<string> songPaths = new List<string>();
    private Coroutine coverRoutine; // para cancelar cargas previas

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
            if (coverRoutine != null) StopCoroutine(coverRoutine);
            coverImage.texture = fallbackTexture;
            return;
        }

        string selectedPath = songPaths[index - 1];
        djTable.LoadTrack(selectedPath);
        djTable.Play();

        trackInfoText.text = djTable.GetTrackInfoFromSound();

        if (coverRoutine != null) StopCoroutine(coverRoutine);
        coverRoutine = StartCoroutine(LoadCoverArtWhenReady());
    }

    private IEnumerator LoadCoverArtWhenReady()
    {
        Texture2D tex = null;
        float timeout = 5f; // segundos máximo de espera
        float elapsed = 0f;
        // Intentar hasta que FMOD indique READY y devuelva textura
        while (elapsed < timeout && tex == null)
        {
            tex = djTable.ReadCoverArtFromSound(false); // false: no spam de logs
            if (tex != null) break;
            elapsed += Time.deltaTime;
            yield return null; // esperar siguiente frame
        }
        coverImage.texture = tex != null ? tex : fallbackTexture;
        coverRoutine = null;
    }
}
