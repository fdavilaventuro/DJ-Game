using UnityEngine;
using FMOD;
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using System.Globalization;

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

        replayGainFactor = 1f; // se ajustará luego vía SongManager según JSON

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

    }

    // -----------------------------------------------------------
    //                 REPLAYGAIN: TRACK NORMALIZATION
    // -----------------------------------------------------------
    public void SetReplayGainDb(float gainDb)
    {
        replayGainFactor = Mathf.Pow(10f, gainDb / 20f);
        ApplyFinalVolume();
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
