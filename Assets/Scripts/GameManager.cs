using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] TextMeshProUGUI gameInfoText;

    [Header("Prefabs")]
    [SerializeField] GameObject coinPrefab;
    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject warningPrefab; // Optional visual

    [Header("Map Settings")]
    [SerializeField] private Vector2 spawnAreaMin;
    [SerializeField] private Vector2 spawnAreaMax;
    [Tooltip("Layers that block coins from spawning (e.g., Wall, Bomb, Player)")]
    [SerializeField] private LayerMask obstructionLayer; 

    [Header("Game Settings")]
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private float suddenDeathTime = 40f;
    [SerializeField] private float gameDuration = 120f;
    [SerializeField] int finalArenaSize = 15;

    public static GameManager Instance { get; private set; }

    // Network Variables
    public NetworkVariable<bool> gameActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> gameTime = new NetworkVariable<float>(10f);

    private bool suddenDeathStarted = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (gameInfoText.text == "") gameInfoText.text = "WELCOME BOMB-ONLINE GAME, PLEASE INPUT YOUR NAME AND PLAY.";
    }

    public override void OnNetworkSpawn()
    {
        gameTime.OnValueChanged += UpdateGameInfoText;
        gameActive.OnValueChanged += OnGameActiveStateChanged;

        if (gameActive.Value)
        {
            gameInfoText.text = $"{gameTime.Value:F1}";
        }
    }

    private void Update()
    {
        if (IsHost && Input.GetKeyDown(KeyCode.Return) && !gameActive.Value)
        {
            StartGame();
        }

        if (!IsServer || !gameActive.Value) return;

        gameTime.Value -= Time.deltaTime;

        if (gameTime.Value <= suddenDeathTime && !suddenDeathStarted)
        {
            StartCoroutine(SuddenDeathRoutine());
        }

        if (gameTime.Value <= 0)
        {
            EndGame();
        }
    }

    void StartGame()
    {
        if (!IsServer) return;
        Debug.Log("Game Started");
        gameTime.Value = gameDuration;
        suddenDeathStarted = false;
        gameActive.Value = true;

        StartCoroutine(SpawnCoinRoutine());
    }

    IEnumerator SpawnCoinRoutine()
    {
        while (gameActive.Value && !suddenDeathStarted)
        {
            SpawnCoin();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void OnGameActiveStateChanged(bool previousValue, bool newValue)
    {
        if (newValue == true) gameInfoText.text = $"{gameTime.Value:F1}";
        else gameInfoText.text = "Game Over!!";
    }

    // ---  SPAWN LOGIC ---
    private void SpawnCoin()
    {
        if (!gameActive.Value) return;

        Vector2 spawnPos = Vector2.zero;
        bool validPositionFound = false;

        // Try 10 times to find a valid position
        // As the map shrinks, it gets harder to find a spot, so we limit attempts to prevent freezing.
        for (int i = 0; i < 10; i++)
        {
            Vector2 potentialPos = new Vector2(
                Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                Random.Range(spawnAreaMin.y, spawnAreaMax.y)
            );
            potentialPos = new Vector2(Mathf.Round(potentialPos.x), Mathf.Round(potentialPos.y));

            // Check if this spot is blocked by a Wall or Bomb
            if (!Physics2D.OverlapCircle(potentialPos, 0.4f, obstructionLayer))
            {
                spawnPos = potentialPos;
                validPositionFound = true;
                break; // Found a good spot!
            }
        }

        // If after 10 tries still hit walls (map is full), don't spawn anything
        if (!validPositionFound) return;

        // Instantiate
        GameObject item = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        var netObj = item.GetComponent<NetworkObject>();
        var itemScript = item.GetComponent<Coin>();

        if (netObj != null)
        {
            netObj.Spawn();

            // Rarity Logic
            int type = 0;
            float r = Random.Range(0f, 100f);

            if (r > 95f) type = 6;       // Rare
            else if (r > 90f) type = 1;  // Diamond
            else if (r > 75f) type = 2;  // Trap
            else if (r > 65f) type = 5;  // Fire
            else if (r > 55f) type = 4;  // Bomb Up
            else if (r > 45f) type = 3;  // Speed

            itemScript.coinType.Value = type;
        }
    }

    // --- SPAWN WALL (Prevents walls overlapping players/coins) ---
    void SpawnWall(int x, int y)
    {
        Vector2 pos = new Vector2(x, y);

        // Check for objects before spawning the wall
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(pos, 0.45f);

        foreach (var hit in hitObjects)
        {
            // If Wall lands on Coin -> Destroy Coin
            if (hit.TryGetComponent(out Coin coin))
            {
                if (coin.IsSpawned) coin.GetComponent<NetworkObject>().Despawn();
            }
            // If Wall lands on Bomb -> Explode Bomb
            if (hit.TryGetComponent(out Bomb bomb))
            {
                bomb.Detonate();
            }
            // If Wall lands on Player -> Kill Player
            if (hit.TryGetComponent(out PlayerMovement player))
            {
                player.Die();
            }
        }

        GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity);
        wall.GetComponent<NetworkObject>().Spawn();
    }

    // --- SUDDEN DEATH SPIRAL ---
    IEnumerator SuddenDeathRoutine()
    {
        suddenDeathStarted = true;
        Debug.Log("SUDDEN DEATH STARTED!");

        // STOP FARMING: Stop spawning new coins
        StopCoroutine(SpawnCoinRoutine());

        
        int minX = Mathf.RoundToInt(spawnAreaMin.x);
        int maxX = Mathf.RoundToInt(spawnAreaMax.x);
        int minY = Mathf.RoundToInt(spawnAreaMin.y);
        int maxY = Mathf.RoundToInt(spawnAreaMax.y);

        // Calculate width and height
        int currentWidth = maxX - minX;
        int currentHeight = maxY - minY;

        // LOOP UNTIL REACH THE "FINAL ARENA" SIZE
        while ((currentWidth > finalArenaSize || currentHeight > finalArenaSize) && gameActive.Value)
        {
            // 1. Top Row
            if (currentHeight > finalArenaSize)
            {
                for (int x = minX; x <= maxX; x++) { SpawnWall(x, maxY); yield return new WaitForSeconds(0.1f); }
                maxY--;
            }

            // 2. Right Column
            if (currentWidth > finalArenaSize)
            {
                for (int y = maxY; y >= minY; y--) { SpawnWall(maxX, y); yield return new WaitForSeconds(0.1f); }
                maxX--;
            }

            // 3. Bottom Row
            if (currentHeight > finalArenaSize) // Check again in case maxY changed
            {
                if (minY <= maxY)
                {
                    for (int x = maxX; x >= minX; x--) { SpawnWall(x, minY); yield return new WaitForSeconds(0.1f); }
                    minY++;
                }
            }

            // 4. Left Column
            if (currentWidth > finalArenaSize) // Check again
            {
                if (minX <= maxX)
                {
                    for (int y = minY; y <= maxY; y++) { SpawnWall(minX, y); yield return new WaitForSeconds(0.1f); }
                    minX++;
                }
            }

            // Update dimensions for the loop check
            currentWidth = maxX - minX;
            currentHeight = maxY - minY;
        }

        Debug.Log("Final Arena Reached! Fight!");
    }

    private void EndGame()
    {
        gameTime.Value = 0;
        gameActive.Value = false;

        CancelInvoke(nameof(SpawnCoin));
        StopAllCoroutines();
        suddenDeathStarted = false;

        Coin[] coins = FindObjectsByType<Coin>(FindObjectsSortMode.None);
        foreach (Coin coin in coins) { coin.DesTroyCoinRpc(); }

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
        if (gameActive.Value) gameInfoText.text = $"{newTime:F1}";
    }

    public Vector2 GetSafeSpawnPosition()
    {
        // Try 20 times to find a spot not covered by Wall/Bomb
        for (int i = 0; i < 20; i++)
        {
            Vector2 potentialPos = new Vector2(
                Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                Random.Range(spawnAreaMin.y, spawnAreaMax.y)
            );
            potentialPos = new Vector2(Mathf.Round(potentialPos.x), Mathf.Round(potentialPos.y));

            // Check if this spot is clear using the Obstruction Layer
            if (!Physics2D.OverlapCircle(potentialPos, 0.4f, obstructionLayer))
            {
                return potentialPos; // Found a safe spot!
            }
        }

        // return center if no safe spot found
        return Vector2.zero;
    }
}