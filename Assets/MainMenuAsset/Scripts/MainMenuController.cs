using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button createButton; 
    [SerializeField] private Button joinButton;  

    [Header("Scene Name")]
    [SerializeField] private string gameSceneName = "GameScene";

    private void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(AudioManager.Instance.mainMenuBGM);

        // 1. Setup Listeners
        createButton.onClick.AddListener(OnCreateClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // 2. Load Saved Name
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

    private void OnCreateClicked() // HOST LOGIC
    {
        SavePlayerName();

        // Start Host
        NetworkManager.Singleton.StartHost();

        // Load Scene (Only Host does this!)
        // The Client will automatically follow because of "Enable Scene Management"
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