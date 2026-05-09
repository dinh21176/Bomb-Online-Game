using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource; // Nguồn phát nhạc nền
    [SerializeField] private AudioSource sfxSource; // Nguồn phát hiệu ứng

    [Header("Background Music (BGM)")]
    public AudioClip mainMenuBGM;
    public AudioClip lobbyBGM;
    public AudioClip gameplayBGM; // ThuyenChien.mp3

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

    // Hàm gọi nhạc nền (có lặp lại)
    public void PlayBGM(AudioClip clip)
    {
        Debug.Log("Music: " + clip.name);

        if (bgmSource.clip == clip) return; // Nếu đang phát bài này rồi thì thôi
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    // Hàm gọi hiệu ứng âm thanh (chỉ kêu 1 lần)
    public void PlaySFX(AudioClip clip)
    {
        sfxSource.PlayOneShot(clip);
    }
}