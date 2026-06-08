using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public enum GameMode
{
    PvP,
    PvE
}

public enum PvEStage
{
    NotStarted,
    Stage1,       // Ải 1: Quái nhỏ dễ
    Stage2,       // Ải 2: Quái nhỏ đông/khó hơn
    Stage3_Boss,  // Ải 3: Trận chiến với Boss
    Victory,      // Thắng toàn bộ chế độ PvE
    Defeat        // Thua cuộc do hết thời gian hoặc chết hết
}

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] TextMeshProUGUI gameInfoText;
    [SerializeField] EndGamePanelUI endGamePanel;

    [Header("Prefabs")]
    [SerializeField] GameObject coinPrefab;
    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject warningPrefab; // Optional visual
    [SerializeField] GameObject botPrefab;
    [SerializeField] GameObject basicMonsterPrefab;
    [SerializeField] GameObject bossPrefab;

    [Header("Map Settings")]
    public Vector2 spawnAreaMin;
    public Vector2 spawnAreaMax;
    [Tooltip("Layers that block coins from spawning (e.g., Wall, Bomb, Player)")]
    [SerializeField] private LayerMask obstructionLayer; 

    [Header("Game Settings")]
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private float suddenDeathTime = 40f;
    [SerializeField] private float gameDuration = 120f;
    [SerializeField] int finalArenaSize = 15;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("PvE Core Systems")]
    public NetworkVariable<GameMode> currentMode = new NetworkVariable<GameMode>(GameMode.PvP);
    public NetworkVariable<PvEStage> currentStage = new NetworkVariable<PvEStage>(PvEStage.NotStarted);

    // Quản lý danh sách quái để tính điều kiện qua màn
    private List<GameObject> spawnedMonsters = new List<GameObject>();
    private int voteReplayCount = 0;
    private int totalConnectedPlayers = 0;

    [HideInInspector] public int selectedMapIndex = 0; 
    public static GameManager Instance { get; private set; }

    // Network Variables
    public NetworkVariable<bool> gameActive = new NetworkVariable<bool>(false);
    public NetworkVariable<float> gameTime = new NetworkVariable<float>(10f);

    public bool suddenDeathStarted = false;

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

        if (!gameActive.Value)
        {
            if (IsHost)
            {
                gameInfoText.text = "Waiting for player, press Enter to start the game.";
            }
            else
            {
                gameInfoText.text = "Waiting for host to start the game.";
            }
        }
        else 
        {
            gameInfoText.text = $"{gameTime.Value:F1}";
        }
    }

    private void Update()
    {
        if (IsServer && !gameActive.Value)
        {
            // Hợp nhất: Bấm Enter sẽ tự động chạy đúng chế độ mà Host đã chọn ở Menu
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                currentMode.Value = MainMenuManager.pendingGameMode;

                if (currentMode.Value == GameMode.PvP)
                {
                    StartGame();
                }
                else if (currentMode.Value == GameMode.PvE)
                {
                    StartPvEStage(PvEStage.Stage1);
                }
                return;
            }
        }

        if (!IsServer || !gameActive.Value) return;

        // Logic đếm ngược thời gian chung
        if (gameTime.Value > 0)
        {
            gameTime.Value -= Time.deltaTime;
        }
        else
        {
            // HẾT THỜI GIAN TRẬN ĐẤU
            if (currentMode.Value == GameMode.PvP)
            {
                EndGame(); // Logic kết thúc PvP cũ của bạn
            }
            else if (currentMode.Value == GameMode.PvE)
            {
                TriggerPvEDefeat("Hết thời gian!");
            }
        }

        // Nếu ở chế độ PvE: Liên tục kiểm tra điều kiện qua màn sạch quái
        if (currentMode.Value == GameMode.PvE && gameActive.Value)
        {
            CheckPvEStageClearCondition();
        }
    }

    void StartGame()
    {
        if (!IsServer) return;
        Debug.Log("Game Started");
        HideEndGamePanelRpc();
        gameTime.Value = gameDuration;
        suddenDeathStarted = false;
        gameActive.Value = true;

        if (MapGenerator.Instance != null)
        {
            int totalMaps = MapGenerator.Instance.mapDatabase.Length;
            if (totalMaps > 0) selectedMapIndex = Random.Range(0, totalMaps);
            MapGenerator.Instance.GenerateMap(selectedMapIndex);
            AutoFitCamera();
        }

        int maxPlayers = 4;
        int currentPlayerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        // Mảng theo dõi góc nào đã có người đứng
        bool[] occupiedCorners = new bool[4];

        // 1. DỊCH CHUYỂN NGƯỜI CHƠI THẬT (Dựa trên ID Mạng để không bao giờ bị trùng)
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null && client.PlayerObject.TryGetComponent(out PlayerMovement pm))
            {
                int cornerIndex = (int)(client.ClientId % 4);
                Vector2 startPos = GetCornerSpawnPosition(cornerIndex);
                pm.ForceTeleport(startPos);
                occupiedCorners[cornerIndex] = true; // Đánh dấu góc này đã có chủ
            }
        }

        // 2. SINH RA BOT ĐỂ LẤP ĐẦY (Chỉ tìm các góc chưa có người)
        int botsToSpawn = maxPlayers - currentPlayerCount;
        for (int i = 0; i < botsToSpawn; i++)
        {
            int botCorner = 0;
            for (int c = 0; c < 4; c++)
            {
                if (!occupiedCorners[c])
                {
                    botCorner = c;
                    occupiedCorners[c] = true;
                    break;
                }
            }
            Vector2 botPos = GetCornerSpawnPosition(botCorner);
            GameObject bot = Instantiate(botPrefab, botPos, Quaternion.identity);
            bot.GetComponent<NetworkObject>().Spawn();
            
        }
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
        if (newValue == true)
        {
            gameInfoText.text = $"{gameTime.Value:F1}";

            // KHI HOST BẤM ENTER -> PHÁT SOUND GAMESTART VÀ NHẠC THUYỀN CHIẾN
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.gameStartSFX);
                AudioManager.Instance.PlayBGM(AudioManager.Instance.gameplayBGM);
            }
        }
        else
        {
            gameInfoText.text = "Game Over!!";
        }
    }

    // ---  SPAWN LOGIC ---
    private void SpawnCoin()
    {
        if (!gameActive.Value) return;

        int type = RollItemType();

        if (currentMode.Value == GameMode.PvE)
        {
           
            if (type == 0 || type == 1 || type == 2)
            {
                return; 
            }
        }

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
            itemScript.coinType.Value = type;
        }
    }

    private int RollItemType()
    {
        float r = Random.Range(0f, 100f);

        if (r > 98f) return 6;   // Rare
        if (r > 94f) return 10;  // Invincible
        if (r > 90f) return 9;   // Invisible
        if (r > 84f) return 8;   // Reverse controls
        if (r > 78f) return 7;   // Slow
        if (r > 70f) return 1;   // Diamond
        if (r > 62f) return 2;   // Trap
        if (r > 52f) return 5;   // Fire
        if (r > 42f) return 4;   // Bomb Up
        if (r > 32f) return 3;   // Speed

        return 0;                // Coin
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
                player.Die(player.OwnerClientId);
            }
        }

        GameObject wall = Instantiate(wallPrefab, pos, Quaternion.identity);
        wall.GetComponent<NetworkObject>().Spawn();
        if (MapGenerator.Instance != null) MapGenerator.Instance.RegisterWall(wall);
    }

    // --- SUDDEN DEATH SPIRAL ---
    //
    IEnumerator SuddenDeathRoutine()
    {
        suddenDeathStarted = true;
        Debug.Log("SUDDEN DEATH STARTED!");

        StopCoroutine(SpawnCoinRoutine());

        int minX = Mathf.RoundToInt(spawnAreaMin.x);
        int maxX = Mathf.RoundToInt(spawnAreaMax.x);
        int minY = Mathf.RoundToInt(spawnAreaMin.y);
        int maxY = Mathf.RoundToInt(spawnAreaMax.y);

        // Use a HashSet to ensure we don't pick the same spot twice (corners)
        HashSet<Vector2> currentRingPositions = new HashSet<Vector2>();

        while (gameActive.Value)
        {
            int currentWidth = maxX - minX;
            int currentHeight = maxY - minY;

            // If BOTH dimensions are small enough, stop!
            if (currentWidth <= finalArenaSize && currentHeight <= finalArenaSize)
            {
                break;
            }

            currentRingPositions.Clear();

            // 1. CHECK HEIGHT: Only shrink Y if it's still too tall
            bool shrinkY = currentHeight > finalArenaSize;
            if (shrinkY)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    currentRingPositions.Add(new Vector2(x, minY));
                    currentRingPositions.Add(new Vector2(x, maxY));
                }
            }

            // 2. CHECK WIDTH: Only shrink X if it's still too wide
            bool shrinkX = currentWidth > finalArenaSize;
            if (shrinkX)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    currentRingPositions.Add(new Vector2(minX, y));
                    currentRingPositions.Add(new Vector2(maxX, y));
                }
            }

            // --- WARNING PHASE ---
            List<GameObject> activeWarnings = new List<GameObject>();
            if (warningPrefab != null)
            {
                foreach (Vector2 pos in currentRingPositions)
                {
                    GameObject warning = Instantiate(warningPrefab, pos, Quaternion.identity);
                    if (warning.TryGetComponent(out Unity.Netcode.NetworkObject netObj)) netObj.Spawn();
                    activeWarnings.Add(warning);
                }
            }

            yield return new WaitForSeconds(3.0f);

            // --- CRUSH PHASE ---
            foreach (var warn in activeWarnings)
            {
                if (warn != null)
                {
                    if (warn.TryGetComponent(out Unity.Netcode.NetworkObject netObj) && netObj.IsSpawned)
                        netObj.Despawn();
                    else
                        Destroy(warn);
                }
            }

            foreach (Vector2 pos in currentRingPositions)
            {
                SpawnWall((int)pos.x, (int)pos.y);
            }

            // 3. UPDATE INDICES (Only shrink the specific sides we worked on)
            if (shrinkY) { minY++; maxY--; }
            if (shrinkX) { minX++; maxX--; }

            spawnAreaMin = new Vector2(minX, minY);
            spawnAreaMax = new Vector2(maxX, maxY);

            yield return new WaitForSeconds(2.0f);
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

        string winnerText = ScoreBoardManager.Instance.GetWinnerName();
        UpdateWinnerRpc(winnerText);
        ShowEndGamePanelRpc("MATCH COMPLETE", winnerText, true, "MAIN MENU");

        if (MapGenerator.Instance != null) MapGenerator.Instance.ClearCurrentMap();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void UpdateWinnerRpc(string winnerText)
    {
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

    // Hàm này tự động tính toán 4 góc an toàn bên trong bức tường bao quanh
    public Vector2 GetCornerSpawnPosition(int playerIndex)
    {
        // Lấy tọa độ Min Max mà bạn đã set trong GameManager
        float minX = Mathf.Round(spawnAreaMin.x);
        float maxX = Mathf.Round(spawnAreaMax.x);
        float minY = Mathf.Round(spawnAreaMin.y);
        float maxY = Mathf.Round(spawnAreaMax.y);

        // Cộng trừ 1 để nhân vật đứng lùi vào trong, không bị đè lên bức tường viền ngoài cùng
        Vector2[] corners = new Vector2[] {
            new Vector2(minX + 1, maxY - 1), // Góc 0: Trên cùng bên trái
            new Vector2(maxX - 1, minY + 1), // Góc 1: Dưới cùng bên phải
            new Vector2(minX + 1, minY + 1), // Góc 2: Dưới cùng bên trái
            new Vector2(maxX - 1, maxY - 1)  // Góc 3: Trên cùng bên phải
        };

        return corners[playerIndex % 4]; // Trả về vị trí tương ứng với thứ tự Player
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayGlobalSFXRpc(int soundID)
    {
        if (AudioManager.Instance == null) return;

        if (soundID == 1) // ID 1 = Nhặt item
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.collectItemSFX);
        }
    }

    public void AutoFitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Tính toán chiều rộng và chiều cao của Map hiện tại
        float mapWidth = spawnAreaMax.x - spawnAreaMin.x;
        float mapHeight = spawnAreaMax.y - spawnAreaMin.y;

        // Viền đệm (Padding) để map không dính sát rạt vào mép màn hình
        float padding = 3f;

        // Tính toán Size theo tỷ lệ màn hình (Aspect Ratio)
        float orthoSizeY = (mapHeight / 2f) + padding;
        float orthoSizeX = ((mapWidth / 2f) + padding) / cam.aspect;

        // Lấy kích thước lớn nhất để đảm bảo Map không bị cắt xén
        cam.orthographicSize = Mathf.Max(orthoSizeX, orthoSizeY);

        // Đưa Camera về đúng tâm của Map
        float centerX = (spawnAreaMax.x + spawnAreaMin.x) / 2f;
        float centerY = (spawnAreaMax.y + spawnAreaMin.y) / 2f;
        cam.transform.position = new Vector3(centerX, centerY, -10f); // -10f là z offset mặc định của 2D
    }

    // Hàm khởi tạo chế độ chơi PvE từ màn hình chọn (Gọi bởi Host)
    public void SetupPvEModeSelection(GameMode selectedMode)
    {
        if (!IsServer) return;
        currentMode.Value = selectedMode;
        Debug.Log($"Chế độ chơi được set thành: {currentMode.Value}");
    }

    // Hàm bắt đầu một Ải PvE cụ thể
    public void StartPvEStage(PvEStage stage)
    {
        if (!IsServer) return;

        HideEndGamePanelRpc();

        currentStage.Value = stage;
        gameActive.Value = true;
        suddenDeathStarted = false; // Tắt vòng boSudden Death ở chế độ PvE

        // Thiết lập thời gian cho từng ải (Ví dụ: ải nhỏ 90s, Boss 180s)
        gameTime.Value = (stage == PvEStage.Stage3_Boss) ? 180f : 90f;

        // 1. Dọn dẹp bản đồ cũ
        if (MapGenerator.Instance != null) MapGenerator.Instance.ClearCurrentMap();
        ClearExistingMonsters();

        foreach (Coin item in FindObjectsByType<Coin>(FindObjectsSortMode.None)) { if (item.IsSpawned) item.GetComponent<NetworkObject>().Despawn(); }
        foreach (Bomb bomb in FindObjectsByType<Bomb>(FindObjectsSortMode.None)) { if (bomb.IsSpawned) bomb.GetComponent<NetworkObject>().Despawn(); }
        foreach (Explosion exp in FindObjectsByType<Explosion>(FindObjectsSortMode.None)) { if (exp.IsSpawned) exp.GetComponent<NetworkObject>().Despawn(); }

        // 2. Sinh bản đồ tương ứng cho từng ải
        int mapIndex = 0;
        if (stage == PvEStage.Stage1) mapIndex = 0; // Kéo map 1 vào database
        else if (stage == PvEStage.Stage2) mapIndex = 1; // Map 2
        else if (stage == PvEStage.Stage3_Boss) mapIndex = 2; // Map Boss

        if (MapGenerator.Instance != null)
        {
            MapGenerator.Instance.GenerateMap(mapIndex);
            AutoFitCamera(); 
        }

        // 3. Dịch chuyển toàn bộ người chơi về vị trí an toàn (Góc map)
        PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        totalConnectedPlayers = allPlayers.Length;

        foreach (PlayerMovement player in allPlayers)
        {
            // Ép buộc dùng ID mạng để chia góc, giải quyết 100% việc đè nhau khi qua Ải
            player.ResetStats();
            int spawnIndex = (int)(player.OwnerClientId % 4);
            Vector2 spawnPos = GetCornerSpawnPosition(spawnIndex);
            player.ForceTeleport(spawnPos);
        }

        // 4. KÍCH HOẠT HÀM SINH QUÁI (Sẽ viết ở Bước 2)
        SpawnMonstersForStage(stage);

        Debug.Log($"--- BẮT ĐẦU PVE: {stage} ---");
    }

    // Kiểm tra xem quái trên map đã chết hết chưa
    private void CheckPvEStageClearCondition()
    {
        // Loại bỏ các phần tử quái bị hủy khỏi danh sách kiểm tra
        spawnedMonsters.RemoveAll(item => item == null);

        if (spawnedMonsters.Count == 0)
        {
            // Đã tiêu diệt sạch toàn bộ quái trong ải hiện tại!
            AdvanceToNextStage();
        }
    }

    // Hàm tự động chuyển sang Ải tiếp theo hoặc kích hoạt Chiến Thắng
    private void AdvanceToNextStage()
    {
        if (currentStage.Value == PvEStage.Stage1)
        {
            StartCoroutine(StageTransitionRoutine(PvEStage.Stage2, "STAGE 1 CLEAR!", "Next wave is coming..."));
        }
        else if (currentStage.Value == PvEStage.Stage2)
        {
            StartCoroutine(StageTransitionRoutine(PvEStage.Stage3_Boss, "STAGE 2 CLEAR!", "Warning: boss approaching!"));
        }
        else if (currentStage.Value == PvEStage.Stage3_Boss)
        {
            currentStage.Value = PvEStage.Victory;
            gameActive.Value = false;
            UpdateGameInfoTextRpc("VICTORY! YOU DEFEATED THE BOSS!");
            ShowEndGamePanelRpc("VICTORY!", "You defeated the boss!", true, "MAIN MENU");
        }
    }

    IEnumerator StageTransitionRoutine(PvEStage nextStage, string title, string message)
    {
        // 1. Tạm dừng game và báo hiệu
        gameActive.Value = false;
        UpdateGameInfoTextRpc($"{title} {message}");
        ShowEndGamePanelRpc(title, message, false, "MAIN MENU");

        // 2. Dọn dẹp sạch sẽ map cũ
        if (MapGenerator.Instance != null) MapGenerator.Instance.ClearCurrentMap();
        ClearExistingMonsters();

        // 3. Cho người chơi nghỉ ngơi 3.5 giây để cắn bình máu hoặc chuẩn bị tinh thần
        yield return new WaitForSeconds(3.5f);

        // 4. Bắt đầu ải tiếp theo
        HideEndGamePanelRpc();
        StartPvEStage(nextStage);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void UpdateGameInfoTextRpc(string msg)
    {
        if (gameInfoText != null) gameInfoText.text = msg;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowEndGamePanelRpc(string title, string message, bool showMainMenuButton, string buttonLabel)
    {
        GetOrCreateEndGamePanel().ShowResult(title, message, showMainMenuButton, buttonLabel);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideEndGamePanelRpc()
    {
        GetOrCreateEndGamePanel().Hide();
    }

    // Xử lý khi người chơi thua cuộc
    public void TriggerPvEDefeat(string reason)
    {
        if (!IsServer || currentStage.Value == PvEStage.Defeat) return;

        currentStage.Value = PvEStage.Defeat;
        gameActive.Value = false;
        Debug.Log($"THUA CUỘC PvE! Lý do: {reason}");

        // Gọi RPC hiển thị Panel Bỏ phiếu Chơi lại / Rời phòng lên màn hình UI của mọi người
        voteReplayCount = 0;
        UpdateGameInfoTextRpc("DEFEAT!");
        ShowEndGamePanelRpc("DEFEAT", reason, true, "MAIN MENU");
    }

    // Hàm dùng để quản lý thực thể quái (Sẽ gọi từ script Quái ở Bước 2)
    public void RegisterMonster(GameObject monster)
    {
        if (monster != null && !spawnedMonsters.Contains(monster))
        {
            spawnedMonsters.Add(monster);
        }
    }

    private void ClearExistingMonsters()
    {
        foreach (var monster in spawnedMonsters)
        {
            if (monster != null)
            {
                if (monster.GetComponent<NetworkObject>().IsSpawned)
                    monster.GetComponent<NetworkObject>().Despawn(true);
            }
        }
        spawnedMonsters.Clear();
    }

    // --- HỆ THỐNG VOTE (BỎ PHIẾU CHƠI LẠI) ---
    [Rpc(SendTo.Everyone)]
    private void OpenReplayVotePanelRpc()
    {
        // Logic UI hiển thị bảng thông báo: "Bạn có muốn chơi lại không?" 
        // Gồm 2 nút: [Chơi Lại] và [Rời Đi]
        // Nút [Chơi Lại] sẽ gọi hàm: SubmitVoteServerRpc(true);
        // Nút [Rời Đi] sẽ gọi hàm: SubmitVoteServerRpc(false);
    }

    [Rpc(SendTo.Server)]
    public void SubmitVoteServerRpc(bool voteReplay, RpcParams rpcParams = default)
    {
        if (voteReplay)
        {
            voteReplayCount++;
            if (voteReplayCount >= totalConnectedPlayers)
            {
                // Toàn bộ người chơi đồng ý chơi lại -> Khởi động lại từ Ải 1
                StartPvEStage(PvEStage.Stage1);
                CloseVotePanelRpc();
            }
        }
        else
        {
            // Có người bấm rời đi -> Ngắt kết nối mạng mạng đưa người đó về Menu sảnh
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (clientId == NetworkManager.ServerClientId)
            {
                // Nếu Host rời phòng -> Sập phòng luôn
                NetworkManager.Singleton.Shutdown();
            }
            else
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void CloseVotePanelRpc()
    {
        // UI logic: Ẩn bảng vote đi để tiếp tục chơi ván mới
    }

    private void SpawnMonstersForStage(PvEStage stage)
    {
        int monsterCount = 0;

        if (stage == PvEStage.Stage1) monsterCount = 6;
        else if (stage == PvEStage.Stage2) monsterCount = 12;
        else if (stage == PvEStage.Stage3_Boss)
        {
            // TÌM TỌA ĐỘ CHÍNH GIỮA BẢN ĐỒ ĐỂ THẢ BOSS
            Vector2 centerPos = new Vector2((spawnAreaMin.x + spawnAreaMax.x) / 2f, (spawnAreaMin.y + spawnAreaMax.y) / 2f);
            centerPos = new Vector2(Mathf.Round(centerPos.x), Mathf.Round(centerPos.y));

            GameObject boss = Instantiate(bossPrefab, centerPos, Quaternion.identity);
            boss.GetComponent<NetworkObject>().Spawn();

            RegisterMonster(boss); // Đăng ký Boss như một quái vật để CheckPvEStageClearCondition hoạt động
            return; // Đẻ Boss xong thì thoát hàm
        }
        else return;

        for (int i = 0; i < monsterCount; i++)
        {
            Vector2 spawnPos = GetMonsterSpawnPosition();

            if (spawnPos != Vector2.zero)
            {
                GameObject monster = Instantiate(basicMonsterPrefab, spawnPos, Quaternion.identity);
                monster.GetComponent<NetworkObject>().Spawn();
                RegisterMonster(monster);
            }
        }
    }

    public Vector2 GetMonsterSpawnPosition()
    {
        for (int i = 0; i < 50; i++) // Quét tối đa 50 lần để tìm ô đẹp
        {
            Vector2 potentialPos = GetSafeSpawnPosition();
            if (potentialPos == Vector2.zero) continue;

            bool isSafeFromPlayers = true;
            // Kiểm tra xem vị trí này có quá gần 4 góc của người chơi không (Bán kính 4 ô)
            for (int c = 0; c < 4; c++)
            {
                if (Vector2.Distance(potentialPos, GetCornerSpawnPosition(c)) < 4.0f)
                {
                    isSafeFromPlayers = false;
                    break;
                }
            }

            if (isSafeFromPlayers) return potentialPos; // Tìm được ô hoàn hảo!
        }

        // Nếu map quá chật, đành lấy tạm ô an toàn bất kỳ
        return GetSafeSpawnPosition();
    }

    public void ReturnToMainMenu()
    {
        // Ngắt kết nối mạng một cách an toàn
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Dọn dẹp nhạc (Tùy chọn)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(AudioManager.Instance.mainMenuBGM);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    private EndGamePanelUI GetOrCreateEndGamePanel()
    {
        if (endGamePanel != null)
            return endGamePanel;

        endGamePanel = FindFirstObjectByType<EndGamePanelUI>(FindObjectsInactive.Include);

        if (endGamePanel == null)
        {
            GameObject panelObject = new GameObject("EndGamePanelUI");
            endGamePanel = panelObject.AddComponent<EndGamePanelUI>();
        }

        return endGamePanel;
    }

}
