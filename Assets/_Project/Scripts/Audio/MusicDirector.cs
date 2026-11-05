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
    /// <summary>How the Scrapeyard stems are balanced in a given place.</summary>
    public enum StemMix { Street, Quiet, Park, Core, Ghost, Interior, Home, Station }

    public class MusicDirector : MonoBehaviour
    {
        // Stem order: chords, bass, drums, lead, arp, cello, white noise. Last value: low-pass cutoff (Hz).
        private static readonly float[][] Mixes =
        {
            new[] { 1f, 1f, 1f, 1f, 1f, 0.8f, 0.25f, 22000f },      // Street: the whole song
            new[] { 0.9f, 0f, 0f, 0.35f, 0.8f, 1f, 0.2f, 22000f },   // Quiet: rooftop, breathing room
            new[] { 0.9f, 0.7f, 0.35f, 0.5f, 0.4f, 1f, 0.3f, 12000f }, // Park: melancholy, drums far away
            new[] { 0.25f, 1f, 0.9f, 0.2f, 1f, 0.3f, 0.7f, 22000f }, // Core: tense, static-heavy
            new[] { 0.6f, 0f, 0f, 0f, 0.3f, 1f, 0.6f, 6000f },       // Ghost Line: strings and hiss
            new[] { 0.7f, 0.4f, 0f, 0f, 0f, 0.9f, 0.4f, 900f },      // Interior: the street heard through walls
            new[] { 0.8f, 0f, 0f, 0.3f, 0.6f, 0.7f, 0f, 3500f },     // Home: a music box version
            new[] { 0.6f, 0f, 0f, 0f, 0.25f, 0.8f, 0.5f, 4000f },    // Station: echoing, far away
        };
        private const int StemCount = 7;
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

        /// <summary>The city-channel clip currently playing (null if none).</summary>
        public AudioClip CurrentCityClip => city != null && city.isPlaying ? city.clip : null;

        // Where each city track was when it was swapped out (shop music → back to the street, resumed).
        private readonly System.Collections.Generic.Dictionary<AudioClip, float> resumeTimes = new System.Collections.Generic.Dictionary<AudioClip, float>();

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
            float dt = Time.unscaledDeltaTime;
            overrideMute = Mathf.MoveTowards(overrideMute, overrideActive ? 0f : 1f, dt / 0.9f);
            city.volume = cityLevel * cityGain * music * overrideMute;
            fight.volume = fightLevel * fightGain * music;
            if (overrideSource != null) overrideSource.volume = cityLevel * overrideGain * music * (InFight ? 0f : 1f);
            if (overrideSource != null)
            {
                overrideGain = Mathf.MoveTowards(overrideGain, overrideActive ? 1f : 0f, dt / 0.9f);
                if (!overrideActive && overrideGain <= 0f && overrideSource.isPlaying) overrideSource.Stop();
            }
            UpdateStems(dt, music);
        }

        // ================================================================== Stems (adaptive city music)

        private AudioSource[] stems;
        private AudioLowPassFilter[] stemFilters;
        private readonly float[] stemLevels = new float[StemCount];
        private float stemCutoff = 22000f;
        private bool stemsActive;
        private StemMix baseMix = StemMix.Street;
        private readonly System.Collections.Generic.List<StemMix> mixStack = new System.Collections.Generic.List<StemMix>();

        public bool StemsPlaying => stemsActive;

        /// <summary>
        /// Plays the city song as separate stems, all in sync, mixed for this place. If the same stems are
        /// already playing (walking from one Subnet scene to the next), they keep going and only the mix moves.
        /// </summary>
        public void PlayStems(AudioClip[] clips, StemMix mix, float fadeSeconds = 2.5f)
        {
            if (clips == null || clips.Length < StemCount) return;
            EnsureStemSources();
            baseMix = mix;
            mixStack.Clear();
            bool same = stemsActive;
            for (int i = 0; i < StemCount && same; i++) same = stems[i].clip == clips[i] && stems[i].isPlaying;
            if (!same)
            {
                if (city.isPlaying) { resumeTimes[city.clip] = city.time; city.Stop(); }
                double start = AudioSettings.dspTime + 0.2;
                for (int i = 0; i < StemCount; i++)
                {
                    stems[i].Stop();
                    stems[i].clip = clips[i];
                    stems[i].time = 0f;
                    if (clips[i] != null) stems[i].PlayScheduled(start);
                    stemLevels[i] = 0f;
                }
                stemsActive = true;
            }
            stemsStopPending = false;
            if (!InFight) FadeCity(1f, fadeSeconds, 22000f);
        }

        private bool stemsStopPending;

        public void StopStems()
        {
            if (!stemsActive) return;
            stemsActive = false;
            foreach (AudioSource s in stems) s.Stop();
        }

        /// <summary>A place inside the scene (rooftop, an apartment) changes the balance while Leo is in it.</summary>
        public void PushMix(StemMix mix) => mixStack.Add(mix);

        public void PopMix(StemMix mix)
        {
            int i = mixStack.LastIndexOf(mix);
            if (i >= 0) mixStack.RemoveAt(i);
        }

        private void EnsureStemSources()
        {
            if (stems != null) return;
            stems = new AudioSource[StemCount];
            stemFilters = new AudioLowPassFilter[StemCount];
            for (int i = 0; i < StemCount; i++) stems[i] = CreateChannel("Stem_" + i, out stemFilters[i]);
        }

        private void UpdateStems(float dt, float music)
        {
            if (!stemsActive || stems == null) return;
            if (stemsStopPending && cityGain <= 0.001f) { stemsStopPending = false; StopStems(); return; }
            float[] mix = Mixes[(int)(mixStack.Count > 0 ? mixStack[mixStack.Count - 1] : baseMix)];
            bool talking = Subnet.Dialogue.DialogueManager.Instance != null && Subnet.Dialogue.DialogueManager.Instance.IsOpen;
            stemCutoff = Mathf.Exp(Mathf.MoveTowards(Mathf.Log(stemCutoff), Mathf.Log(mix[StemCount]), dt * 2.5f));
            float cutoff = Mathf.Min(stemCutoff, cityFilter.cutoffFrequency);
            for (int i = 0; i < StemCount; i++)
            {
                float target = mix[i];
                // While someone talks, the busy parts step back so the words come first.
                if (talking && (i == 2 || i == 3)) target *= 0.45f;
                else if (talking && i == 4) target *= 0.7f;
                stemLevels[i] = Mathf.MoveTowards(stemLevels[i], target, dt / 1.6f);
                stems[i].volume = cityLevel * cityGain * music * overrideMute * stemLevels[i];
                stemFilters[i].cutoffFrequency = cutoff;
            }
        }

        // ================================================================== Override (a room with its own song)

        private AudioSource overrideSource;
        private bool overrideActive;
        private float overrideGain;
        private float overrideMute = 1f;

        /// <summary>The shop's song takes over; the city keeps running silently underneath, in sync.</summary>
        public void PushOverride(AudioClip clip)
        {
            if (clip == null) return;
            if (overrideSource == null) overrideSource = CreateChannel("Override", out _);
            if (overrideSource.clip != clip || !overrideSource.isPlaying)
            {
                overrideSource.clip = clip;
                overrideSource.time = resumeTimes.TryGetValue(clip, out float t) && t < clip.length - 1f ? t : 0f;
                overrideSource.Play();
            }
            overrideActive = true;
        }

        public void PopOverride()
        {
            if (overrideSource != null && overrideSource.clip != null) resumeTimes[overrideSource.clip] = overrideSource.time;
            overrideActive = false;
        }

        // ================================================================== City

        /// <summary>Starts (or keeps) the city track. Same clip already playing = nothing changes.</summary>
        public void PlayCity(AudioClip clip, float fadeSeconds = 2.5f)
        {
            if (clip == null) return;
            if (stemsActive) StopStems();
            if (city.clip == clip && city.isPlaying)
            {
                if (!InFight) FadeCity(1f, fadeSeconds, 22000f);
                return;
            }
            if (city.clip != null && city.isPlaying) resumeTimes[city.clip] = city.time;
            city.clip = clip;
            city.time = resumeTimes.TryGetValue(clip, out float resume) && resume < clip.length - 1f ? resume : 0f;
            city.Play();
            cityGain = 0f;
            cityFilter.cutoffFrequency = 22000f;
            if (!InFight) FadeCity(1f, fadeSeconds, 22000f);
        }

        /// <summary>Fades the city track out and stops it.</summary>
        public void StopCity(float fadeSeconds = 2f)
        {
            if (stemsActive)
            {
                stemsStopPending = true;
                FadeCity(0f, fadeSeconds, 22000f);
                return;
            }
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

            // Stems play on their own sources (city stays silent), so they count as "city playing" too.
            if (city.isPlaying || stemsActive) FadeCity(0f, 0.7f, 450f);

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

            if ((city.clip != null && city.isPlaying) || (stemsActive && !stemsStopPending))
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
            if (stemsActive)
            {
                double start = AudioSettings.dspTime + 0.2;
                foreach (AudioSource stem in stems) if (stem.clip != null) stem.PlayScheduled(start);
            }
        }
    }
}
