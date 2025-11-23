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
        public string artist; // nuevo campo
        public string album;  // nuevo campo
    }

    private class TrackEntry
    {
        public string audioPath;
        public TrackMetadata meta;
        public string coverPath;
    }

    public DJTable djTable;
    [Header("UI Elements")]
    public TMP_Dropdown songDropdown;
    public RawImage coverImage;
    public Texture fallbackTexture;
    [Header("Track Info")]
    public TextMeshProUGUI trackInfoText;
    [Header("Debug")]
    public bool autoLoadFirstOnStart = false; // si true, carga automáticamente el primer track

    private List<TrackEntry> tracks = new List<TrackEntry>();

    void Start()
    {
        LoadSongs();
        PopulateDropdown();
        coverImage.texture = fallbackTexture;
        trackInfoText.text = "No track playing.";

        // Autocargar primer track (index 1 porque 0 es "None Playing")
        if (autoLoadFirstOnStart && songDropdown != null && tracks.Count > 0)
        {
            // Elegir un track aleatorio (dropdown index 0 es "None Playing")
            int randomTrack = UnityEngine.Random.Range(0, tracks.Count); // 0..tracks.Count-1
            songDropdown.value = randomTrack + 1; // dispara OnValueChanged
            // OnSongSelected ya llama Play(), así que no repetimos.
            djTable.Play();
        }
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
            if (t.meta != null && !string.IsNullOrEmpty(t.meta.artist))
            {
                displayName += " - " + t.meta.artist;
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
            string firstLine = !string.IsNullOrEmpty(entry.meta.title)
                ? entry.meta.title
                : Path.GetFileNameWithoutExtension(entry.audioPath);

            string secondLine = "";
            bool hasArtist = !string.IsNullOrEmpty(entry.meta.artist);
            bool hasAlbum = !string.IsNullOrEmpty(entry.meta.album);
            if (hasArtist && hasAlbum)
                secondLine = entry.meta.artist + " - " + entry.meta.album;
            else if (hasArtist)
                secondLine = entry.meta.artist;
            else if (hasAlbum)
                secondLine = entry.meta.album;
            else
                secondLine = "";

            string thirdLine = "";
            bool hasBpm = entry.meta.bpm > 0;
            bool hasKey = !string.IsNullOrEmpty(entry.meta.initial_key);
            bool hasCamelot = !string.IsNullOrEmpty(entry.meta.camelot);
            if (hasBpm)
                thirdLine += $"BPM: {entry.meta.bpm} ";
            if (hasKey)
                thirdLine += $"Key: {entry.meta.initial_key}";
            if (hasCamelot)
                thirdLine += $"({entry.meta.camelot})";

            trackInfoText.text = firstLine + "\n" + secondLine + "\n" + thirdLine;
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
                coverImage.texture = tex.LoadImage(bytes) ? tex : fallbackTexture;
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
