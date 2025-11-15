using UnityEngine;
using FMOD;
using FMODUnity;

public class DJTable : MonoBehaviour
{
    private FMOD.System system;
    private FMOD.Sound sound;
    private FMOD.Channel channel;
    private FMOD.DSP pitchShifterDSP;
    private FMOD.DSP eqDSP; // <-- EQ DSP

    // Volume
    private float volume = 1f;
    private float volumeBase = 1f;
    private float crossfadeFactor = 1f;

    // Pitch / Keylock
    private float targetPitch = 1f;
    private float currentPitch = 1f;
    private float basePitch = 1f;
    [SerializeField]
    private bool keyLockEnabled = false;
    private const float minPitch = 0.94f;
    private const float maxPitch = 1.06f;

    // EQ & FX
    private float eqHi = 0.5f;
    private float eqMid = 0.5f;
    private float eqLow = 0.5f;
    private float fxAmount = 0f;

    public float VolumeBase => volumeBase;

    // --- Pro Scratch ---
    private bool isScratching = false;
    private uint scratchSamplePosition = 0;
    private float samplesPerDegree = 0f;
    private float platterVelocity = 0f;
    private float friction = 300f;
    private float maxVelocity = 1000f;
    private bool motorOn = true;

    void Awake()
    {
        system = RuntimeManager.CoreSystem;
    }

    void Update()
    {
        system.update();

        // Smooth pitch
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 3f);

        if (!channel.hasHandle()) return;

