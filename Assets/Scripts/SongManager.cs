using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class SongManager : MonoBehaviour
{
    [Serializable]
    private class TrackMetadata
    {
        public string title;
        public string id;
        public string filename;
        public int bpm;
        public string initial_key;
        public string camelot;
        public string replaygain_track_gain;
        public string replaygain_track_peak;
        public string cover;
    }

    private class TrackEntry
    {
        public string audioPath;
        public TrackMetadata meta;
        public string coverPath;
    }

    public TMP_Dropdown songDropdown;
    public DJTable djTable;
    public RawImage coverImage;
    public Texture fallbackTexture;
    public TextMeshProUGUI trackInfoText;

    private List<TrackEntry> tracks = new List<TrackEntry>();

    void Start()
    {
        LoadSongs();
        PopulateDropdown();
        coverImage.texture = fallbackTexture;
        trackInfoText.text = "No track playing.";
    }

    void LoadSongs()
    {
        tracks.Clear();
        string folderPath = Path.Combine(Application.streamingAssetsPath, "Music");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Music folder not found at {folderPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath, "*.ogg"))
        {
            var entry = new TrackEntry
            {
                audioPath = file
            };

            string stem = Path.GetFileNameWithoutExtension(file);
            string jsonPath = Path.Combine(folderPath, stem + ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    entry.meta = JsonUtility.FromJson<TrackMetadata>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to parse metadata {jsonPath}: {e.Message}");
                }
            }

            string coverFile = stem + ".png";
            string coverAbs = Path.Combine(folderPath, coverFile);
            if (File.Exists(coverAbs))
            {
                entry.coverPath = coverAbs;
            }

            tracks.Add(entry);
        }

        Debug.Log($"Loaded {tracks.Count} songs.");
    }

    void PopulateDropdown()
    {
        if (songDropdown == null) return;
        songDropdown.ClearOptions();
        var songNames = new List<string> { "None Playing" };
        foreach (var t in tracks)
        {
            string displayName = t.meta != null && !string.IsNullOrEmpty(t.meta.title)
                ? t.meta.title
                : Path.GetFileNameWithoutExtension(t.audioPath);
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
            coverImage.texture = fallbackTexture;
            trackInfoText.text = "No track playing.";
            return;
        }

        var entry = tracks[index - 1];
        djTable.LoadTrack(entry.audioPath);
        // ReplayGain desde JSON
        if (entry.meta != null && !string.IsNullOrEmpty(entry.meta.replaygain_track_gain))
        {
            float gainDb;
            string g = entry.meta.replaygain_track_gain.Trim();
            int dbIndex = g.IndexOf("dB", StringComparison.OrdinalIgnoreCase);
            if (dbIndex >= 0) g = g.Substring(0, dbIndex).Trim();
            if (float.TryParse(g, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out gainDb))
            {
                djTable.SetReplayGainDb(gainDb);
            }
        }
        djTable.Play();

        // Construir info para UI usando metadata
        if (entry.meta != null)
        {
            trackInfoText.text = $"{entry.meta.title}\nBPM: {entry.meta.bpm}  Key: {entry.meta.initial_key} ({entry.meta.camelot})";
        }
        else
        {
            trackInfoText.text = Path.GetFileNameWithoutExtension(entry.audioPath);
        }

        // Cargar portada desde archivo .png
        if (!string.IsNullOrEmpty(entry.coverPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(entry.coverPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    coverImage.texture = tex;
                }
                else
                {
                    coverImage.texture = fallbackTexture;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load cover {entry.coverPath}: {e.Message}");
                coverImage.texture = fallbackTexture;
            }
        }
        else
        {
            coverImage.texture = fallbackTexture;
        }
    }
}
