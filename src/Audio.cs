using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections;
using Il2CppSystem.Linq.Expressions.Interpreter;
using JetBrains.Annotations;
using Polytopia.Data;
using PolytopiaBackendBase.Auth;
using PolytopiaBackendBase.Game;
using SevenZip.Compression.LZMA;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements.UIR;
using System.Reflection;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using Polibrary.Parsing;
using LibCpp2IL;



namespace Polibrary;

public static class PolibAudiomanager
{
    public static ManualLogSource modLogger;
    public static AudioEngineBehaviour Instance;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(PolibAudiomanager));
        modLogger = logger;

        ClassInjector.RegisterTypeInIl2Cpp<AudioEngineBehaviour>();
        GameObject audioGO = new GameObject("AudioEngineGO");
        Instance = audioGO.AddComponent<AudioEngineBehaviour>();
        GameObject.DontDestroyOnLoad(audioGO);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlaySFX))]
    private static void AudioManager_PlaySFX(SFXTypes id, PolytopiaBackendBase.Common.SkinType skinType, float volume, float pitchMod, float pan)
    {
        
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void GameManager_Update()
    {
        if(Input.GetKey(KeyCode.LeftControl))
        {
            if(Input.GetKeyDown(KeyCode.P)) 
            {
                if (!Instance.engine.loopingById.ContainsKey("music"))
                {
                    Instance.engine.PlayLoop("music", Parse.sounds["autotvezet"], 0f, true);
                }
                Instance.engine.ResumeLoop("music");
                Instance.engine.FadeLooped("music", 1f, 0.5f, EaseType.Linear);

                Main.modLogger.LogInfo("resume");
            }
            if(Input.GetKeyDown(KeyCode.O)) 
            {
                Instance.engine.FadeLooped("music", 0f, 0.5f, EaseType.Linear, () =>
                {
                    Instance.engine.PauseLoop("music");
                });
                Main.modLogger.LogInfo("pause");
            }
        }
    }
}


//Huge shoutout to the oceans for taking a blow in water supply for this one!


public class AudioEngineBehaviour : MonoBehaviour
{
    public AudioEngine engine;

    private void Awake()
    {
        engine = new AudioEngine();
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

#region CachedSound

public class CachedSound
{
    public float[] AudioData { get; private set; }
    public WaveFormat WaveFormat { get; private set; }

    public CachedSound(string fileName)
    {
        using var reader = new AudioFileReader(fileName);
        LoadFromProvider(reader);
    }

    public CachedSound(byte[] wavData)
    {
        using var ms = new System.IO.MemoryStream(wavData);
        using var reader = new WaveFileReader(ms);
        LoadFromProvider(reader.ToSampleProvider());
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
                wholeFile.Add(buffer[i]);
        }

        AudioData = wholeFile.ToArray();
    }
}

#endregion

#region CachedSoundSampleProvider

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
        var toCopy = System.Math.Min(available, count);

        System.Array.Copy(sound.AudioData, position, buffer, offset, toCopy);
        position += toCopy;

        return (int)toCopy;
    }

    public void Reset() => position = 0;

    public bool HasEnded => position >= sound.AudioData.Length;
}

#endregion

#region Ease

public enum EaseType
{
    Linear,
    EaseIn,
    EaseOut,
    SmoothStep
}

#endregion

#region SoundInstance

public class SoundInstance
{
    private readonly CachedSoundSampleProvider source;
    private readonly VolumeSampleProvider volumeProvider;
    private readonly MixingSampleProvider mixer;
    private readonly bool loop;

    public bool IsStopped { get; private set; }
    public bool IsPaused { get; private set; }

    public float Volume
    {
        get => volumeProvider.Volume;
        private set => volumeProvider.Volume = System.Math.Clamp(value, 0f, 1f);
    }

    // Fade state
    private bool isFading;
    private float fadeStartVolume;
    private float fadeTargetVolume;
    private float fadeDuration;
    private float fadeElapsed;
    private EaseType fadeEase;
    private System.Action fadeComplete;

    public SoundInstance(CachedSound sound, MixingSampleProvider mixer, float volume, bool loop)
    {
        this.mixer = mixer;
        this.loop = loop;

        source = new CachedSoundSampleProvider(sound);
        var looping = new LoopingSampleProvider(source, loop);

        volumeProvider = new VolumeSampleProvider(looping)
        {
            Volume = volume
        };

        mixer.AddMixerInput(volumeProvider);
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
    }

    public void StartFade(float targetVolume, float durationSeconds, EaseType ease = EaseType.Linear, System.Action onComplete = null)
    {
        if (durationSeconds <= 0f)
        {
            Volume = System.Math.Clamp(targetVolume, 0f, 1f);
            onComplete?.Invoke();
            return;
        }

        fadeStartVolume = Volume;
        fadeTargetVolume = System.Math.Clamp(targetVolume, 0f, 1f);
        fadeDuration = durationSeconds;
        fadeElapsed = 0f;
        fadeEase = ease;
        fadeComplete = onComplete;
        isFading = true;
    }

