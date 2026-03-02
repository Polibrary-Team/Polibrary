using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Polibrary.Parsing;
using Polytopia.Data; // For SettingsUtils
using UnityEngine;

namespace Polibrary;

public static class PolibAudiomanager
{
    public static ManualLogSource ModLogger;
    //public static AudioEngineBehaviour Behaviour;
    public static void Load(ManualLogSource logger)
    {
        ModLogger = logger;
        Harmony.CreateAndPatchAll(typeof(PolibAudiomanager));
        
        //ClassInjector.RegisterTypeInIl2Cpp<AudioEngineBehaviour>();

        //var go = new GameObject("PolibAudioEngine");
        //UnityEngine.Object.DontDestroyOnLoad(go);
        //Behaviour = go.AddComponent<AudioEngineBehaviour>();
        
        ModLogger.LogInfo("Polib Audio Engine Loaded.");
    }

    
    /*
    /// <summary>
    /// Play a one-shot sound by ID
    /// </summary>
    public static void PlaySound(string id, float volume = 1f, float pan = 0f)
    {
        if (Parse.sounds.TryGetValue(id, out var sound))
        {
            // Calculate effective volume based on Game Settings
            float masterMult = SettingsUtils.Volume; 
            float sfxMult = AudioManager.ShouldPlaySoundEffects() ? 1f : 0f;
            
            // Note: We apply the multiplier here or let the Engine handle Master/Group volumes.
            // letting the engine handle it is cleaner.
            
            Behaviour.engine.PlaySfx(sound, volume, pan);
        }
        else
        {
            ModLogger.LogWarning($"Sound ID not found: {id}");
        }
    }
    
    // --- Harmony Patches to Sync with Game State ---

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.VolumeChanged))]
    private static void OnGameVolumeChanged()
    {
        if (Behaviour?.engine == null) return;

        // Sync NAudio volumes with Polytopia Settings
        Behaviour.engine.MasterVolume = SettingsUtils.Volume;
        Behaviour.engine.SfxVolume = AudioManager.ShouldPlaySoundEffects() ? 1f : 0f;
        Behaviour.engine.MusicVolume = AudioManager.ShouldPlayTribeMusic() ? 1f : 0f; // Simplified logic
    }*/
}




























