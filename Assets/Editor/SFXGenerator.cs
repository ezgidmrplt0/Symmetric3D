using UnityEngine;
using UnityEditor;
using System.IO;

public class SFXGenerator : EditorWindow
{
    private const int SAMPLE_RATE = 44100;
    private const string OUTPUT_DIR = "Assets/Audio/Generated";

    [MenuItem("Tools/Symmetric3D/Generate All SFX")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
                AssetDatabase.CreateFolder("Assets", "Audio");
            AssetDatabase.CreateFolder("Assets/Audio", "Generated");
        }

        SaveClip(GeneratePickup(),      "SFX_Pickup.wav");
        SaveClip(GeneratePlace(),       "SFX_Place.wav");
        SaveClip(GenerateRotate(),      "SFX_Rotate.wav");
        SaveClip(GenerateTransfer(),    "SFX_Transfer.wav");
        SaveClip(GenerateButtonClick(), "SFX_ButtonClick.wav");
        SaveClip(GenerateWin(),         "SFX_Win.wav");
        SaveClip(GenerateBgMusic(),     "BGM_Loop.wav");

        AssetDatabase.Refresh();
        AutoAssignToAudioManager();
        Debug.Log("[SFXGenerator] All sounds generated in " + OUTPUT_DIR);
    }

    // ── PICKUP: "viyu" swoosh / whoosh efekti (eski rotate sesi) ───
    static float[] GeneratePickup()
    {
        float duration = 0.22f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = t / duration;
            float env = Mathf.Sin(norm * Mathf.PI);
            float freq = Mathf.Lerp(300f, 850f, norm);
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
            float noise = (Mathf.PerlinNoise(t * 800f, 0f) - 0.5f) * 2f;
            val += noise * 0.2f * env;
            data[i] = val * env * 0.85f;
        }
        return data;
    }

    // ── PLACE: "viyu" swoosh zıttı — aşağı inen ters swoosh ───────
    static float[] GeneratePlace()
    {
        float duration = 0.22f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = t / duration;
            float env = Mathf.Sin(norm * Mathf.PI);
            // Yüksek frekanstan alçağa inen swoosh (850Hz -> 250Hz)
            float freq = Mathf.Lerp(850f, 250f, norm);
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;
            float noise = (Mathf.PerlinNoise(t * 800f, 42f) - 0.5f) * 2f;
            val += noise * 0.2f * env;
            data[i] = val * env * 0.85f;
        }
        return data;
    }

    // ── ROTATE: tınlayan "bloop" efekti (eski pickup sesi) ─────────
    static float[] GenerateRotate()
    {
        float duration = 0.18f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 18f);
            float freq = Mathf.Lerp(380f, 950f, t / duration);
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
            val += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;
            data[i] = val * env;
        }
        return data;
    }

    // ── TRANSFER: sıvı akış hissi — kabarcıklı glug ──────────────
    static float[] GenerateTransfer()
    {
        float duration = 0.45f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = t / duration;
            float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 1.5f);

            // base bubbly tone
            float bubbleRate = 12f;
            float bubbleMod = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * bubbleRate * t);
            float freq = 280f * bubbleMod;
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;

            // secondary wobble
            float freq2 = 420f * (1f + 0.2f * Mathf.Sin(2f * Mathf.PI * 8f * t));
            val += Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.2f;

            // water noise texture
            float noise = (Mathf.PerlinNoise(t * 600f, 42f) - 0.5f) * 2f;
            val += noise * 0.15f;

            data[i] = val * env * 0.85f;
        }
        return data;
    }

    // ── BUTTON CLICK: tatminkar, bubble/pop tarzı canlı ve doygun tınlayan "POP" ──────
    static float[] GenerateButtonClick()
    {
        float duration = 0.08f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float norm = t / duration;

            // Hızlı frekans yükselmesi (Pitch sweep: 450Hz -> 1900Hz)
            float freq = Mathf.Lerp(450f, 1900f, Mathf.Pow(norm, 0.35f));

            // Yumuşak balon patlama zarfı (Pop envelope)
            float env = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 7f);

            // Ana ton + üst harmonik ekleme
            float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
            val += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.25f;

            data[i] = val * env;
        }
        Normalize(data, 0.9f);
        return data;
    }

    // ── WIN: majör arpej fanfare ──────────────────────────────────
    static float[] GenerateWin()
    {
        float duration = 1.4f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        // C5, E5, G5, C6 arpeggio
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteLen = 0.25f;
        float overlap = 0.12f;

        for (int n = 0; n < notes.Length; n++)
        {
            float noteStart = n * (noteLen - overlap);
            int startSample = (int)(noteStart * SAMPLE_RATE);

            for (int i = 0; i < (int)(noteLen * 2f * SAMPLE_RATE); i++)
            {
                int idx = startSample + i;
                if (idx >= samples) break;

                float t = (float)i / SAMPLE_RATE;
                float env = Mathf.Exp(-t * 3.5f) * Mathf.Clamp01(t / 0.01f);
                float val = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * 0.35f;
                val += Mathf.Sin(2f * Mathf.PI * notes[n] * 2f * t) * 0.12f;
                val += Mathf.Sin(2f * Mathf.PI * notes[n] * 3f * t) * 0.06f;
                data[idx] += val * env;
            }
        }

        // final shimmer chord (all notes together)
        float chordStart = notes.Length * (noteLen - overlap);
        int chordSample = (int)(chordStart * SAMPLE_RATE);
        for (int i = 0; i < samples - chordSample; i++)
        {
            int idx = chordSample + i;
            float t = (float)i / SAMPLE_RATE;
            float env = Mathf.Exp(-t * 2f) * Mathf.Clamp01(t / 0.02f);
            float val = 0f;
            foreach (float freq in notes)
            {
                val += Mathf.Sin(2f * Mathf.PI * freq * t) * 0.15f;
                val += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.04f;
            }
            data[idx] += val * env;
        }

        Normalize(data, 0.85f);
        return data;
    }

    // ── BG MUSIC: ambient loop — rahatlatıcı pentatonik kalıp ────
    static float[] GenerateBgMusic()
    {
        float duration = 16f;
        int samples = (int)(SAMPLE_RATE * duration);
        float[] data = new float[samples];

        // C pentatonic: C4, D4, E4, G4, A4
        float[] scale = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f };

        // pad: warm chord drone
        float[] padNotes = { scale[0], scale[2], scale[4] }; // C, E, A
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SAMPLE_RATE;
            float padEnv = 0.12f;
            float val = 0f;
            foreach (float freq in padNotes)
            {
                float f = freq * 0.5f; // one octave lower
                val += Mathf.Sin(2f * Mathf.PI * f * t) * padEnv;
                val += Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * padEnv * 0.3f; // slight detune for warmth
            }
            data[i] = val;
        }

        // melodic plucks on a simple repeating pattern
        float bpm = 90f;
        float beatDur = 60f / bpm;
        int[] pattern = { 0, 2, 4, 3, 2, 0, 3, 4, 2, 4, 3, 0, 4, 2, 0, 3,
                          2, 4, 0, 3, 4, 2, 3, 0, 0, 3, 2, 4, 3, 0, 4, 2 };

        for (int p = 0; p < pattern.Length; p++)
        {
            float noteStart = p * beatDur * 0.5f;
            int startSample = (int)(noteStart * SAMPLE_RATE);
            float freq = scale[pattern[p]];
            float noteDur = beatDur * 0.8f;

            for (int i = 0; i < (int)(noteDur * SAMPLE_RATE); i++)
            {
                int idx = startSample + i;
                if (idx >= samples) break;

                float t = (float)i / SAMPLE_RATE;
                float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.005f);
                float val = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.22f;
                val += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.08f;
                val += Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.03f;
                data[idx] += val * env;
            }
        }

        // soft percussion: closed hi-hat style ticks on every beat
        for (float beat = 0f; beat < duration; beat += beatDur)
        {
            int startSample = (int)(beat * SAMPLE_RATE);
            for (int i = 0; i < (int)(0.04f * SAMPLE_RATE); i++)
            {
                int idx = startSample + i;
                if (idx >= samples) break;
                float t = (float)i / SAMPLE_RATE;
                float env = Mathf.Exp(-t * 80f);
                float noise = Mathf.PerlinNoise(t * 4000f, beat * 100f) - 0.5f;
                data[idx] += noise * env * 0.08f;
            }
        }

        // fade last 0.5s for seamless loop
        int fadeSamples = (int)(0.5f * SAMPLE_RATE);
        for (int i = 0; i < fadeSamples; i++)
        {
            float fade = (float)i / fadeSamples;
            // fade out end
            data[samples - 1 - i] *= fade;
            // crossfade: blend start into end for loop
            data[samples - 1 - i] += data[i] * (1f - fade) * 0.5f;
        }

        Normalize(data, 0.7f);
        return data;
    }

    // ── UTILITIES ─────────────────────────────────────────────────

    static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float s in data)
            if (Mathf.Abs(s) > max) max = Mathf.Abs(s);
        if (max < 0.001f) return;
        float scale = peak / max;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;
    }

    static void SaveClip(float[] data, string filename)
    {
        Normalize(data, 0.85f);
        string path = Path.Combine(OUTPUT_DIR, filename);
        string fullPath = Path.Combine(Application.dataPath, "..", path);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int sampleCount = data.Length;
            short channels = 1;
            int byteRate = SAMPLE_RATE * channels * 2;
            short blockAlign = (short)(channels * 2);

            // WAV header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write(channels);
            writer.Write(SAMPLE_RATE);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((short)16); // bits per sample
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(sampleCount * 2);

            for (int i = 0; i < sampleCount; i++)
            {
                short val = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
                writer.Write(val);
            }
        }

        Debug.Log("[SFXGenerator] Saved: " + path);
    }

    static void AutoAssignToAudioManager()
    {
        AudioManager manager = FindObjectOfType<AudioManager>();
        if (manager == null)
        {
            Debug.LogWarning("[SFXGenerator] AudioManager not found in scene. Assign clips manually.");
            return;
        }

        manager.pickupSFX      = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_Pickup.wav");
        manager.placeSFX       = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_Place.wav");
        manager.rotateSFX      = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_Rotate.wav");
        manager.transferSFX    = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_Transfer.wav");

        AudioClip popClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/pop.mp3");
        if (popClip == null) popClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/dragon-studio-bubble-pop-406640.mp3");
        if (popClip == null) popClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/universfield-bubble-pop-293342.mp3");
        if (popClip == null) popClip = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_ButtonClick.wav");
        manager.buttonClickSFX = popClip;

        manager.winSFX         = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/SFX_Win.wav");
        manager.bgMusic        = AssetDatabase.LoadAssetAtPath<AudioClip>(OUTPUT_DIR + "/BGM_Loop.wav");

        EditorUtility.SetDirty(manager);
        Debug.Log("[SFXGenerator] All clips auto-assigned to AudioManager!");
    }
}
