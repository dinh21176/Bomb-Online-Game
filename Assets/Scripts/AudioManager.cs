using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource; 
    [SerializeField] private AudioSource sfxSource; 

    [Header("Background Music (BGM)")]
    public AudioClip mainMenuBGM;
    public AudioClip lobbyBGM;
    public AudioClip gameplayBGM; 

    [Header("Sound Effects (SFX)")]
    public AudioClip gameStartSFX;
    public AudioClip collectItemSFX;
    public AudioClip explodeSFX;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    public void PlayBGM(AudioClip clip)
    {
        Debug.Log("Music: " + clip.name);

        if (bgmSource.clip == clip) return; 
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }
}