    public void Update(float deltaTime)
    {
        if (IsPaused) return;  // <-- skip fade and volume updates while paused

        if (isFading)
        {
            fadeElapsed += deltaTime;
            float t = System.Math.Clamp(fadeElapsed / fadeDuration, 0f, 1f);
            t = ApplyEase(t, fadeEase);

            Volume = fadeStartVolume + (fadeTargetVolume - fadeStartVolume) * t;

            if (fadeElapsed >= fadeDuration)
            {
                isFading = false;
                Volume = fadeTargetVolume;
                fadeComplete?.Invoke();
            }
        }
    }

    public void Stop()
    {
        if (IsStopped) return;
        IsStopped = true;
        mixer.RemoveMixerInput(volumeProvider);
    }

    public bool HasFinished => !loop && source.HasEnded;

    private float ApplyEase(float t, EaseType ease)
    {
        return ease switch
        {
            EaseType.EaseIn => t * t,
            EaseType.EaseOut => 1f - (1f - t) * (1f - t),
            EaseType.SmoothStep => t * t * (3f - 2f * t),
            _ => t
        };
    }

    private class LoopingSampleProvider : ISampleProvider
    {
        private readonly CachedSoundSampleProvider source;
        private readonly bool loop;

        public LoopingSampleProvider(CachedSoundSampleProvider source, bool loop)
        {
            this.source = source;
            this.loop = loop;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int written = 0;

            while (written < count)
            {
                int read = source.Read(buffer, offset + written, count - written);

                if (read == 0)
                {
                    if (!loop)
                        break;

                    source.Reset();
                }

                written += read;
            }

            return written;
        }
    }
}

#endregion

#region AudioEngine

public class AudioEngine : System.IDisposable
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

    public IReadOnlyList<SoundInstance> ActiveSounds => activeSounds;

    public float MasterVolume
    {
        get => masterVolume.Volume;
        set => masterVolume.Volume = System.Math.Clamp(value, 0f, 1f);
    }

    public float SfxVolume
    {
        get => sfxVolume.Volume;
        set => sfxVolume.Volume = System.Math.Clamp(value, 0f, 1f);
    }

    public float MusicVolume
    {
        get => musicVolume.Volume;
        set => musicVolume.Volume = System.Math.Clamp(value, 0f, 1f);
    }

    public AudioEngine(int sampleRate = 44100, int channels = 2)
    {
        output = new WaveOutEvent();

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        masterMixer = new MixingSampleProvider(format) { ReadFully = true };
        sfxMixer = new MixingSampleProvider(format) { ReadFully = true };
        musicMixer = new MixingSampleProvider(format) { ReadFully = true };

        sfxVolume = new VolumeSampleProvider(sfxMixer) { Volume = 1f };
        musicVolume = new VolumeSampleProvider(musicMixer) { Volume = 1f };

        masterMixer.AddMixerInput(sfxVolume);
        masterMixer.AddMixerInput(musicVolume);

        masterVolume = new VolumeSampleProvider(masterMixer) { Volume = 1f };

        output.Init(masterVolume);
        output.Play();
    }

    public SoundInstance PlaySfx(CachedSound sound, float volume = 1f, bool loop = false)
    {
        var instance = new SoundInstance(sound, sfxMixer, volume, loop);
        activeSounds.Add(instance);
        return instance;
    }

    public SoundInstance PlayMusic(CachedSound sound, float volume = 1f, bool loop = true)
    {
        var instance = new SoundInstance(sound, musicMixer, volume, loop);
        activeSounds.Add(instance);
        return instance;
    }

    public SoundInstance PlayLoop(string id, CachedSound sound, float volume = 1f, bool music = true)
    {
        StopLoop(id);

        var mixer = music ? musicMixer : sfxMixer;
        var instance = new SoundInstance(sound, mixer, volume, true);

        loopingById[id] = instance;
        activeSounds.Add(instance);

        return instance;
    }

    public void StopLoop(string id)
    {
        if (loopingById.TryGetValue(id, out var instance))
        {
            instance.Stop();
            loopingById.Remove(id);
            activeSounds.Remove(instance);
        }
    }

    public void StopAll()
    {
        foreach (var s in activeSounds.ToList())
            s.Stop();

        activeSounds.Clear();
        loopingById.Clear();
    }

    public void FadeLooped(string id, float targetVolume, float durationSeconds, EaseType ease, System.Action onComplete = null)
    {
        if (loopingById.TryGetValue(id, out var instance))
        {
            instance.StartFade(targetVolume, durationSeconds, ease, onComplete);
        }
    }

    public void PauseLoop(string id)
    {
        if (loopingById.TryGetValue(id, out var instance))
            instance.Pause();
    }

    public void ResumeLoop(string id)
    {
        if (loopingById.TryGetValue(id, out var instance))
            instance.Resume();
    }

    public void Update(float deltaTime)
    {
        foreach (var s in activeSounds)
            s.Update(deltaTime);

        activeSounds.RemoveAll(s =>
        {
            if (s.HasFinished)
            {
                s.Stop();
                return true;
            }
            return false;
        });
    }

    public void Dispose()
    {
        StopAll();
        output.Dispose();
    }
}

#endregion