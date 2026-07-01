using UnityEngine;

namespace SlideAndMatch
{
    /// <summary>
    /// Procedural SFX — generates AudioClips from code so no audio
    /// assets are required.  Attach to any GameObject in the scene.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private float masterVolume = 0.3f;

        private AudioSource source;
        private AudioClip slideClip;
        private AudioClip mergeClip;
        private AudioClip gameOverClip;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            GenerateClips();
        }

        // ── Public API ────────────────────────────────────────
        public void PlaySlide()    => Play(slideClip);
        public void PlayMerge()    => Play(mergeClip);
        public void PlayGameOver() => Play(gameOverClip);

        // ── Internals ─────────────────────────────────────────
        private void Play(AudioClip clip)
        {
            if (clip != null && source != null)
                source.PlayOneShot(clip, masterVolume);
        }

        private void GenerateClips()
        {
            slideClip    = CreateTone(220f, 0.08f, 0.25f);
            mergeClip    = CreateTone(440f, 0.12f, 0.35f);
            gameOverClip = CreateDescendingTone(300f, 150f, 0.4f, 0.3f);
        }

        /// <summary>Simple sine tone with exponential decay.</summary>
        private static AudioClip CreateTone(float frequency, float duration, float volume)
        {
            int sampleRate  = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount;
                envelope *= envelope;                             // exponential decay
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("tone_" + (int)frequency, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Sine that sweeps from startFreq → endFreq with fade-out.</summary>
        private static AudioClip CreateDescendingTone(
            float startFreq, float endFreq, float duration, float volume)
        {
            int sampleRate  = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / sampleRate;
                float progress = (float)i / sampleCount;
                float freq     = Mathf.Lerp(startFreq, endFreq, progress);
                float envelope = 1f - progress;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("descending_tone", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
