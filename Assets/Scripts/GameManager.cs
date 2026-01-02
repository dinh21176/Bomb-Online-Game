using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI gameInfoText;
    [SerializeField] GameObject coinPrefab;
    [SerializeField] private Vector2 spawnAreaMin;
    [SerializeField] private Vector2 spawnAreaMax;
    [SerializeField] private float spawnInterval = 1f;
    public static GameManager Instance { get; private set; }
    public NetworkVariable<float> gameTime = new NetworkVariable<float>(10f);
    private bool gameActive = false;

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
    private void Start()
    {
        gameInfoText.text = "Press Enter to start the game!";
        gameTime.OnValueChanged += UpdateGameInfoText;
    }

    private void Update()
    {
        if (IsHost && Input.GetKeyDown(KeyCode.Return) && !gameActive)
        {
            StartGame();
        }

        if (!IsServer || !gameActive) return;
        
        gameTime.Value -= Time.deltaTime;
        if (gameTime.Value <= 0)
        {
            EndGame();
        }
    }

    void StartGame()
    {
        if(!IsServer) return;
        Debug.Log("Game Started");
        gameTime.Value = 60f;
        SetGameActiveRpc(true);
        InvokeRepeating(nameof(SpawnCoin), 0f, spawnInterval);

    }

    void SetGameActiveRpc(bool active)
    {
        gameActive = active;
        if (gameActive)
        {
            gameInfoText.text = $"{gameTime.Value:F1}";
        }
        else
        {
            gameInfoText.text = "Game Over!!";
        }
    }

    private void SpawnCoin()
    {
        if (!gameActive) return;
        Vector2 pos = new Vector2(Random.Range(spawnAreaMin.x, spawnAreaMax.x), Random.Range(spawnAreaMin.y, spawnAreaMax.y));
        pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));

        GameObject item = Instantiate(coinPrefab, pos, Quaternion.identity);
        var netObj = item.GetComponent<NetworkObject>();
        var itemScript = item.GetComponent<Coin>();

        if (netObj != null)
        {
            netObj.Spawn();

            // ---  RARITY LOGIC ---
            int type = 0; // Default: Yellow Coin (0)
            float r = Random.Range(0f, 100f);


            if (r > 97f) type = 6;       // 3%  - Rare (Magenta)
            else if (r > 90f) type = 1;  // 10%  - Diamond (Cyan)
            else if (r > 75f) type = 2;  // 25% - Trap (Red)
            else if (r > 65f) type = 5;  // 35% - Fire/Range (DarkGreen)
            else if (r > 60f) type = 4;  // 40% - Bomb Up (Black)
            else if (r > 50f) type = 3;  // 50% - Speed (Blue)

            // If none of the above, it stays type 0 (Yellow Coin)

            itemScript.coinType.Value = type;
        }
    }

    private void EndGame()
    {
        gameTime.Value = 0;
        SetGameActiveRpc(false);
        CancelInvoke(nameof(SpawnCoin));
        Coin[] coins = FindObjectsByType<Coin>(FindObjectsSortMode.None);
        foreach (Coin coin in coins)
        {
            coin.DesTroyCoinRpc();
        }
        UpdateWinnerRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]

    void UpdateWinnerRpc()
    {
        string winnerText = ScoreBoardManager.Instance.GetWinnerName();
        gameInfoText.text = winnerText;

    }

    void UpdateGameInfoText(float previousTime, float newTime)
    {
        if(gameActive)
        {
            gameInfoText.text = $"{newTime:F1}";
        }
    }

    public Vector2 GetRandomSpawnPosition()
    {
        return new Vector2(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y)
        );
    }
}
