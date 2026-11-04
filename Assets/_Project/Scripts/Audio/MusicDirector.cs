using System.Collections;
using Subnet.Core;
using UnityEngine;

namespace Subnet.Audio
{
    /// <summary>
    /// Persistent music system (survives scene loads).
    /// - City track: never stops while you're in the city. During fights it keeps playing at volume 0
    ///   behind a closing low-pass filter, so when the fight ends it resumes exactly where it "would" be.
    /// - Fight track: fades in with an opening filter sweep; ends with a "tape stop" (pitch dive + muffle).
    /// - All levels follow SYSTEM CONFIG → MUSIC volume.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        private static MusicDirector instance;

        public static MusicDirector Instance => instance;

        [SerializeField] private float cityLevel = 0.55f;
        [SerializeField] private float fightLevel = 0.75f;

        private AudioSource city;
        private AudioSource fight;
        private AudioLowPassFilter cityFilter;
        private AudioLowPassFilter fightFilter;

        private float cityGain;   // 0..1 envelope applied on top of cityLevel
        private float fightGain;
        private Coroutine cityRoutine;
        private Coroutine fightRoutine;

        /// <summary>Default fight track (set by SceneMusic).</summary>
        public AudioClip FightTrack { get; set; }

        public bool InFight { get; private set; }

        /// <summary>Returns the director, creating it on first use.</summary>
        public static MusicDirector Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("[MusicDirector]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicDirector>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            city = CreateChannel("City", out cityFilter);
            fight = CreateChannel("Fight", out fightFilter);
            AudioBootstrap.AudioRestarted += OnAudioRestarted;
        }

        private void OnDestroy()
        {
            AudioBootstrap.AudioRestarted -= OnAudioRestarted;
            if (instance == this) instance = null;
        }

        private AudioSource CreateChannel(string name, out AudioLowPassFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            filter = go.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            return source;
        }

        private void Update()
        {
            float music = GameSettings.MusicVolume;
            city.volume = cityLevel * cityGain * music;
            fight.volume = fightLevel * fightGain * music;
        }

        // ================================================================== City

        /// <summary>Starts (or keeps) the city track. Same clip already playing = nothing changes.</summary>
        public void PlayCity(AudioClip clip, float fadeSeconds = 2.5f)
        {
            if (clip == null) return;
            if (city.clip == clip && city.isPlaying)
            {
                if (!InFight) FadeCity(1f, fadeSeconds, 22000f);
                return;
            }
            city.clip = clip;
            city.time = 0f;
            city.Play();
            cityGain = 0f;
            cityFilter.cutoffFrequency = 22000f;
            if (!InFight) FadeCity(1f, fadeSeconds, 22000f);
        }

        /// <summary>Fades the city track out and stops it.</summary>
        public void StopCity(float fadeSeconds = 2f)
        {
            if (!city.isPlaying) return;
            if (cityRoutine != null) StopCoroutine(cityRoutine);
            cityRoutine = StartCoroutine(FadeChannel(city, cityFilter, () => cityGain, v => cityGain = v, 0f, fadeSeconds, 22000f, true));
        }

        private void FadeCity(float target, float seconds, float cutoff)
        {
            if (cityRoutine != null) StopCoroutine(cityRoutine);
            cityRoutine = StartCoroutine(FadeChannel(city, cityFilter, () => cityGain, v => cityGain = v, target, seconds, cutoff, false));
        }

        // ================================================================== Fight

        /// <summary>City muffles to silence (but keeps running), fight track sweeps in.</summary>
        public void EnterFight(AudioClip clip = null)
        {
            AudioClip track = clip != null ? clip : FightTrack;
            InFight = true;

            if (city.isPlaying) FadeCity(0f, 0.7f, 450f);

            if (track == null) return;
            if (fightRoutine != null) StopCoroutine(fightRoutine);
            fight.clip = track;
            fight.time = 0f;
            fight.pitch = 1f;
            fightFilter.cutoffFrequency = 700f;
            fightGain = 0f;
            fight.Play();
            fightRoutine = StartCoroutine(FadeChannel(fight, fightFilter, () => fightGain, v => fightGain = v, 1f, 1.1f, 22000f, false, 0.25f));
        }

        /// <summary>Fight track "tape-stops" out, city track opens back up in sync.</summary>
        public void ExitFight()
        {
            InFight = false;
            if (fightRoutine != null) StopCoroutine(fightRoutine);
            if (fight.isPlaying) fightRoutine = StartCoroutine(TapeStop());

            if (city.clip != null && city.isPlaying)
            {
                if (cityRoutine != null) StopCoroutine(cityRoutine);
                cityRoutine = StartCoroutine(DelayedCityReturn(0.35f));
            }
        }

        private IEnumerator DelayedCityReturn(float delay)
        {
            for (float t = 0f; t < delay; t += GameTime.UnscaledDelta) yield return null;
            yield return FadeChannel(city, cityFilter, () => cityGain, v => cityGain = v, 1f, 2.2f, 22000f, false);
        }

        private IEnumerator TapeStop()
        {
            const float duration = 1.0f;
            float startGain = fightGain;
            float startCutoff = fightFilter.cutoffFrequency;
            for (float t = 0f; t < duration; t += GameTime.UnscaledDelta)
            {
                float n = t / duration;
                fight.pitch = Mathf.Lerp(1f, 0.15f, n * n);
                fightGain = Mathf.Lerp(startGain, 0f, n);
                fightFilter.cutoffFrequency = Mathf.Lerp(startCutoff, 500f, n);
                yield return null;
            }
            fight.Stop();
            fight.pitch = 1f;
            fightGain = 0f;
            fightFilter.cutoffFrequency = 22000f;
            fightRoutine = null;
        }

        private static IEnumerator FadeChannel(AudioSource source, AudioLowPassFilter filter, System.Func<float> getGain, System.Action<float> setGain,
            float targetGain, float seconds, float targetCutoff, bool stopAtEnd, float delay = 0f)
        {
            for (float t = 0f; t < delay; t += GameTime.UnscaledDelta) yield return null;
            float startGain = getGain();
            float startCutoff = filter.cutoffFrequency;
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds; t += GameTime.UnscaledDelta)
            {
                float n = Easing.Evaluate(EaseType.SmoothStep, t / seconds);
                setGain(Mathf.Lerp(startGain, targetGain, n));
                // Exponential sweep sounds natural for filters.
                filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(startCutoff), Mathf.Log(targetCutoff), n));
                yield return null;
            }
            setGain(targetGain);
            filter.cutoffFrequency = targetCutoff;
            if (stopAtEnd) source.Stop();
        }

        private void OnAudioRestarted()
        {
            // AudioSettings.Reset stops every source: restart what should be playing.
            if (city.clip != null && cityGain > 0f) city.Play();
            if (InFight && fight.clip != null) fight.Play();
        }
    }
}
