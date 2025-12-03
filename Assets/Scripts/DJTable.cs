using UnityEngine;
using FMOD;
using FMODUnity;
using System;

public class DJTable : MonoBehaviour
{
    private FMOD.System system;
    private FMOD.Sound sound;
    private FMOD.Channel channel;

    private FMOD.DSP eqDSP;

    // -------- REAL DJ FILTER (using MULTIBAND_EQ) --------
    private FMOD.DSP lpfDSP;   // LPF side
    private FMOD.DSP hpfDSP;   // HPF side

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

    // FILTER FX knob
    private float fxAmount = 0.0f;

    // Scratch
    private bool isScratching = false;
    private float lastVinylAngle = 0f;
    private float accumulatedSeek = 0f;
    private uint lastPlaybackPos = 0;
    private float scratchSensitivity = 15f;

    // Cue
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
    }

    // -----------------------------------------------------------
    //                     REPLAYGAIN
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
        channel.getPaused(out bool isPaused);
        channel.getVolume(out float vol);

        if (isPaused || vol < 0.01f)
            return false;

        channel.getAudibility(out float audible);
        if (audible < 0.01f)
            return false;

        return true;
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
        SetupFilterDSPs();     // ---- FILTER DSPs ----
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

    // ===========================================================
    //            REAL DJ FILTER (LPF on left / HPF on right)
    // ===========================================================

    void SetupFilterDSPs()
    {
        // LPF
        if (!lpfDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.MULTIBAND_EQ, out lpfDSP);
            lpfDSP.setParameterInt((int)DSP_MULTIBAND_EQ.A_FILTER,
                (int)DSP_MULTIBAND_EQ_FILTER_TYPE.LOWPASS_12DB);
            lpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_FREQUENCY, 22000f);
            lpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_Q, 0.7f);
            lpfDSP.setBypass(true);
        }

        // HPF
        if (!hpfDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.MULTIBAND_EQ, out hpfDSP);
            hpfDSP.setParameterInt((int)DSP_MULTIBAND_EQ.A_FILTER,
                (int)DSP_MULTIBAND_EQ_FILTER_TYPE.HIGHPASS_12DB);
            hpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_FREQUENCY, 20f);
            hpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_Q, 0.7f);
            hpfDSP.setBypass(true);
        }

        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, lpfDSP);
        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, hpfDSP);

        ApplyFilter();
    }

    public void SetFXAmount(float value)
    {
        fxAmount = Mathf.Clamp01(value);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (!lpfDSP.hasHandle() || !hpfDSP.hasHandle())
            return;

        float v = fxAmount;  // Knob value 0 → 1

        const float minFreq = 80f;        // lowest cutoff frequency
        const float maxFreqLPF = 22000f;  // LPF fully open
        const float maxFreqHPF = 4000f;   // HPF fully open

        if (Mathf.Approximately(v, 0.5f))
        {
            // Center: bypass both filters
            lpfDSP.setBypass(true);
            hpfDSP.setBypass(true);
        }
        else if (v < 0.5f)
        {
            // LEFT side = LPF sweep
            float t = v / 0.5f;                     // 0 → 1 as knob goes left → center
            float cutoff = Mathf.Lerp(minFreq, maxFreqLPF, t * t);  // quadratic curve

            lpfDSP.setBypass(false);
            lpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_FREQUENCY, cutoff);

            hpfDSP.setBypass(true);
        }
        else
        {
            // RIGHT side = HPF sweep
            float t = (v - 0.5f) / 0.5f;           // 0 → 1 as knob goes center → right
            float cutoff = Mathf.Lerp(minFreq, maxFreqHPF, t * t);  // quadratic curve

            hpfDSP.setBypass(false);
            hpfDSP.setParameterFloat((int)DSP_MULTIBAND_EQ.A_FREQUENCY, cutoff);

            lpfDSP.setBypass(true);
        }
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

    // Expone el pitch actual (suavizado) para la UI
    public float CurrentPitch => currentPitch;

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
        if (lpfDSP.hasHandle()) lpfDSP.release();
        if (hpfDSP.hasHandle()) hpfDSP.release();
        if (sound.hasHandle()) sound.release();

        replayGainFactor = 1f;
    }

    // -----------------------------------------------------------
    //                     SCRATCHING
    // -----------------------------------------------------------

    public void BeginScratch(float initialAngle)
    {
        isScratching = true;
        lastVinylAngle = initialAngle;

        if (channel.hasHandle())
            channel.getPosition(out lastPlaybackPos, TIMEUNIT.MS);

        channel.setPitch(1f);
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
    //                          CUE
    // -----------------------------------------------------------

    public float GetTrackPositionMS()
    {
        if (!channel.hasHandle()) return 0f;

        channel.getPosition(out uint ms, TIMEUNIT.MS);
        return ms;
    }

    public void SetTrackPositionMS(float ms)
    {
        if (!channel.hasHandle()) return;
        channel.setPosition((uint)ms, TIMEUNIT.MS);
    }

    public void CuePress()
    {
        if (isScratching) return;

        channel.getPaused(out bool isPaused);
        channel.isPlaying(out bool isPlaying);

        if (!isPlaying || isPaused)
        {
            channel.getPosition(out uint pos, TIMEUNIT.MS);
            cuePointMS = pos;
        }

        cueHeld = true;

        SetTrackPositionMS(cuePointMS);
        channel.setPaused(false);
        channel.setPitch(1f);
    }

    public void CueRelease()
    {
        cueHeld = false;

        if (!channel.hasHandle()) return;

        channel.setPaused(true);
        SetTrackPositionMS(cuePointMS);
    }
}
