using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip pickupSFX;
    public AudioClip placeSFX;
    public AudioClip rotateSFX;
    public AudioClip transferSFX;
    public AudioClip buttonClickSFX;
    public AudioClip winSFX;
    public AudioClip bgMusic;

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource musicSource;

    private const string SOUND_MUTED_KEY = "SoundMuted";
    private const string MUSIC_MUTED_KEY = "MusicMuted";

    public bool IsSoundMuted { get; private set; }
    public bool IsMusicMuted { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        LoadSettings();
    }

    private void InitializeAudioSources()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
    }

    private void Start()
    {
        if (bgMusic != null && musicSource != null && !IsMusicMuted)
        {
            musicSource.clip = bgMusic;
            musicSource.Play();
        }

        AttachAllButtonListeners();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        AttachAllButtonListeners();
    }

    public static void AttachAllButtonListeners()
    {
        Button[] allButtons = FindObjectsOfType<Button>(true);
        foreach (Button btn in allButtons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(PlayButtonClick);
                btn.onClick.AddListener(PlayButtonClick);
            }
        }
    }

    private void LoadSettings()
    {
        IsSoundMuted = PlayerPrefs.GetInt(SOUND_MUTED_KEY, 0) == 1;
        IsMusicMuted = PlayerPrefs.GetInt(MUSIC_MUTED_KEY, 0) == 1;

        if (sfxSource != null) sfxSource.mute = IsSoundMuted;
        if (musicSource != null) musicSource.mute = IsMusicMuted;
    }

    public void ToggleSound()
    {
        IsSoundMuted = !IsSoundMuted;
        PlayerPrefs.SetInt(SOUND_MUTED_KEY, IsSoundMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (sfxSource != null) sfxSource.mute = IsSoundMuted;
    }

    public void ToggleMusic()
    {
        IsMusicMuted = !IsMusicMuted;
        PlayerPrefs.SetInt(MUSIC_MUTED_KEY, IsMusicMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (musicSource != null)
        {
            musicSource.mute = IsMusicMuted;
            if (!IsMusicMuted && bgMusic != null && !musicSource.isPlaying)
            {
                musicSource.clip = bgMusic;
                musicSource.Play();
            }
        }
    }

    public static void PlayPickup()
    {
        if (Instance != null && Instance.pickupSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.PlayOneShot(Instance.pickupSFX);
        }
    }

    public static void PlayPlace()
    {
        if (Instance != null && Instance.placeSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.PlayOneShot(Instance.placeSFX);
        }
    }

    public static void PlayRotate()
    {
        if (Instance != null && Instance.rotateSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.PlayOneShot(Instance.rotateSFX);
        }
    }

    public static void PlayTransfer()
    {
        if (Instance != null && Instance.transferSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.PlayOneShot(Instance.transferSFX);
        }
    }

    public static void PlayButtonClick()
    {
        if (Instance != null && Instance.buttonClickSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.pitch = Random.Range(0.95f, 1.06f);
            Instance.sfxSource.PlayOneShot(Instance.buttonClickSFX);
            Instance.sfxSource.pitch = 1.0f;
        }
    }

    public static void PlayWin()
    {
        if (Instance != null && Instance.winSFX != null && !Instance.IsSoundMuted)
        {
            Instance.sfxSource.PlayOneShot(Instance.winSFX);
        }
    }

    public void PlayOneShotSFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null && !IsSoundMuted)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
}
