using UnityEngine;
using FMOD;
using FMODUnity;

public class DJTable : MonoBehaviour
{
    private FMOD.System system;
    private FMOD.Sound sound;
    private FMOD.Channel channel;
    private FMOD.DSP pitchShifterDSP;

    private float volume = 1f;
    private float targetPitch = 1f;
    private float currentPitch = 1f;
    private float basePitch = 1f;
    [SerializeField]
    private bool keyLockEnabled = false;

    private const float minPitch = 0.94f;
    private const float maxPitch = 1.06f;
    private const float pitchSmoothSpeed = 4f;

    void Awake()
    {
        system = RuntimeManager.CoreSystem;
    }

    void Update()
    {
        system.update();

        // Smooth target pitch (for slider smoothing)
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 3f);

        if (!channel.hasHandle()) return;

        if (keyLockEnabled)
        {
            // Key Lock ON — only use DSP
            float semitoneShift = Mathf.Log(currentPitch / basePitch, 2f) * 12f;
            float dspPitch = Mathf.Pow(2f, semitoneShift / 12f);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, dspPitch);
        }
        else
        {
            // Normal mode — adjust tempo directly, bypass DSP
            channel.setPitch(currentPitch);
        }
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

        // Toggle pause
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

        // Start playback
        ChannelGroup group = default;
        system.playSound(sound, group, false, out channel);
        channel.setVolume(volume);
        channel.setPitch(1f);
        basePitch = 1f;
        targetPitch = 1f;
        currentPitch = 1f;

        SetupDSP();
    }

    void SetupDSP()
    {
        if (!pitchShifterDSP.hasHandle())
        {
            system.createDSPByType(DSP_TYPE.PITCHSHIFT, out pitchShifterDSP);

            // Try a high-quality configuration
            pitchShifterDSP.setParameterInt((int)DSP_PITCHSHIFT.FFTSIZE, 8192);
            pitchShifterDSP.setParameterInt((int)DSP_PITCHSHIFT.MAXCHANNELS, 2);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.OVERLAP, 8f); // smoother
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, 1f);
        }

        channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, pitchShifterDSP);
    }


    public void ToggleKeyLock()
    {
        if (!channel.hasHandle() || !pitchShifterDSP.hasHandle()) return;

        keyLockEnabled = !keyLockEnabled;

        if (keyLockEnabled)
        {
            basePitch = currentPitch;
            channel.setPitch(1f); // keep tempo
        }
        else
        {
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, 1f);
            channel.setPitch(currentPitch);
        }
    }


    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (channel.hasHandle())
            channel.setVolume(volume);
    }

    public void SetPitch(float newPitch)
    {
        currentPitch = Mathf.Clamp(newPitch, 0.94f, 1.06f);

        if (!channel.hasHandle()) return;

        if (keyLockEnabled)
        {
            // Compute ratio relative to base pitch
            float semitoneShift = Mathf.Log(currentPitch / basePitch, 2f) * 12f;
            float dspPitch = Mathf.Pow(2f, semitoneShift / 12f);
            pitchShifterDSP.setParameterFloat((int)DSP_PITCHSHIFT.PITCH, dspPitch);
        }
        else
        {
            channel.setPitch(currentPitch);
        }
    }

    public void Stop()
    {
        if (channel.hasHandle()) channel.stop();
        if (pitchShifterDSP.hasHandle()) pitchShifterDSP.release();
        if (sound.hasHandle()) sound.release();
    }
}
