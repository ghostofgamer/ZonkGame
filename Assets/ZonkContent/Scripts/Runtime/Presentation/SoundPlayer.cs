using System.Collections.Generic;
using UnityEngine;
using Zenject;
using Zonk.Progress;

namespace Zonk.Presentation
{
    /// <summary>Звуки игры по имени.</summary>
    public enum Sfx
    {
        Click,
        DiceClack,
        DiceRattle,
        DiceKeep,
        Zonk,
        Bank,
        HotDice,
        Win,
        Lose,
        Coin,
        Thud,
        Whoosh,
        Thunder,
    }

    /// <summary>
    /// Проигрывает звуки. Пока звуковых файлов нет, клипы синтезируются в коде (шум, тоны):
    /// звук сразу есть и ничего не весит в сборке. Настоящие клипы подставляются полями Overrides.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Override
        {
            public Sfx Sound;
            public AudioClip Clip;
        }

        [SerializeField] private AudioSource _source;
        [SerializeField] private List<Override> _overrides = new List<Override>();

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private IGameSettings _settings;

        [Inject]
        public void Construct(IGameSettings settings)
        {
            _settings = settings;
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioSource source)
        {
            _source = source;
        }
#endif

        public void Play(Sfx sound, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (_source == null || (_settings != null && !_settings.Sound))
                return;

            var clip = Get(sound);
            if (clip == null)
                return;

            _source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            _source.PlayOneShot(clip, volume);
        }

        private AudioClip Get(Sfx sound)
        {
            if (_clips.TryGetValue(sound, out var cached))
                return cached;

            AudioClip clip = null;
            foreach (var item in _overrides)
            {
                if (item.Sound == sound && item.Clip != null)
                    clip = item.Clip;
            }

            if (clip == null)
                clip = ProceduralSounds.Create(sound);

            _clips[sound] = clip;
            return clip;
        }
    }

    /// <summary>Синтез простых звуков. Заглушки до настоящих звуков, но уже дают отклик.</summary>
    public static class ProceduralSounds
    {
        private const int Rate = 22050;

        public static AudioClip Create(Sfx sound)
        {
            switch (sound)
            {
                case Sfx.Click: return Tone("click", 0.05f, 900f, 0.25f, 60f);
                case Sfx.DiceClack: return Noise("clack", 0.06f, 0.6f, 70f, 1);
                case Sfx.DiceRattle: return Noise("rattle", 0.7f, 0.35f, 8f, 14);
                case Sfx.DiceKeep: return Noise("keep", 0.05f, 0.4f, 90f, 1);
                case Sfx.Zonk: return Sweep("zonk", 0.6f, 220f, 70f, 0.6f);
                case Sfx.Bank: return Chord("bank", 0.35f, new[] { 523f, 659f }, 0.35f);
                case Sfx.HotDice: return Chord("hot", 0.5f, new[] { 523f, 659f, 784f, 1046f }, 0.3f);
                case Sfx.Win: return Chord("win", 0.9f, new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.3f);
                case Sfx.Lose: return Sweep("lose", 0.8f, 330f, 150f, 0.4f);
                case Sfx.Coin: return Chord("coin", 0.25f, new[] { 988f, 1318f }, 0.3f);
                case Sfx.Thud: return Sweep("thud", 0.3f, 120f, 40f, 0.8f);
                case Sfx.Whoosh: return Whoosh("whoosh", 0.28f, 0.45f);
                case Sfx.Thunder: return Rumble("thunder", 2.4f, 0.8f);
                default: return null;
            }
        }

        private static AudioClip Noise(string name, float duration, float volume, float decay, int bursts)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            for (var b = 0; b < bursts; b++)
            {
                var start = bursts == 1 ? 0 : (int)(samples.Length * (b / (float)bursts + (float)random.NextDouble() * 0.04f));
                for (var i = start; i < samples.Length; i++)
                {
                    var t = (i - start) / (float)Rate;
                    var envelope = Mathf.Exp(-t * decay * (bursts > 1 ? 6f : 1f));
                    if (envelope < 0.01f)
                        break;
                    samples[i] += ((float)random.NextDouble() * 2f - 1f) * envelope * volume;
                }
            }

            return Build(name, samples);
        }

        /// <summary>Гром: «коричневый» шум (низкий гул) с быстрым нарастанием, раскатами и долгим затуханием.</summary>
        private static AudioClip Rumble(string name, float duration, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            var value = 0f;
            var peak = 0.0001f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)Rate;
                value = Mathf.Clamp(value + ((float)random.NextDouble() * 2f - 1f) * 0.05f, -1f, 1f) * 0.995f;
                var attack = Mathf.Clamp01(t / 0.06f);
                var roll = 0.75f + 0.25f * Mathf.Sin(t * 9f + Mathf.Sin(t * 2.3f) * 3f);
                samples[i] = value * attack * roll * Mathf.Exp(-t * 1.6f);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            for (var i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / peak * volume;
            return Build(name, samples);
        }

        /// <summary>«Вжух»: шум через простой фильтр, громкость нарастает и спадает, фильтр открывается к середине.</summary>
        private static AudioClip Whoosh(string name, float duration, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(name.GetHashCode());
            var filtered = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                var envelope = Mathf.Sin(t * Mathf.PI);
                var cutoff = Mathf.Lerp(0.02f, 0.25f, envelope);
                filtered += (((float)random.NextDouble() * 2f - 1f) - filtered) * cutoff;
                samples[i] = filtered * envelope * volume * 2f;
            }

            return Build(name, samples);
        }

        private static AudioClip Tone(string name, float duration, float frequency, float volume, float decay)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)Rate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * decay) * volume;
            }

            return Build(name, samples);
        }

        private static AudioClip Sweep(string name, float duration, float from, float to, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var phase = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, t) / Rate;
                samples[i] = Mathf.Sin(phase) * (1f - t) * volume;
            }

            return Build(name, samples);
        }

        private static AudioClip Chord(string name, float duration, float[] notes, float volume)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var step = samples.Length / (notes.Length + 1);
            for (var n = 0; n < notes.Length; n++)
            {
                for (var i = n * step; i < samples.Length; i++)
                {
                    var t = (i - n * step) / (float)Rate;
                    samples[i] += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * Mathf.Exp(-t * 5f) * volume / notes.Length * 2f;
                }
            }

            return Build(name, samples);
        }

        private static AudioClip Build(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