/*
public class AudioEngineBehaviour : MonoBehaviour
{
    public AudioEngine engine;

    private void Awake()
    {
        engine = new AudioEngine();
        // Sync initial volumes
        engine.MasterVolume = SettingsUtils.Volume;
        engine.SfxVolume = AudioManager.ShouldPlaySoundEffects() ? 1f : 0f;
        engine.MusicVolume = AudioManager.ShouldPlayTribeMusic() ? 1f : 0f;
    }

    private void Update()
    {
        engine.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        engine.Dispose();
    }
}

#region NAudio Wrappers

public class CachedSound
{
    public float[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    /// Load from a file path (WAV, MP3, AIFF, etc provided by AudioFileReader)
    /// </summary>
    public CachedSound(string fileName)
    {
        using var reader = new AudioFileReader(fileName);
        // Force 44.1kHz Stereo for compatibility
        var resampler = GetResampledProvider(reader); 
        LoadFromProvider(resampler);
    }

    /// <summary>
    /// Load from raw file bytes (MemoryStream). Supports WAV and MP3.
    /// </summary>
    public CachedSound(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        WaveStream reader = null;

        try
        {
            // 1. Try generic Wave (RIFF headers)
            reader = new WaveFileReader(stream);
        }
        catch
        {
            // 2. If Wave fails, reset and try MP3
            stream.Position = 0;
            try 
            { 
                reader = new Mp3FileReader(stream); 
            }
            catch 
            { 
                // 3. Fallback / Error
                throw new InvalidDataException("CachedSound: Bytes are not valid WAV or MP3."); 
            }
        }

        using (reader)
        {
            // Convert to sample provider and force 44.1kHz Stereo
            var resampler = GetResampledProvider(reader.ToSampleProvider());
            LoadFromProvider(resampler);
        }
    }

    /// <summary>
    /// Helper to ensure all sounds enter the engine as 44.1kHz Stereo
    /// </summary>
    private ISampleProvider GetResampledProvider(ISampleProvider input)
    {
        ISampleProvider output = input;

        // 1. Resample to 44100 if needed
        if (output.WaveFormat.SampleRate != 44100)
        {
            output = new WdlResamplingSampleProvider(output, 44100);
        }

        // 2. Convert to Stereo if needed
        if (output.WaveFormat.Channels == 1)
        {
            output = output.ToStereo();
        }
        // Note: NAudio doesn't have a built-in "ToStereo" for >2 channels, 
        // but WdlResampling usually keeps channels intact or we assume source is Mono/Stereo.

        return output;
    }

    private void LoadFromProvider(ISampleProvider provider)
    {
        WaveFormat = provider.WaveFormat;
        var wholeFile = new List<float>();
        
        // Create a buffer (1 second worth of data at a time)
        var buffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                wholeFile.Add(buffer[i]);
            }
        }

        AudioData = wholeFile.ToArray();
    }
}

public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound sound;
    private long position;

    public CachedSoundSampleProvider(CachedSound sound)
    {
        this.sound = sound;
    }

    public WaveFormat WaveFormat => sound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var available = sound.AudioData.Length - position;
        var toCopy = Math.Min(available, count);
        Array.Copy(sound.AudioData, position, buffer, offset, toCopy);
        position += toCopy;
        return (int)toCopy;
    }

    public void Reset() => position = 0;
    public bool HasEnded => position >= sound.AudioData.Length;
}


public class SoundInstance
{
    // NAudio Providers
    private readonly CachedSoundSampleProvider _source;
    private readonly ISampleProvider _loopingWrapper;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly PanningSampleProvider _panProvider;
    private readonly MixingSampleProvider _mixer;

    // State
    private readonly bool _loop;
    public bool IsStopped { get; private set; }
    public bool IsPaused { get; private set; }
    
    // Fading State
    private bool _fadeActive;
    private float _fadeDuration;
    private float _fadeElapsed;
    private float _fadeStartVolume;
    private float _fadeTargetVolume;
    private EaseType _fadeEaseType;

    // Properties
    public bool HasFinished => !_loop && _source.HasEnded;

    /// <summary>
    /// Current volume (0.0 to 1.0). Setting this cancels any active fade.
    /// </summary>
    public float Volume
    {
        get => _currentVolume;
        set
        {
            _currentVolume = Mathf.Clamp01(value);
            _fadeActive = false; // Manual change kills fade
            UpdateProviderVolume();
        }
    }
    private float _currentVolume;

    /// <summary>
    /// Panning (-1.0 Left to 1.0 Right).
    /// </summary>
    public float Pan
    {
        get => _panProvider.Pan;
        set => _panProvider.Pan = Mathf.Clamp(value, -1f, 1f);
    }

    public SoundInstance(CachedSound sound, MixingSampleProvider mixer, float startVolume, float pan = 0f, bool loop = false)
    {
        _mixer = mixer;
        _loop = loop;
        _currentVolume = startVolume;

        // 1. Source
        _source = new CachedSoundSampleProvider(sound);

        // 2. Loop Handling
        if (loop)
            _loopingWrapper = new LoopingSampleProvider(_source);
        else
            _loopingWrapper = _source;

        // 3. Volume Control
        _volumeProvider = new VolumeSampleProvider(_loopingWrapper)
        {
            Volume = startVolume
        };

        // 4. Panning Control
        _panProvider = new PanningSampleProvider(_volumeProvider)
        {
            Pan = Mathf.Clamp(pan, -1f, 1f)
        };

        // 5. Connect to Mixer
        _mixer.AddMixerInput(_panProvider);
    }

    public void Pause()
    {
        IsPaused = true;
        UpdateProviderVolume();
    }

    public void Resume()
    {
        IsPaused = false;
        UpdateProviderVolume();
    }

    public void Stop()
    {
        if (IsStopped) return;
        IsStopped = true;
        
        // Remove from mixer to stop processing and free resources
        _mixer.RemoveMixerInput(_panProvider);
    }

    /// <summary>
    /// Starts fading the volume over time.
    /// </summary>
    public void StartFade(float targetVolume, float durationSeconds, EaseType ease = EaseType.Linear)
    {
        _fadeStartVolume = _currentVolume;
        _fadeTargetVolume = Mathf.Clamp01(targetVolume);
        _fadeDuration = Mathf.Max(durationSeconds, 0.001f); // Avoid divide by zero
        _fadeEaseType = ease;
        _fadeElapsed = 0f;
        _fadeActive = true;
    }

    /// <summary>
    /// Call this every frame (e.g., from MonoBehaviour.Update)
    /// </summary>
    public void Update(float deltaTime)
    {
        if (IsStopped) return;

        // Don't progress fades if paused
        if (IsPaused) return; 

        if (_fadeActive)
        {
            _fadeElapsed += deltaTime;
            float t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);

            // Apply Easing Math
            float easedT = _fadeEaseType switch
            {
                EaseType.EaseIn => t * t,
                EaseType.EaseOut => t * (2f - t),
                EaseType.SmoothStep => t * t * (3f - 2f * t),
                _ => t // Linear
            };

            _currentVolume = Mathf.Lerp(_fadeStartVolume, _fadeTargetVolume, easedT);

            // Fade Complete
            if (_fadeElapsed >= _fadeDuration)
            {
                _fadeActive = false;
                _currentVolume = _fadeTargetVolume;

                // Auto-stop if faded to silence and not looping (standard game behavior)
                if (Mathf.Approximately(_fadeTargetVolume, 0f) && !_loop)
                {
                    Stop();
                }
            }

            UpdateProviderVolume();
        }
    }

    private void UpdateProviderVolume()
    {
        // If paused, mute the provider, otherwise use current volume
        _volumeProvider.Volume = IsPaused ? 0f : _currentVolume;
    }

    // --- Internal Loop Helper ---
    private class LoopingSampleProvider : ISampleProvider
    {
        private readonly CachedSoundSampleProvider _source;

        public LoopingSampleProvider(CachedSoundSampleProvider source)
        {
            _source = source;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = _source.Read(buffer, offset + totalBytesRead, count - totalBytesRead);

                if (bytesRead == 0)
                {
                    // End of source, reset to beginning
                    _source.Reset();
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}

public class AudioEngine : IDisposable
{
    private readonly IWavePlayer output;
    private readonly MixingSampleProvider masterMixer;
    private readonly MixingSampleProvider sfxMixer;
    private readonly MixingSampleProvider musicMixer;

    private readonly VolumeSampleProvider masterVolume;
    private readonly VolumeSampleProvider sfxVolume;
    private readonly VolumeSampleProvider musicVolume;

    private readonly List<SoundInstance> activeSounds = new();
    public Dictionary<string, SoundInstance> loopingById = new();

    // Standard format: 44.1kHz Stereo
    private readonly WaveFormat engineFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public float MasterVolume { get => masterVolume.Volume; set => masterVolume.Volume = value; }
    public float SfxVolume { get => sfxVolume.Volume; set => sfxVolume.Volume = value; }
    public float MusicVolume { get => musicVolume.Volume; set => musicVolume.Volume = value; }

    public AudioEngine()
    {
        output = new WaveOutEvent(); // Or WasapiOut if you prefer low latency on Windows
        
        // Mixers
        masterMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };
        sfxMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };
        musicMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };

        // Groups
        sfxVolume = new VolumeSampleProvider(sfxMixer);
        musicVolume = new VolumeSampleProvider(musicMixer);
        
        masterMixer.AddMixerInput(sfxVolume);
        masterMixer.AddMixerInput(musicVolume);

        masterVolume = new VolumeSampleProvider(masterMixer);

        output.Init(masterVolume);
        output.Play();
    }

    public SoundInstance PlaySfx(CachedSound sound, float volume = 1f, float pan = 0f)
    {
        var instance = new SoundInstance(sound, sfxMixer, volume, pan, false);
        lock(activeSounds) activeSounds.Add(instance);
        return instance;
    }

    public SoundInstance PlayLoop(string id, CachedSound sound, float volume = 1f, bool isMusic = true)
    {
        StopLoop(id);
        var mixer = isMusic ? musicMixer : sfxMixer;
        var instance = new SoundInstance(sound, mixer, volume, 0f, true);
        
        loopingById[id] = instance;
        lock(activeSounds) activeSounds.Add(instance);
        return instance;
    }

    public void StopLoop(string id)
    {
        if (loopingById.TryGetValue(id, out var instance))
        {
            instance.Stop();
            loopingById.Remove(id);
        }
    }
    
    public void FadeLooped(string id, float target, float time, EaseType ease = EaseType.Linear, Action onComplete = null)
    {
        if (loopingById.TryGetValue(id, out var instance)) instance.StartFade(target, time);
    }

    public void Update(float deltaTime)
    {
        lock (activeSounds)
        {
            for (int i = activeSounds.Count - 1; i >= 0; i--)
            {
                var s = activeSounds[i];
                s.Update(deltaTime);
                if (s.HasFinished || s.IsStopped)
                {
                    s.Stop();
                    activeSounds.RemoveAt(i);
                }
            }
        }
    }

    public void Dispose()
    {
        output?.Stop();
        output?.Dispose();
    }
}

#endregion

public enum EaseType
{
    Linear,
    EaseIn,     // Quadratic In
    EaseOut,    // Quadratic Out
    SmoothStep  // Hermite
}*/