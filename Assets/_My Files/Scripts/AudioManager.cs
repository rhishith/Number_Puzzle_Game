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
        private AudioSource bgmSource;
        private AudioClip slideClip;
        private AudioClip mergeClip;
        private AudioClip gameOverClip;
        private AudioClip bgmClip;

        public bool MusicMuted
        {
            get => PlayerPrefs.GetInt("MusicMuted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("MusicMuted", value ? 1 : 0);
                PlayerPrefs.Save();
                UpdateMusicState();
            }
        }

        public bool SfxMuted
        {
            get => PlayerPrefs.GetInt("SfxMuted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("SfxMuted", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public bool IsMuted
        {
            get => SfxMuted;
            set => SfxMuted = value;
        }

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

            // Setup BGM Source
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
            bgmSource.volume = 0.12f; // Soft background volume

            GenerateClips();
            bgmClip = GenerateProceduralBgm();
            bgmSource.clip = bgmClip;

            UpdateMusicState();
        }

        // ── Public API ────────────────────────────────────────
        public void PlaySlide()
        {
            if (source != null) source.pitch = 1.0f;
            Play(slideClip);
        }

        public void PlayMerge(int mergedValue = 4)
        {
            if (source != null && mergeClip != null && !SfxMuted)
            {
                // Calculate pitch: base pitch is 1.0 (for 4). Every double in value (e.g. 8, 16, 32...) adds 0.12f to pitch
                float pitch = 1.0f + Mathf.Log(mergedValue / 4f, 2f) * 0.12f;
                pitch = Mathf.Clamp(pitch, 0.8f, 2.5f);

                source.pitch = pitch;
                source.PlayOneShot(mergeClip, masterVolume);
            }
        }

        public void PlayGameOver()
        {
            if (source != null) source.pitch = 1.0f;
            Play(gameOverClip);
        }

        // ── Internals ─────────────────────────────────────────
        private void UpdateMusicState()
        {
            if (bgmSource != null)
            {
                bgmSource.mute = MusicMuted;
                if (!MusicMuted && !bgmSource.isPlaying)
                {
                    bgmSource.Play();
                }
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && source != null && !SfxMuted)
                source.PlayOneShot(clip, masterVolume);
        }

        private void GenerateClips()
        {
            slideClip    = CreateSlideSFX();
            mergeClip    = CreateMergeSFX();
            gameOverClip = CreateGameOverSFX();
        }

        // ── Waveform Synthesizers (Chiptune Sound) ─────────────
        private static float TriangleWave(float phase)
        {
            float x = phase % (2f * Mathf.PI);
            if (x < 0f) x += 2f * Mathf.PI;
            float norm = x / (2f * Mathf.PI);
            return 2f * Mathf.Abs(2f * (norm - Mathf.Floor(norm + 0.5f))) - 1f;
        }

        private static float SquareWave(float phase, float dutyCycle = 0.5f)
        {
            float x = phase % (2f * Mathf.PI);
            if (x < 0f) x += 2f * Mathf.PI;
            return (x / (2f * Mathf.PI)) < dutyCycle ? 1f : -1f;
        }

        // ── SFX Synthesizers ───────────────────────────────────
        private static AudioClip CreateSlideSFX()
        {
            int sampleRate = 44100;
            float duration = 0.10f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                
                // Sweep frequency from 300Hz to 700Hz
                float freq = Mathf.Lerp(300f, 700f, progress);
                phase += 2f * Mathf.PI * freq / sampleRate;

                float env = 1f - progress;
                samples[i] = TriangleWave(phase) * env * 0.18f;
            }

            AudioClip clip = AudioClip.Create("sfx_slide", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMergeSFX()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float firstNoteDuration = 0.07f;
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = (t < firstNoteDuration) ? 987.77f : 1318.51f; // B5 → E6
                phase += 2f * Mathf.PI * freq / sampleRate;

                float env = 1f - (t / duration);
                samples[i] = SquareWave(phase) * env * 0.12f;
            }

            AudioClip clip = AudioClip.Create("sfx_merge", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateGameOverSFX()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float[] notes = { 392.00f, 329.63f, 261.63f, 196.00f }; // G4 → E4 → C4 → G3
            float noteDuration = duration / 4f;
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                int currentNote = Mathf.FloorToInt(t / noteDuration);
                currentNote = Mathf.Clamp(currentNote, 0, 3);
                float noteTime = t % noteDuration;

                float freq = notes[currentNote];
                phase += 2f * Mathf.PI * freq / sampleRate;

                float env = 1f - (noteTime / noteDuration);
                samples[i] = SquareWave(phase) * env * 0.15f;
            }

            AudioClip clip = AudioClip.Create("sfx_gameover", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ── Loopable Background Music Synthesizer ──────────────
        private AudioClip GenerateProceduralBgm()
        {
            int sampleRate = 44100;
            float tempo = 110f; // 110 BPM
            float beatDuration = 60f / tempo;
            float stepDuration = beatDuration / 2f; // Eighth notes (0.272s)
            int stepsCount = 64; // 8 bars (17.45 seconds loop)
            float totalDuration = stepDuration * stepsCount;
            int sampleCount = Mathf.CeilToInt(sampleRate * totalDuration);
            float[] samples = new float[sampleCount];

            float[] scaleAm = { 220f, 261.63f, 329.63f, 440f, 523.25f, 659.25f }; // Am chord
            float[] scaleF  = { 174.61f, 220.00f, 261.63f, 349.23f, 440.00f, 523.25f }; // F chord
            float[] scaleC  = { 130.81f, 164.81f, 196.00f, 261.63f, 329.63f, 392.00f }; // C chord
            float[] scaleG  = { 196.00f, 246.94f, 293.66f, 392.00f, 493.88f, 587.33f }; // G chord

            int[] arpPattern = { 0, 2, 1, 3, 2, 4, 3, 5 };

            float bassPhase = 0f;
            float arpPhase = 0f;
            float melPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                int currentBar = Mathf.FloorToInt(t / (stepDuration * 8)) % 8;
                int currentStepInBar = Mathf.FloorToInt(t / stepDuration) % 8;
                int globalStep = Mathf.FloorToInt(t / stepDuration) % stepsCount;
                float stepTime = t % stepDuration;

                float[] activeScale;
                float bassFreq;
                if (currentBar < 2)      { activeScale = scaleAm; bassFreq = 110.00f; } // Am
                else if (currentBar < 4) { activeScale = scaleF;  bassFreq = 87.31f; }  // F
                else if (currentBar < 6) { activeScale = scaleC;  bassFreq = 130.81f; } // C
                else                     { activeScale = scaleG;  bassFreq = 98.00f; }  // G

                // 1. Bass Line (Sine wave)
                bassPhase += 2f * Mathf.PI * bassFreq / sampleRate;
                float bassVal = Mathf.Sin(bassPhase) * 0.4f;

                // 2. Arpeggio (Triangle wave)
                int noteIdx = arpPattern[currentStepInBar];
                float arpFreq = activeScale[noteIdx];
                arpPhase += 2f * Mathf.PI * arpFreq / sampleRate;
                float arpEnv = Mathf.Exp(-stepTime * 4.5f);
                float arpVal = TriangleWave(arpPhase) * arpEnv * 0.22f;

                // 3. Melody (Sine wave bells)
                float melVal = 0f;
                if (currentBar % 2 == 1 && globalStep % 16 >= 4)
                {
                    int melStep = (globalStep / 2) % 4;
                    float[] melodyNotes = { activeScale[3], activeScale[4], activeScale[5], activeScale[4] };
                    float melFreq = melodyNotes[melStep];
                    melPhase += 2f * Mathf.PI * melFreq / sampleRate;
                    float melEnv = Mathf.Exp(-(t % (stepDuration * 2f)) * 3f);
                    melVal = Mathf.Sin(melPhase) * melEnv * 0.15f;
                }

                float mix = (bassVal + arpVal + melVal) * 0.15f;
                samples[i] = Mathf.Clamp(mix, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("procedural_bgm", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
