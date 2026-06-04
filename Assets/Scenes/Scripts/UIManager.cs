using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
public class UIManager : MonoBehaviour
{
    [SerializeField] public TMP_InputField nameinputField;
    [SerializeField] private TextMeshProUGUI gameInfoText;

    [SerializeField] Button hostButton;
    [SerializeField] Button joinButton;
    [SerializeField] GameObject menu;


    void Start()
    {
        hostButton.onClick.AddListener(Host);
        joinButton.onClick.AddListener(Join);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(AudioManager.Instance.lobbyBGM);
    }

    void Host()
    {
        NetworkManager.Singleton.StartHost();
        menu.SetActive(false);
        gameInfoText.gameObject.SetActive(true);
        gameInfoText.text = "Press Enter to start";

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(AudioManager.Instance.lobbyBGM);
    }

    void Join()
    {
        NetworkManager.Singleton.StartClient();
        menu.SetActive(false);
        gameInfoText.gameObject.SetActive(true);
        gameInfoText.text = "Waiting for host to start the game";

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(AudioManager.Instance.lobbyBGM);
    }
}
