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
    public TextMeshProUGUI trackInfo;
    [Header("Debug")]
    public bool autoLoadTrackOnStart = false; // si true, carga automáticamente un track

    private readonly List<TrackEntry> tracks = new List<TrackEntry>();
    private TrackEntry currentEntry; // track seleccionado actual

    void Start()
    {
        LoadSongs();
        PopulateDropdown();
        coverImage.texture = fallbackTexture;
        trackInfo.text = "No track playing.";

        // Autocargar primer track (index 1 porque 0 es "None Playing")
        if (autoLoadTrackOnStart && songDropdown != null && tracks.Count > 0)
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
            trackInfo.text = "No track playing.";
            currentEntry = null; // limpiar selección
            return;
        }

        var entry = tracks[index - 1];
        currentEntry = entry; // guardar track actual
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

        // Actualizar UI inicial; luego se mantendrá dinámica en Update
        UpdateTrackInfoDynamic();

        // Cargar portada
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

    void Update()
    {
        // Actualizar dinámicamente BPM/Key cuando hay track activo reproduciendo
        if (currentEntry != null && djTable != null && djTable.IsPlaying())
        {
            UpdateTrackInfoDynamic();
        }
    }

    // ---------------- Dinámica BPM / Key ----------------
    private void UpdateTrackInfoDynamic()
    {
        if (currentEntry == null)
        {
            trackInfo.text = "No track playing.";
            return;
        }

        string firstLine = (currentEntry.meta != null && !string.IsNullOrEmpty(currentEntry.meta.title))
            ? currentEntry.meta.title
            : Path.GetFileNameWithoutExtension(currentEntry.audioPath);

        string secondLine = "";
        if (currentEntry.meta != null)
        {
            bool hasArtist = !string.IsNullOrEmpty(currentEntry.meta.artist);
            bool hasAlbum = !string.IsNullOrEmpty(currentEntry.meta.album);
            if (hasArtist && hasAlbum)
                secondLine = currentEntry.meta.artist + " - " + currentEntry.meta.album;
            else if (hasArtist)
                secondLine = currentEntry.meta.artist;
            else if (hasAlbum)
                secondLine = currentEntry.meta.album;
        }

        string thirdLine = "";
        float relPitch = (djTable != null) ? Mathf.Max(0.0001f, djTable.CurrentPitch) : 1f;

        // BPM dinámico
        if (currentEntry.meta != null && currentEntry.meta.bpm > 0)
        {
            int bpmAdjusted = Mathf.Max(1, Mathf.RoundToInt(currentEntry.meta.bpm * relPitch));
            thirdLine += $"BPM: {bpmAdjusted} ";
        }

        // Key dinámica
        string keyStr = currentEntry.meta != null ? currentEntry.meta.initial_key : null;
        string camelotStr = currentEntry.meta != null ? currentEntry.meta.camelot : null;

        string newKey, newCamelot;
        if (TryTransposeKey(keyStr, camelotStr, relPitch, out newKey, out newCamelot))
        {
            if (!string.IsNullOrEmpty(newKey))
                thirdLine += $"Key: {newKey}";
            if (!string.IsNullOrEmpty(newCamelot))
                thirdLine += $" ({newCamelot})";
        }
        else
        {
            if (!string.IsNullOrEmpty(keyStr))
                thirdLine += $"Key: {keyStr}";
            if (!string.IsNullOrEmpty(camelotStr))
                thirdLine += $" ({camelotStr})";
        }

        trackInfo.text = firstLine + "\n" + secondLine + "\n" + thirdLine.Trim();
    }

    // ---------- Utilidades de transposición ----------
    private static readonly string[] Notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static readonly System.Collections.Generic.Dictionary<string, int> NoteToIndex =
        new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "C",0 }, { "C#",1 }, { "Db",1 },
            { "D",2 }, { "D#",3 }, { "Eb",3 },
            { "E",4 }, { "Fb",4 }, { "E#",5 },
            { "F",5 }, { "F#",6 }, { "Gb",6 },
            { "G",7 }, { "G#",8 }, { "Ab",8 },
            { "A",9 }, { "A#",10 }, { "Bb",10 },
            { "B",11 }, { "Cb",11 }
        };

    private static readonly System.Collections.Generic.Dictionary<string, string> MajorToCamelot =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "C","8B" }, { "G","9B" }, { "D","10B" }, { "A","11B" }, { "E","12B" },
            { "B","1B" }, { "F#","2B" }, { "C#","3B" }, { "G#","4B" }, { "Ab","4B" },
            { "D#","5B" }, { "Eb","5B" }, { "A#","6B" }, { "Bb","6B" }, { "F","7B" }
        };

    private static readonly System.Collections.Generic.Dictionary<string, string> MinorToCamelot =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "A","8A" }, { "E","9A" }, { "B","10A" }, { "F#","11A" }, { "C#","12A" },
            { "G#","1A" }, { "Ab","1A" }, { "D#","2A" }, { "Eb","2A" }, { "A#","3A" }, { "Bb","3A" },
            { "F","4A" }, { "C","5A" }, { "G","6A" }, { "D","7A" }
        };

    private static readonly System.Collections.Generic.Dictionary<string, (string note, bool minor)> CamelotToNote =
        new System.Collections.Generic.Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            { "8B",("C",false) }, { "9B",("G",false) }, { "10B",("D",false) }, { "11B",("A",false) }, { "12B",("E",false) },
            { "1B",("B",false) }, { "2B",("F#",false) }, { "3B",("C#",false) }, { "4B",("G#",false) }, { "5B",("D#",false) },
            { "6B",("A#",false) }, { "7B",("F",false) },

            { "8A",("A",true) }, { "9A",("E",true) }, { "10A",("B",true) }, { "11A",("F#",true) }, { "12A",("C#",true) },
            { "1A",("G#",true) }, { "2A",("D#",true) }, { "3A",("A#",true) }, { "4A",("F",true) }, { "5A",("C",true) },
            { "6A",("G",true) }, { "7A",("D",true) }
        };

    private static bool TryTransposeKey(string initialKey, string camelot, float relPitch, out string newKey, out string newCamelot)
    {
        newKey = null;
        newCamelot = null;

        int semitones = Mathf.RoundToInt(12f * Mathf.Log(Mathf.Max(relPitch, 0.0001f), 2f));
        if (semitones == 0)
        {
            newKey = initialKey;
            newCamelot = camelot;
            return !string.IsNullOrEmpty(newKey) || !string.IsNullOrEmpty(newCamelot);
        }

        if (TryParseNoteKey(initialKey, out int idx, out bool isMinor, out string rootNote))
        {
            int newIdx = Mod12(idx + semitones);
            string noteName = Notes[newIdx];
            newKey = isMinor ? (noteName + "m") : noteName;

            if (isMinor)
            {
                if (MinorToCamelot.TryGetValue(noteName, out string camel))
                    newCamelot = camel;
            }
            else
            {
                if (MajorToCamelot.TryGetValue(noteName, out string camel))
                    newCamelot = camel;
            }
            return true;
        }

        if (!string.IsNullOrEmpty(camelot) && CamelotToNote.TryGetValue(camelot.Trim(), out var pair))
        {
            bool isMinor2 = pair.Item2;
            string baseNote = pair.Item1;

            if (NoteToIndex.TryGetValue(baseNote, out int idx2))
            {
                int newIdx = Mod12(idx2 + semitones);
                string noteName = Notes[newIdx];
                newKey = isMinor2 ? (noteName + "m") : noteName;

                if (isMinor2)
                {
                    if (MinorToCamelot.TryGetValue(noteName, out string camel))
                        newCamelot = camel;
                }
                else
                {
                    if (MajorToCamelot.TryGetValue(noteName, out string camel))
                        newCamelot = camel;
                }
                return true;
            }
        }

        return false;
    }

    private static int Mod12(int v) => (v % 12 + 12) % 12;

    private static bool TryParseNoteKey(string key, out int index, out bool isMinor, out string rootOut)
    {
        index = 0; isMinor = false; rootOut = null;
        if (string.IsNullOrWhiteSpace(key)) return false;

        string s = key.Trim();

        s = s.Replace("minor", "m", StringComparison.OrdinalIgnoreCase)
             .Replace("min", "m", StringComparison.OrdinalIgnoreCase)
             .Replace("major", "", StringComparison.OrdinalIgnoreCase)
             .Replace("maj", "", StringComparison.OrdinalIgnoreCase)
             .Replace(" ", "");

        if (s.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            isMinor = true;
            s = s.Substring(0, s.Length - 1);
        }

        if (!NoteToIndex.TryGetValue(s, out index))
            return false;

        rootOut = s.ToUpperInvariant();
        return true;
    }
}
