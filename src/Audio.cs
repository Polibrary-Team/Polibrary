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
    private static readonly string DATA_PATH = Path.Combine(PolyMod.Plugin.BASE_PATH, "PolibraryData");
    public static AudioEngine engine = new();
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(PolibAudiomanager));
        modLogger = logger;
        string filePath = Path.Combine(DATA_PATH, "AudioTest.wav");
        Parse.sounds.Add("basic", new CachedSound(filePath));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlaySFX))]
    private static void Thing(SFXTypes id, PolytopiaBackendBase.Common.SkinType skinType, float volume, float pitchMod, float pan)
    {
        //engine.PlaySfx(Parse.sounds.GetOrDefault("basic"), volume);
    }
}


//Huge shoutout to the oceans for taking a blow in water supply for this one!


#region Cached Sound

public class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    public CachedSound(string fileName)
    {
        using var reader = new AudioFileReader(fileName);
        WaveFormat = reader.WaveFormat;

        var wholeFile = new List<float>();
        var readBuffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];

        int samplesRead;
        while ((samplesRead = reader.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
                wholeFile.Add(readBuffer[i]);
        }

        AudioData = wholeFile.ToArray();
    }
}

#endregion

#region CachedSoundSampleProvider

public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound cachedSound;
    private long position;

    public CachedSoundSampleProvider(CachedSound cachedSound)
    {
        this.cachedSound = cachedSound;
    }

    public WaveFormat WaveFormat => cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = cachedSound.AudioData.Length - position;
        var samplesToCopy = System.Math.Min(availableSamples, count);

        System.Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
        position += samplesToCopy;

        return (int)samplesToCopy;
    }

    public void Reset() => position = 0;
}

#endregion

#region SoundInstance (Fully Controllable)

public class SoundInstance
{
    private readonly CachedSoundSampleProvider source;
    private readonly VolumeSampleProvider volumeProvider;
    private readonly MixingSampleProvider mixer;
    private readonly bool loop;
    private bool stopped;

    public float Volume
    {
        get => volumeProvider.Volume;
        set => volumeProvider.Volume = System.Math.Clamp(value, 0f, 1f);
    }

    public SoundInstance(CachedSound sound, MixingSampleProvider mixer, float volume, bool loop)
    {
        this.mixer = mixer;
        this.loop = loop;

        source = new CachedSoundSampleProvider(sound);

        var loopingProvider = new LoopingSampleProvider(source, loop);
        volumeProvider = new VolumeSampleProvider(loopingProvider)
        {
            Volume = volume
        };

        mixer.AddMixerInput(volumeProvider);
    }

    public void Stop()
    {
        stopped = true;
        Volume = 0;
    }

    public void Pause() => Volume = 0;
    public void Resume(float volume = 1f) => Volume = volume;

    public async Task FadeTo(float targetVolume, int milliseconds)
    {
        float startVolume = Volume;
        int steps = 20;
        int delay = milliseconds / steps;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Volume = startVolume + (targetVolume - startVolume) * t;
            await Task.Delay(delay);
        }

        Volume = targetVolume;
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
            int totalSamplesWritten = 0;

            while (totalSamplesWritten < count)
            {
                int samplesRead = source.Read(
                    buffer,
                    offset + totalSamplesWritten,
                    count - totalSamplesWritten);

                if (samplesRead == 0)
                {
                    if (!loop)
                        break;

                    source.Reset();
                }

                totalSamplesWritten += samplesRead;
            }

            return totalSamplesWritten;
        }
    }
}

#endregion

#region Audio Engine

public class AudioEngine : System.IDisposable
{
    private readonly IWavePlayer outputDevice;
    private readonly MixingSampleProvider masterMixer;
    private readonly MixingSampleProvider sfxMixer;
    private readonly MixingSampleProvider musicMixer;

    private readonly VolumeSampleProvider masterVolume;
    private readonly VolumeSampleProvider sfxVolume;
    private readonly VolumeSampleProvider musicVolume;

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
        outputDevice = new WaveOutEvent();

        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

        masterMixer = new MixingSampleProvider(format) { ReadFully = true };
        sfxMixer = new MixingSampleProvider(format) { ReadFully = true };
        musicMixer = new MixingSampleProvider(format) { ReadFully = true };

        sfxVolume = new VolumeSampleProvider(sfxMixer) { Volume = 1f };
        musicVolume = new VolumeSampleProvider(musicMixer) { Volume = 1f };

        masterMixer.AddMixerInput(sfxVolume);
        masterMixer.AddMixerInput(musicVolume);

        masterVolume = new VolumeSampleProvider(masterMixer) { Volume = 1f };

        outputDevice.Init(masterVolume);
        outputDevice.Play();
    }

    public SoundInstance PlaySfx(CachedSound sound, float volume = 1f, bool loop = false)
        => new SoundInstance(sound, sfxMixer, volume, loop);

    public SoundInstance PlayMusic(CachedSound sound, float volume = 1f, bool loop = true)
        => new SoundInstance(sound, musicMixer, volume, loop);

    public void Dispose() => outputDevice.Dispose();
}

#endregion

#region Example Usage

/*
var engine = new AudioEngine();

var sounds = new Dictionary<string, CachedSound>
{
    ["click"] = new CachedSound("click.wav"),
    ["music"] = new CachedSound("music.wav")
};

var click = engine.PlaySfx(sounds["click"], 0.7f);
var music = engine.PlayMusic(sounds["music"], 0.5f, true);

// Change volume live
music.Volume = 0.2f;

// Fade out music over 2 seconds
await music.FadeTo(0f, 2000);

// Stop music completely
music.Stop();

// Global controls
engine.MasterVolume = 0.8f;
engine.SfxVolume = 1f;
engine.MusicVolume = 0.6f;
*/

#endregion