        // --- Pitch / Keylock ---
        if (keyLockEnabled)
        {
            float semitoneShift = Mathf.Log(currentPitch / basePitch, 2f) * 12f;
            float dspPitch = Mathf.Pow(2f, semitoneShift / 12f);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, dspPitch);
        }
        else
        {
            channel.setPitch(currentPitch);
        }

        // EQ is applied in real-time through DSP, so no need to update each frame unless knobs change
    }

    public void LoadTrack(string filePath)
    {
        if (sound.hasHandle())
            sound.release();

        var result = system.createSound(filePath, MODE.DEFAULT | MODE._2D | MODE.CREATESTREAM, out sound);
        if (result != RESULT.OK)
            UnityEngine.Debug.LogError("FMOD load error: " + result);
    }

    public void Play()
    {
        if (!sound.hasHandle()) return;

        if (channel.hasHandle())
        {
            bool isPlaying;
            bool isPaused;
            channel.isPlaying(out isPlaying);
            channel.getPaused(out isPaused);

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

        channel.setPitch(1f);
        basePitch = 1f;
        targetPitch = 1f;
        currentPitch = 1f;

        SetupDSP();
        SetupEQDSP();

        // Setup scratch
        sound.getLength(out uint totalSamples, FMOD.TIMEUNIT.PCM);
        samplesPerDegree = totalSamples / 360f;
        channel.getPosition(out scratchSamplePosition, FMOD.TIMEUNIT.PCM);
        platterVelocity = 0f;
    }

    void SetupDSP()
    {
        if (!pitchShifterDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.PITCHSHIFT, out pitchShifterDSP);
            pitchShifterDSP.setParameterInt((int)DSP_PITCHSHIFT.FFTSIZE, 8192);
            pitchShifterDSP.setParameterInt((int)DSP_PITCHSHIFT.MAXCHANNELS, 2);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.OVERLAP, 8f);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, 1f);
        }

        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, pitchShifterDSP);
    }

    void SetupEQDSP()
    {
        if (!eqDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.THREE_EQ, out eqDSP);

            // Initialize to default (0 dB)
            eqDSP.setParameterFloat((int)DSP_THREE_EQ.LOWGAIN, 0f);
            eqDSP.setParameterFloat((int)DSP_THREE_EQ.MIDGAIN, 0f);
            eqDSP.setParameterFloat((int)DSP_THREE_EQ.HIGHGAIN, 0f);
        }

        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, eqDSP);
        ApplyEQ(); // apply current knob values
    }

    public void ToggleKeyLock()
    {
        if (!channel.hasHandle() || !pitchShifterDSP.hasHandle()) return;

        keyLockEnabled = !keyLockEnabled;

        if (keyLockEnabled)
        {
            basePitch = currentPitch;
            channel.setPitch(1f);
        }
        else
        {
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, 1f);
            channel.setPitch(currentPitch);
        }
    }

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
        volume = volumeBase * crossfadeFactor;

        if (channel.hasHandle())
            channel.setVolume(volume);
    }

    public void SetPitch(float newPitch)
    {
        currentPitch = Mathf.Clamp(newPitch, minPitch, maxPitch);

        if (!channel.hasHandle()) return;

        if (keyLockEnabled)
        {
            float semitoneShift = Mathf.Log(currentPitch / basePitch, 2f) * 12f;
            float dspPitch = Mathf.Pow(2f, semitoneShift / 12f);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, dspPitch);
        }
        else
        {
            channel.setPitch(currentPitch);
        }
    }

    // --- EQ / FX ---
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

    public void SetFXAmount(float value)
    {
        fxAmount = Mathf.Clamp01(value);
    }

    private void ApplyEQ()
    {
        if (!eqDSP.hasHandle()) return;

        // Map 0..1 -> -12 dB .. +12 dB
        float lowGain = Mathf.Lerp(-12f, 12f, eqLow);
        float midGain = Mathf.Lerp(-12f, 12f, eqMid);
        float highGain = Mathf.Lerp(-12f, 12f, eqHi);

        eqDSP.setParameterFloat((int)DSP_THREE_EQ.LOWGAIN, lowGain);
        eqDSP.setParameterFloat((int)DSP_THREE_EQ.MIDGAIN, midGain);
        eqDSP.setParameterFloat((int)DSP_THREE_EQ.HIGHGAIN, highGain);
    }

    // --- Stop ---
    public void Stop()
    {
        if (channel.hasHandle()) channel.stop();
        if (pitchShifterDSP.hasHandle()) pitchShifterDSP.release();
        if (eqDSP.hasHandle()) eqDSP.release();
        if (sound.hasHandle()) sound.release();
    }

    // --- Pro Scratch ---
    public void StartScratching()
    {
        if (!channel.hasHandle()) return;
        isScratching = true;
        channel.setFrequency(0);
    }

    public void StopScratching()
    {
        if (!channel.hasHandle()) return;
        isScratching = false;
        channel.setFrequency(0);
    }

    public void UpdateProScratch(float deltaAngle, float deltaTime)
    {
        if (!channel.hasHandle()) return;

        sound.getLength(out uint totalSamples, FMOD.TIMEUNIT.PCM);

        if (isScratching)
        {
            platterVelocity = deltaAngle / deltaTime;
            platterVelocity = Mathf.Clamp(platterVelocity, -maxVelocity, maxVelocity);

            int deltaSamples = (int)(deltaAngle * samplesPerDegree);
            scratchSamplePosition = (uint)Mathf.Clamp(scratchSamplePosition + deltaSamples, 0, totalSamples - 1);
            channel.setPosition(scratchSamplePosition, FMOD.TIMEUNIT.PCM);
        }
        else
        {
            if (Mathf.Abs(platterVelocity) > 0.1f)
            {
                float angleStep = platterVelocity * deltaTime;
                int deltaSamples = (int)(angleStep * samplesPerDegree);
                scratchSamplePosition = (uint)Mathf.Clamp(scratchSamplePosition + deltaSamples, 0, totalSamples - 1);
                channel.setPosition(scratchSamplePosition, FMOD.TIMEUNIT.PCM);

                float frictionStep = friction * deltaTime;
                if (platterVelocity > 0f)
                    platterVelocity = Mathf.Max(platterVelocity - frictionStep, 0f);
                else
                    platterVelocity = Mathf.Min(platterVelocity + frictionStep, 0f);
            }
            else if (motorOn)
            {
                float motorSpeed = 360f * 0.5f * deltaTime;
                int deltaSamples = (int)(motorSpeed * samplesPerDegree);
                scratchSamplePosition = (uint)Mathf.Clamp(scratchSamplePosition + deltaSamples, 0, totalSamples - 1);
                channel.setPosition(scratchSamplePosition, FMOD.TIMEUNIT.PCM);
            }
        }
    }
}
