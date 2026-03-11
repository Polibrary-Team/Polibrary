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
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using PolyMod;

namespace Polibrary;

public static class PolibAudiomanager
{
    public static ManualLogSource modLogger;
    public static AudioEngineBehaviour Behaviour;
    public static MusicData polytopiaMusicData;
    public static void Load(ManualLogSource logger)
    {
        modLogger = logger;

        Harmony.CreateAndPatchAll(typeof(PolibAudiomanager));

        ClassInjector.RegisterTypeInIl2Cpp<AudioEngineBehaviour>();

        var go = new GameObject("PolibAudioEngine");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Behaviour = go.AddComponent<AudioEngineBehaviour>();
    }

    public static Dictionary<string, CachedSound> sounds = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Loader), nameof(Loader.LoadAudioFile))]
    static void LoadAudio(Mod mod, Mod.File file)
    {
        CachedSound sound = new CachedSound(file.bytes);
        sound.name = Path.GetFileNameWithoutExtension(file.name);
        sounds[sound.name] = sound;
        Main.modLogger.LogInfo($"Made {sound.name} a CachedSound");
    }

    
    
    /// <summary>
    /// Play a one-shot sound by ID
    /// </summary>
    public static void PlaySound(string id, float volume = 1f, float pan = 0f)
    {
        if (sounds.TryGetValue(id, out var sound))
        {
            float masterMult = SettingsUtils.Volume; 
            float sfxMult = AudioManager.ShouldPlaySoundEffects() ? 1f : 0f;
            
            Behaviour.engine.PlaySfx(sound, volume, pan);
        }
        else
        {
            modLogger.LogWarning($"Sound ID not found: {id}");
        }
    }

    private static bool TryGetSound(out CachedSound sound, string id, TribeType tribe = TribeType.None, SkinType skinType = SkinType.Default)
    {
        string style;
        if (skinType != SkinType.Default) style = "_" + EnumCache<SkinType>.GetName(skinType);
        else if (tribe != TribeType.None) style = "_" + EnumCache<TribeType>.GetName(tribe);
        else style = "";

        if (!sounds.TryGetValue(id + style, out var value))
        {
            sound = null;
            return false;
        } 

        sound = value;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlaySFX))]
    private static void SfxPatch(SFXTypes id, SkinType skinType = SkinType.Default, float volume = 1, float pitchMod = 1, float pan = 0)
    {
        if (TryGetSound(out var sound, EnumCache<SFXTypes>.GetName(id), TribeType.None, skinType))
        Behaviour.engine.PlaySfx(sound, volume, pan);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.FadeAudioSource))]
    private static void MusicPatch(AudioManager.AudioSourceTypes id, float to, float time = 0.6f, DG.Tweening.Ease easing = DG.Tweening.Ease.Linear, Il2CppSystem.Action onComplete = null)
    {
        if (id != AudioManager.AudioSourceTypes.TribeMusic) return;
        Behaviour.engine.FadeLooped("music", to * Behaviour.engine.MusicVolume, time, EaseType.Linear, () =>
        {
            if (GameManager.Instance.isLevelLoaded && to == 0f)
            {
                Behaviour.engine.SetLoopPause("music", true);
            }
        });
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicData), nameof(MusicData.GetMusicAudioClip))]
    private static void GetMusic(TribeType type, ref AudioClip __result, SkinType skinType = SkinType.Default)
    {
        if (!TryGetSound(out var sound, "music", type, skinType))
        {
            Behaviour.engine.StopLoop("music");
        }
        else
        {
            if (Behaviour.engine.loopingById.TryGetValue("music", out var playingSound) && playingSound.name == sound.name && GameManager.Instance.isLevelLoaded)
            {
                Behaviour.engine.SetLoopPause("music", false);
            }
            else
            {
                Behaviour.engine.PlayLoop("music", sound, 0f, true);
            }
            __result = null;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.VolumeChanged))]
    private static void OnGameVolumeChanged()
    {
        if (Behaviour?.engine == null) return;

        Behaviour.engine.MasterVolume = SettingsUtils.Volume;
        Behaviour.engine.SfxVolume = AudioManager.ShouldPlaySoundEffects() ? 1f : 0f;
        Behaviour.engine.MusicVolume = AudioManager.ShouldPlayTribeMusic() ? 1f : 0f;
    }
}





























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
    public string name;
    public float[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    /// Load from a file path
    /// </summary>
    public CachedSound(string fileName)
    {
        using var reader = new AudioFileReader(fileName);
        var resampler = GetResampledProvider(reader); 
        LoadFromProvider(resampler);
    }

    /// <summary>
    /// Load from raw file bytes. Supports WAV and MP3.
    /// </summary>
    public CachedSound(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        WaveStream reader = null;

        try
        {
            reader = new WaveFileReader(stream);
        }
        catch
        {
            stream.Position = 0;
            try 
            { 
                reader = new Mp3FileReader(stream); 
            }
            catch 
            { 
                throw new InvalidDataException("CachedSound: Bytes are not valid WAV or MP3."); 
            }
        }

        using (reader)
        {
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

        if (output.WaveFormat.SampleRate != 44100)
        {
            output = new WdlResamplingSampleProvider(output, 44100);
        }

        if (output.WaveFormat.Channels == 1)
        {
            output = output.ToStereo();
        }

        return output;
    }

    private void LoadFromProvider(ISampleProvider provider)
    {
        WaveFormat = provider.WaveFormat;
        var wholeFile = new List<float>();
        
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

public class StereoPanProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private float pan;

    public StereoPanProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new System.ArgumentException("StereoPanProvider requires a Stereo input.");
            
        this.source = source;
    }

    public float Pan
    {
        get => pan;
        set => pan = Mathf.Clamp(value, -1f, 1f);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);

        if (pan == 0.0f) return samplesRead;

        float leftMult = (pan <= 0) ? 1f : (1f - pan);
        float rightMult = (pan >= 0) ? 1f : (1f + pan);

        for (int i = 0; i < samplesRead; i += 2)
        {
            buffer[offset + i] *= leftMult;
            buffer[offset + i + 1] *= rightMult;
        }

        return samplesRead;
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
    public string name;
    private readonly CachedSoundSampleProvider _source;
    private readonly ISampleProvider _loopingWrapper;
    private readonly VolumeSampleProvider _volumeProvider;
    private readonly MixingSampleProvider _mixer;
    private readonly StereoPanProvider _panProvider; 
    private readonly bool _loop;
    public bool IsStopped { get; private set; }
    public bool IsPaused { get; private set; }

    private bool _fadeActive;
    private float _fadeDuration;
    private float _fadeElapsed;
    private float _fadeStartVolume;
    private float _fadeTargetVolume;
    private EaseType _fadeEaseType;
    private Action _onFadeComplete; 
    public bool HasFinished => !_loop && _source.HasEnded;

    public float Volume
    {
        get => _currentVolume;
        set
        {
            _currentVolume = Mathf.Clamp01(value);
            
            
            if (_fadeActive)
            {
                _fadeActive = false;
                _onFadeComplete = null; 
            }
            
            UpdateProviderVolume();
        }
    }
    private float _currentVolume;

    public float Pan
    {
        get => _panProvider.Pan;
        set => _panProvider.Pan = Mathf.Clamp(value, -1f, 1f);
    }

    public SoundInstance(CachedSound sound, MixingSampleProvider mixer, float startVolume, float pan = 0f, bool loop = false)
    {
        name = sound.name;
        _mixer = mixer;
        _loop = loop;
        _currentVolume = startVolume;

        _source = new CachedSoundSampleProvider(sound);

        if (loop)
            _loopingWrapper = new LoopingSampleProvider(_source);
        else
            _loopingWrapper = _source;

        _volumeProvider = new VolumeSampleProvider(_loopingWrapper)
        {
            Volume = startVolume
        };

        _panProvider = new StereoPanProvider(_volumeProvider)
        {
            Pan = Mathf.Clamp(pan, -1f, 1f)
        };

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

        _onFadeComplete = null;
        
        _mixer.RemoveMixerInput(_panProvider);
    }

    public void StartFade(float targetVolume, float durationSeconds, EaseType ease = EaseType.Linear, Action onComplete = null)
    {
        _fadeStartVolume = _currentVolume;
        _fadeTargetVolume = Mathf.Clamp01(targetVolume);
        _fadeDuration = Mathf.Max(durationSeconds, 0.001f);
        _fadeEaseType = ease;
        _fadeElapsed = 0f;

        _onFadeComplete = onComplete;
        
        _fadeActive = true;
    }

    public void Update(float deltaTime)
    {
        if (IsStopped) return;
        if (IsPaused) return; 

        if (_fadeActive)
        {
            _fadeElapsed += deltaTime;
            float t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);

            float easedT = _fadeEaseType switch
            {
                EaseType.EaseIn => t * t,
                EaseType.EaseOut => t * (2f - t),
                EaseType.SmoothStep => t * t * (3f - 2f * t),
                _ => t
            };

            _currentVolume = Mathf.Lerp(_fadeStartVolume, _fadeTargetVolume, easedT);

            if (_fadeElapsed >= _fadeDuration)
            {
                _fadeActive = false;
                _currentVolume = _fadeTargetVolume;
                _onFadeComplete?.Invoke();
                _onFadeComplete = null;

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
        _volumeProvider.Volume = IsPaused ? 0f : _currentVolume;
    }

    private class LoopingSampleProvider : ISampleProvider
    {
        private readonly CachedSoundSampleProvider _source;
        public LoopingSampleProvider(CachedSoundSampleProvider source) => _source = source;
        public WaveFormat WaveFormat => _source.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = _source.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0) _source.Reset();
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

    private readonly WaveFormat engineFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public float MasterVolume { get => masterVolume.Volume; set => masterVolume.Volume = value; }
    public float SfxVolume { get => sfxVolume.Volume; set => sfxVolume.Volume = value; }
    public float MusicVolume { get => musicVolume.Volume; set => musicVolume.Volume = value; }

    public AudioEngine()
    {
        output = new WaveOutEvent();
        
        masterMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };
        sfxMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };
        musicMixer = new MixingSampleProvider(engineFormat) { ReadFully = true };

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

    public void SetLoopPause(string id, bool paused)
    {
        if (loopingById.TryGetValue(id, out var instance))
        {
            if (paused) instance.Pause();
            else instance.Resume();
        }
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
        if (loopingById.TryGetValue(id, out var instance)) instance.StartFade(target, time, ease, onComplete);
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
    EaseIn,
    EaseOut,
    SmoothStep
}