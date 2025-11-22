using UnityEngine;
using FMOD;
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using System.Globalization;
using System.IO; // añadido para MemoryStream

public class DJTable : MonoBehaviour
{
    private FMOD.System system;
    private FMOD.Sound sound;
    private FMOD.Channel channel;
    private FMOD.DSP eqDSP;

    // Volume
    private float volume = 1f;
    private float volumeBase = 1f;
    private float crossfadeFactor = 1f;
    private float replayGainFactor = 1f;

    // Pitch
    private float targetPitch = 1f;
    private float currentPitch = 1f;
    private const float minPitch = 0.94f;
    private const float maxPitch = 1.06f;

    // EQ
    private float eqHi = 0.5f;
    private float eqMid = 0.5f;
    private float eqLow = 0.5f;

    // Scratch
    private bool isScratching = false;
    private float lastVinylAngle = 0f;
    private float accumulatedSeek = 0f;
    private uint lastPlaybackPos = 0;
    private float scratchSensitivity = 15f;

    // --- CUE ---
    private float cuePointMS = 0f;
    private bool cueHeld = false;

    public float VolumeBase => volumeBase;

    void Awake()
    {
        system = RuntimeManager.CoreSystem;
    }

    void Update()
    {
        system.update();

        // If scratching or holding cue → pitch = 1 always
        if (!channel.hasHandle() || isScratching || cueHeld)
            return;

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 3f);
        channel.setPitch(currentPitch);
    }

    // -----------------------------------------------------------
    //                        TRACK LOAD
    // -----------------------------------------------------------

    public void LoadTrack(string filePath)
    {
        if (sound.hasHandle())
            sound.release();

        replayGainFactor = 1f;

        var result = system.createSound(
            filePath,
            MODE.DEFAULT | MODE._2D | MODE.CREATESTREAM | MODE.NONBLOCKING,
            out sound
        );

        if (result != RESULT.OK)
        {
            UnityEngine.Debug.LogError("FMOD load error: " + result);
            return;
        }

        ReadReplayGainFromSound();
    }

    // -----------------------------------------------------------
    //                 COVER ART: READ IMAGE FROM TAGS
    // -----------------------------------------------------------
    public Texture2D ReadCoverArtFromSound(bool logIfMissing = true)
    {
        if (!sound.hasHandle()) return null;

        // Asegurar que el sound terminó de abrirse si se usó NONBLOCKING
        sound.getOpenState(out OPENSTATE openState, out uint _, out bool _, out bool _);
        if (openState != OPENSTATE.READY)
        {
            // Todavía no disponible
            if (logIfMissing)
                UnityEngine.Debug.Log("Sound aún cargando (openState=" + openState + ")");
            return null;
        }

        sound.getNumTags(out int numTags, out int _);
        for (int i = 0; i < numTags; i++)
        {
            var result = sound.getTag(null, i, out TAG tag);
            if (result != RESULT.OK) continue;
            if (tag.data == IntPtr.Zero) continue;

            string tagName = tag.name;
            if (string.IsNullOrEmpty(tagName)) continue;
            string tagNameUpper = tagName.ToUpperInvariant();

            // No filtramos estrictamente por tipo porque algunas builds pueden devolver otro tipo distinto
            if (tagNameUpper != "METADATA_BLOCK_PICTURE" && tag.type == TAGTYPE.VORBISCOMMENT && tagNameUpper != "METADATA_BLOCK_PICTURE")
                continue;

            if (tagNameUpper != "METADATA_BLOCK_PICTURE")
                continue;

            string base64 = Marshal.PtrToStringAnsi(tag.data);
            if (string.IsNullOrEmpty(base64)) continue;
            base64 = base64.Trim();
            try
            {
                byte[] raw = Convert.FromBase64String(base64);
                byte[] imageBytes = ExtractImageBytesFromFlacPicture(raw);
                if (imageBytes == null || imageBytes.Length == 0) continue;

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(imageBytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    return tex;
                }
                UnityEngine.Debug.LogWarning("No se pudo cargar la imagen embebida (LoadImage falló). Index tag: " + i);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("Error parseando METADATA_BLOCK_PICTURE (index " + i + "): " + ex.Message);
            }
        }

        if (logIfMissing)
        {
            UnityEngine.Debug.LogWarning("No se encontró METADATA_BLOCK_PICTURE. Dump de tags:");
            DebugDumpTags();
        }
        return null; // Sin arte
    }

    private void DebugDumpTags()
    {
        if (!sound.hasHandle()) return;
        sound.getNumTags(out int numTags, out int _);
        for (int i = 0; i < numTags; i++)
        {
            var result = sound.getTag(null, i, out TAG tag);
            if (result != RESULT.OK) continue;
            string name = tag.name != null ? tag.name.ToString() : "<null>"; // corrección operador ??
            string value = "";
            if (tag.data != IntPtr.Zero)
            {
                try { value = Marshal.PtrToStringAnsi(tag.data); } catch { value = "<bin>"; }
            }
            UnityEngine.Debug.Log($"[TAG {i}] type={tag.type} name={name} valueHead={(value.Length > 60 ? value.Substring(0, 60) + "..." : value)}");
        }
    }

    private byte[] ExtractImageBytesFromFlacPicture(byte[] raw)
    {
        // Formato FLAC Picture (todos los enteros 32-bit big-endian):
        // [PictureType][MimeLen][Mime][DescLen][Desc][Width][Height][Depth][Colors][DataLen][Data]
        try
        {
            using (var ms = new MemoryStream(raw))
            using (var br = new BinaryReader(ms))
            {
                int ReadInt32BE()
                {
                    byte[] b = br.ReadBytes(4);
                    if (b.Length < 4) throw new EndOfStreamException();
                    return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
                }

                int picType = ReadInt32BE(); // ignorado
                int mimeLen = ReadInt32BE();
                string mime = System.Text.Encoding.ASCII.GetString(br.ReadBytes(mimeLen));
                int descLen = ReadInt32BE();
                br.ReadBytes(descLen); // desc ignorada
                int width = ReadInt32BE();
                int height = ReadInt32BE();
                int depth = ReadInt32BE(); // ignorado
                int colors = ReadInt32BE(); // ignorado
                int dataLen = ReadInt32BE();
                byte[] data = br.ReadBytes(dataLen);

                if (data.Length != dataLen)
                    throw new Exception("Tamaño de datos incoherente");

                // Validar que sea JPEG/PNG
                if (!string.IsNullOrEmpty(mime))
                {
                    string m = mime.ToLowerInvariant();
                    if (!(m.Contains("jpeg") || m.Contains("jpg") || m.Contains("png")))
                        UnityEngine.Debug.Log("MIME inesperado en picture: " + mime);
                }

                // Opcional: verificar dimensiones
                if (width <= 0 || height <= 0)
                    UnityEngine.Debug.LogWarning("Dimensiones inválidas en METADATA_BLOCK_PICTURE");

                return data;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("ExtractImageBytesFromFlacPicture fallo: " + e.Message);
            return null;
        }
    }

    // -----------------------------------------------------------
    //                 REPLAYGAIN: TRACK NORMALIZATION
    // -----------------------------------------------------------

    private void ReadReplayGainFromSound()
    {
        replayGainFactor = 1f;

        if (!sound.hasHandle())
            return;

        sound.getNumTags(out int numTags, out int _);

        for (int i = 0; i < numTags; i++)
        {
            var result = sound.getTag(null, i, out TAG tag);
            if (result != RESULT.OK) continue;
            if (tag.data == IntPtr.Zero) continue;
            if (tag.type != TAGTYPE.VORBISCOMMENT) continue;

            string tagName = tag.name;
            if (string.IsNullOrEmpty(tagName)) continue;

            if (!tagName.Equals("REPLAYGAIN_TRACK_GAIN", StringComparison.OrdinalIgnoreCase))
                continue;

            string value = Marshal.PtrToStringAnsi(tag.data);
            if (string.IsNullOrEmpty(value)) continue;

            string sanitized = value.Trim();
            int dbIndex = sanitized.IndexOf("dB", StringComparison.OrdinalIgnoreCase);
            if (dbIndex >= 0)
                sanitized = sanitized.Substring(0, dbIndex).Trim();

            if (!float.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out float gainDb))
                continue;

            replayGainFactor = Mathf.Pow(10f, gainDb / 20f);
            UnityEngine.Debug.Log($"ReplayGain track gain: {gainDb} dB -> factor {replayGainFactor}");
            break;
        }
    }

    // -----------------------------------------------------------
    //                 METADATA: TITLE & ARTIST
    // -----------------------------------------------------------

    // Devuelve "Artista - Título" leído desde los tags Vorbis del archivo OGG.
    // Si no encuentra metadata útil, devuelve null.
    public string GetTrackDisplayNameFromFile(string filePath)
    {
        FMOD.Sound tempSound;
        var result = system.createSound(
            filePath,
            MODE.DEFAULT | MODE._2D | MODE.CREATESTREAM,
            out tempSound
        );

        if (result != RESULT.OK || !tempSound.hasHandle())
        {
            UnityEngine.Debug.LogWarning($"FMOD tag read error for {filePath}: {result}");
            return null;
        }

        string title = null;
        string artist = null;

        tempSound.getNumTags(out int numTags, out int _);

        for (int i = 0; i < numTags; i++)
        {
            result = tempSound.getTag(null, i, out TAG tag);
            if (result != RESULT.OK) continue;
            if (tag.data == IntPtr.Zero) continue;

            string tagName = tag.name;
            if (string.IsNullOrEmpty(tagName)) continue;

            string tagNameUpper = tagName.ToUpperInvariant();

            // Solo nos interesan tags de texto de comentarios Vorbis
            if (tag.type != TAGTYPE.VORBISCOMMENT)
                continue;

            string value = Marshal.PtrToStringAnsi(tag.data);
            if (string.IsNullOrEmpty(value)) continue;

            if (title == null && tagNameUpper == "TITLE")
            {
                title = value;
            }
            else if (artist == null && tagNameUpper == "ARTIST")
            {
                artist = value;
            }
        }

        tempSound.release();

        if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
            return $"{title} - {artist}";
        if (!string.IsNullOrEmpty(title))
            return title;

        return null;
    }

    public string GetTrackInfoFromSound()
    {
        if (!sound.hasHandle()) return null;
        // Ensure sound finished opening (NONBLOCKING case)
        sound.getOpenState(out OPENSTATE openState, out uint _, out bool _, out bool _);
        if (openState != OPENSTATE.READY)
            return null; // Not ready yet

        string title = null;
        string album = null;
        string artist = null;

        sound.getNumTags(out int numTags, out int _);
        for (int i = 0; i < numTags; i++)
        {
            var result = sound.getTag(null, i, out TAG tag);
            if (result != RESULT.OK) continue;
            if (tag.data == IntPtr.Zero) continue;
            if (tag.type != TAGTYPE.VORBISCOMMENT) continue;

            string tagName = tag.name;
            if (string.IsNullOrEmpty(tagName)) continue;
            string tagNameUpper = tagName.ToUpperInvariant();

            string value = Marshal.PtrToStringAnsi(tag.data);
            if (string.IsNullOrEmpty(value)) continue;

            if (title == null && tagNameUpper == "TITLE")
                title = value;
            else if (album == null && tagNameUpper == "ALBUM")
                album = value;
            else if (artist == null && tagNameUpper == "ARTIST")
                artist = value;
        }

        if (title == null && album == null && artist == null)
            return null; // No useful metadata

        // New format: "track name \n album name - artist name"
        // Build first line (track name)
        string firstLine = title ?? "";

        // Build second line (album name - artist name)
        string secondLine = "";
        if (!string.IsNullOrEmpty(album))
            secondLine = album;
        if (!string.IsNullOrEmpty(artist))
            secondLine = string.IsNullOrEmpty(secondLine) ? artist : (secondLine + " - " + artist);

        // If we have a title
        if (!string.IsNullOrEmpty(firstLine))
        {
            // If we also have second line -> combine; else just first line
            return string.IsNullOrEmpty(secondLine) ? firstLine : (firstLine + "\n" + secondLine);
        }

        // No title: fall back to second line only (album - artist or artist)
        return string.IsNullOrEmpty(secondLine) ? null : secondLine;
    }

    // -----------------------------------------------------------
    //                          PLAY
    // -----------------------------------------------------------

    public bool IsPlaying()
    {
        if (!channel.hasHandle()) return false;

        channel.isPlaying(out bool isPlaying);
        return isPlaying;
    }

    public void Play()
    {
        if (!sound.hasHandle()) return;

        if (channel.hasHandle())
        {
            channel.isPlaying(out bool isPlaying);
            channel.getPaused(out bool isPaused);

            if (isPlaying && !isPaused)
            {
                channel.setPaused(true);
                return;
            }
            else if (isPaused)
            {
                channel.setPaused(false);
                return;
            }
        }

        ChannelGroup group = default;
        system.playSound(sound, group, false, out channel);

        ApplyFinalVolume();

        targetPitch = currentPitch = 1f;

        SetupEQDSP();
    }

    // -----------------------------------------------------------
    //                       EQ DSP SETUP
    // -----------------------------------------------------------

    void SetupEQDSP()
    {
        if (!eqDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.THREE_EQ, out eqDSP);

            eqDSP.setParameterFloat((int)DSP_THREE_EQ.LOWGAIN, 0f);
            eqDSP.setParameterFloat((int)DSP_THREE_EQ.MIDGAIN, 0f);
            eqDSP.setParameterFloat((int)DSP_THREE_EQ.HIGHGAIN, 0f);
        }

        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, eqDSP);
        ApplyEQ();
    }

    // -----------------------------------------------------------
    //                      VOLUME & PITCH
    // -----------------------------------------------------------

    public void SetBaseVolume(float newVolume)
    {
        volumeBase = Mathf.Clamp01(newVolume);
        ApplyFinalVolume();
    }

    public void SetCrossfadeFactor(float factor)
    {
        crossfadeFactor = Mathf.Clamp01(factor);
        ApplyFinalVolume();
    }

    public void SetVolume(float newVolume)
    {
        volumeBase = Mathf.Clamp01(newVolume);
        crossfadeFactor = 1f;
        ApplyFinalVolume();
    }

    private void ApplyFinalVolume()
    {
        volume = volumeBase * crossfadeFactor * replayGainFactor;
        if (channel.hasHandle())
            channel.setVolume(volume);
    }

    public void SetPitch(float newPitch)
    {
        targetPitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
    }

    // -----------------------------------------------------------
    //                          EQ
    // -----------------------------------------------------------

    public void SetEQHi(float value)
    {
        eqHi = Mathf.Clamp01(value);
        ApplyEQ();
    }

    public void SetEQMid(float value)
    {
        eqMid = Mathf.Clamp01(value);
        ApplyEQ();
    }

    public void SetEQLow(float value)
    {
        eqLow = Mathf.Clamp01(value);
        ApplyEQ();
    }

    private void ApplyEQ()
    {
        if (!eqDSP.hasHandle()) return;

        eqDSP.setParameterFloat((int)DSP_THREE_EQ.LOWGAIN, Mathf.Lerp(-12f, 12f, eqLow));
        eqDSP.setParameterFloat((int)DSP_THREE_EQ.MIDGAIN, Mathf.Lerp(-12f, 12f, eqMid));
        eqDSP.setParameterFloat((int)DSP_THREE_EQ.HIGHGAIN, Mathf.Lerp(-12f, 12f, eqHi));
    }

    // -----------------------------------------------------------
    //                          STOP
    // -----------------------------------------------------------

    public void Stop()
    {
        if (channel.hasHandle()) channel.stop();
        if (eqDSP.hasHandle()) eqDSP.release();
        if (sound.hasHandle()) sound.release();
        replayGainFactor = 1f;
    }

    // -----------------------------------------------------------
    //                     SCRATCHING LOGIC
    // -----------------------------------------------------------

    public void BeginScratch(float initialAngle)
    {
        isScratching = true;
        lastVinylAngle = initialAngle;

        if (channel.hasHandle())
            channel.getPosition(out lastPlaybackPos, TIMEUNIT.MS);

        channel.setPitch(1f); // Lock pitch
    }

    public void ScratchUpdate(float vinylAngleDegrees, float deltaTime)
    {
        if (!isScratching || !channel.hasHandle())
            return;

        float delta = Mathf.DeltaAngle(lastVinylAngle, vinylAngleDegrees);
        lastVinylAngle = vinylAngleDegrees;

        float msOffset = delta * scratchSensitivity;
        accumulatedSeek += msOffset;

        uint trackLen = GetTrackLengthMs();
        int newPos = Mathf.Clamp((int)(lastPlaybackPos + accumulatedSeek), 0, (int)trackLen);

        channel.setPosition((uint)newPos, TIMEUNIT.MS);
    }

    public void EndScratch()
    {
        if (!channel.hasHandle())
        {
            isScratching = false;
            return;
        }

        lastPlaybackPos = (uint)Mathf.Clamp(
            lastPlaybackPos + accumulatedSeek,
            0,
            (int)GetTrackLengthMs()
        );

        channel.setPosition(lastPlaybackPos, TIMEUNIT.MS);

        accumulatedSeek = 0f;
        isScratching = false;
    }

    private uint GetTrackLengthMs()
    {
        if (!sound.hasHandle()) return 0;
        sound.getLength(out uint len, TIMEUNIT.MS);
        return len;
    }

    // -----------------------------------------------------------
    //                          CUE SYSTEM
    // -----------------------------------------------------------

    public float GetTrackPositionMS()
    {
        if (!channel.hasHandle()) return 0f;

        channel.getPosition(out uint ms, FMOD.TIMEUNIT.MS);
        return ms;
    }

    public void SetTrackPositionMS(float ms)
    {
        if (!channel.hasHandle()) return;
        channel.setPosition((uint)ms, FMOD.TIMEUNIT.MS);
    }

    // --- CUE PRESS (BUTTON DOWN) ---
    public void CuePress()
    {
        if (isScratching) return;

        channel.getPaused(out bool isPaused);
        channel.isPlaying(out bool isPlaying);

        // --- CASE 1: TRACK DETENIDO (paused or not started) ---
        if (!isPlaying || isPaused)
        {
            // Siempre obtenemos la posición REAL antes de guardar cue
            uint pos;
            channel.getPosition(out pos, FMOD.TIMEUNIT.MS);

            cuePointMS = pos;  // <-- ESTE ES TU NUEVO CUE REAL
            UnityEngine.Debug.Log("Nuevo punto CUE guardado en: " + cuePointMS + "ms");
        }

        // --- PLAY FROM CUE WHILE HELD ---
        cueHeld = true;

        SetTrackPositionMS(cuePointMS);
        channel.setPaused(false);
        channel.setPitch(1f);
    }


    // --- CUE RELEASE (BUTTON UP) ---
    public void CueRelease()
    {
        cueHeld = false;

        if (!channel.hasHandle()) return;

        // Stop & return to cue
        channel.setPaused(true);
        SetTrackPositionMS(cuePointMS);
    }
}
