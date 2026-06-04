using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public static GameMode pendingGameMode = GameMode.PvP;

    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button hostPvPButton;
    [SerializeField] private Button hostPvEButton;
    [SerializeField] private Button joinButton;

    [Header("Scene Name")]
    [SerializeField] private string gameSceneName = "GameScene";

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(AudioManager.Instance.mainMenuBGM);

        // Supports the new 3-button menu, while old scenes with one Host button still work.
        if (hostPvPButton == null)
            hostPvPButton = GameObject.Find("Host PvP")?.GetComponent<Button>()
                ?? GameObject.Find("HostPvPButton")?.GetComponent<Button>()
                ?? GameObject.Find("Create Room")?.GetComponent<Button>();

        if (hostPvEButton == null)
            hostPvEButton = GameObject.Find("Host PvE")?.GetComponent<Button>()
                ?? GameObject.Find("HostPvEButton")?.GetComponent<Button>();

        if (joinButton == null)
            joinButton = GameObject.Find("Join")?.GetComponent<Button>()
                ?? GameObject.Find("Join Room")?.GetComponent<Button>();

        if (hostPvPButton != null) hostPvPButton.onClick.AddListener(() => OnHostClicked(GameMode.PvP));
        if (hostPvEButton != null) hostPvEButton.onClick.AddListener(() => OnHostClicked(GameMode.PvE));
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);

        if (nameInputField != null)
            nameInputField.text = PlayerPrefs.GetString("PlayerName", "Adventurer");
    }

    private void SavePlayerName()
    {
        string pName = "Player";
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
            pName = nameInputField.text;

        PlayerPrefs.SetString("PlayerName", pName);
        PlayerPrefs.Save();
    }

    // HOST LOGIC (Gộp chung cho cả PvP và PvE)
    private void OnHostClicked(GameMode selectedMode)
    {
        SavePlayerName();

        // Lưu chế độ chơi vào biến static
        pendingGameMode = selectedMode;

        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }


    private void OnJoinClicked() // CLIENT LOGIC
    {
        SavePlayerName();

        // Start Client
        // Note: Clients DO NOT load scenes. They wait for the Host to sync them.
        NetworkManager.Singleton.StartClient();
    }
}
