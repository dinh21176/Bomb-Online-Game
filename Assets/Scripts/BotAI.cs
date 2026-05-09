using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;

public class BotAI : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] float moveSpeed = 4.5f;
    [SerializeField] GameObject bombPrefab;
    [SerializeField] TextMeshPro nameText; // Kéo thả text hiển thị tên của Bot vào đây

    [Header("Layers")]
    [SerializeField] LayerMask obstacleLayer;
    [SerializeField] LayerMask softWallLayer;
    [SerializeField] LayerMask playerLayer;
    [SerializeField] LayerMask itemLayer;

    // --- BIẾN MẠNG ĐỂ QUẢN LÝ ĐIỂM VÀ TÊN ---
    public NetworkVariable<FixedString32Bytes> botName = new NetworkVariable<FixedString32Bytes>("Bot");
    public NetworkVariable<int> botScore = new NetworkVariable<int>(0);

    private Animator animator;
    private Collider2D col;
    private SpriteRenderer[] renderers;
    private Vector2 targetPosition;

    private bool isThinking = false;
    public bool isDead = false;
    public int explosionRange = 2;

    private float lastBombTime = 0f;
    private float bombCooldown = 2.5f;
    private float stuckTimer = 0f;

    private Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();

        // Cập nhật tên hiển thị cho Bot ("Bot 1", "Bot 2"...)
        if (IsServer)
        {
            botName.Value = "Bot " + Random.Range(100, 999);
        }

        botName.OnValueChanged += (oldValue, newValue) => { if (nameText != null) nameText.text = newValue.ToString(); };
        if (nameText != null) nameText.text = botName.Value.ToString();

        if (!IsServer) { enabled = false; return; }

        SnapToGrid();
    }

    private void Update()
    {
        if (!IsServer || isDead) return;

        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        Vector2 moveDir = (targetPosition - (Vector2)transform.position).normalized;
        UpdateAnimations(moveDir);

        if (Vector2.Distance(transform.position, targetPosition) <= 0.05f)
        {
            stuckTimer = 0f;
            transform.position = targetPosition;
            if (!isThinking) StartCoroutine(ThinkRoutine());
        }
        else
        {
            stuckTimer += Time.deltaTime;
            // GIẢM THỜI GIAN KẸT XUỐNG 0.15s ĐỂ BOT VÙNG VẪY NHANH HƠN[cite: 13]
            if (stuckTimer > 0.15f)
            {
                stuckTimer = 0f;
                SnapToGrid(); // Ép về lại tâm lưới
                if (!isThinking) StartCoroutine(ThinkRoutine());
            }
        }
    }

    IEnumerator ThinkRoutine()
    {
        isThinking = true;
        yield return new WaitForSeconds(Random.Range(0.1f, 0.2f)); // Suy nghĩ nhanh hơn
        Vector2 currentPos = transform.position;

        // 1. NÉ BOM (DÙNG BFS ĐỂ TÌM LỐI THOÁT)
        if (IsTileDangerous(currentPos))
        {
            Vector2 safeTile = FindSafeSpotBFS(currentPos);
            targetPosition = GetNextMoveTo(currentPos, safeTile);
        }
        // 2. NHẶT ĐỒ
        else if (FindNearbyTarget(currentPos, itemLayer, out Vector2 itemPos))
        {
            targetPosition = GetNextMoveTo(currentPos, itemPos);
        }
        // 3. TÌM NGƯỜI CHƠI
        else if (FindNearbyTarget(currentPos, playerLayer, out Vector2 playerPos) && Vector2.Distance(currentPos, playerPos) > 1f)
        {
            targetPosition = GetNextMoveTo(currentPos, playerPos);
        }
        // 4. ĐẶT BOM KHÔN NGOAN HƠN (Chỉ đặt khi có lối thoát)
        else if (IsNextToTarget(currentPos, softWallLayer) || IsNextToTarget(currentPos, playerLayer))
        {
            if (Time.time - lastBombTime >= bombCooldown)
            {
                // Giả lập đặt bom xem có đường lui không
                if (CanEscapeAfterPlanting(currentPos))
                {
                    PlantBomb(currentPos);
                    targetPosition = FindSafeSpotBFS(currentPos); // Đặt xong bỏ chạy ngay
                }
                else targetPosition = FindRandomValidDirection(currentPos);
            }
            else targetPosition = FindRandomValidDirection(currentPos);
        }
        else targetPosition = FindRandomValidDirection(currentPos);

        isThinking = false;
    }

    // --- CƠ CHẾ HỒI SINH ---
    public void Die()
    {
        if (!IsServer || isDead) return;
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        isDead = true;
        SetBotVisualsRpc(false); // Ẩn Bot đi
        transform.position = new Vector2(-999, -999); // Ném ra khỏi map

        yield return new WaitForSeconds(3f); // Đợi 3 giây hồi sinh

        if (GameManager.Instance != null)
        {
            Vector2 safePos = GameManager.Instance.GetSafeSpawnPosition();
            if (safePos != Vector2.zero)
            {
                transform.position = safePos;
                SnapToGrid();
            }
        }

        SetBotVisualsRpc(true); // Hiện Bot lại
        isDead = false;
    }

    [Rpc(SendTo.Everyone)]
    void SetBotVisualsRpc(bool state)
    {
        if (col != null) col.enabled = state;
        foreach (var r in renderers) r.enabled = state;
        if (nameText != null) nameText.enabled = state;
    }

    // --- THUẬT TOÁN TÌM ĐƯỜNG BFS CHỐNG NGU MỚI ---
    Vector2 FindSafeSpotBFS(Vector2 startPos)
    {
        Queue<Vector2> queue = new Queue<Vector2>();
        HashSet<Vector2> visited = new HashSet<Vector2>();

        queue.Enqueue(startPos);
        visited.Add(startPos);

        int maxLoops = 30; // Quét tối đa 30 ô để chống giật lag
        int loopCount = 0;

        while (queue.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;
            Vector2 curr = queue.Dequeue();

            // Nếu ô này an toàn tuyệt đối -> Trả về kết quả ngay
            if (!IsTileDangerous(curr)) return curr;

            foreach (Vector2 dir in directions)
            {
                Vector2 next = curr + dir;
                if (!visited.Contains(next) && !Physics2D.OverlapCircle(next, 0.4f, obstacleLayer))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return startPos; // Bó tay, chịu chết
    }

    bool CanEscapeAfterPlanting(Vector2 pos)
    {
        int escapeRoutes = 0;
        foreach (Vector2 dir in directions)
        {
            Vector2 next = pos + dir;
            // Chỉ cần có 1 ô bên cạnh trống (không có tường/bom/người chơi) là Bot sẽ dám đặt bom
            if (!Physics2D.OverlapCircle(next, 0.4f, obstacleLayer) && !Physics2D.OverlapCircle(next, 0.4f, playerLayer))
            {
                escapeRoutes++;
            }
        }
        return escapeRoutes > 0;
    }

    // CÁC HÀM CẢM BIẾN CŨ GIỮ NGUYÊN (SnapToGrid, IsTileDangerous, FindNearbyTarget...)
    void SnapToGrid() { targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y)); }

    bool IsTileDangerous(Vector2 pos)
    {
        Bomb[] allBombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        foreach (Bomb bomb in allBombs)
        {
            Vector2 bombPos = new Vector2(Mathf.Round(bomb.transform.position.x), Mathf.Round(bomb.transform.position.y));
            if (pos == bombPos) return true;
            foreach (Vector2 dir in directions)
            {
                for (int i = 1; i <= 3; i++) // Tầm nổ
                {
                    Vector2 checkPos = bombPos + (dir * i);
                    if (Physics2D.OverlapCircle(checkPos, 0.4f, obstacleLayer) && !Physics2D.OverlapCircle(checkPos, 0.4f, softWallLayer)) break;
                    if (pos == checkPos) return true;
                }
            }
        }
        return false;
    }
    bool FindNearbyTarget(Vector2 startPos, LayerMask targetLayer, out Vector2 targetPos)
    {
        Collider2D hit = Physics2D.OverlapCircle(startPos, 5f, targetLayer);
        if (hit != null && hit.gameObject != this.gameObject)
        {
            targetPos = new Vector2(Mathf.Round(hit.transform.position.x), Mathf.Round(hit.transform.position.y));
            return true;
        }
        targetPos = startPos;
        return false;
    }
    bool IsNextToTarget(Vector2 pos, LayerMask layer)
    {
        foreach (Vector2 dir in directions)
        {
            Collider2D hit = Physics2D.OverlapCircle(pos + dir, 0.4f, layer);
            if (hit != null && hit.gameObject != this.gameObject) return true;
        }
        return false;
    }
    Vector2 GetNextMoveTo(Vector2 startPos, Vector2 targetPos)
    {
        Vector2 bestMove = startPos;
        float minDistance = float.MaxValue;
        foreach (Vector2 dir in directions)
        {
            Vector2 nextPos = startPos + dir;
            if (!Physics2D.OverlapCircle(nextPos, 0.4f, obstacleLayer) && !IsTileDangerous(nextPos))
            {
                float dist = Vector2.Distance(nextPos, targetPos);
                if (dist < minDistance) { minDistance = dist; bestMove = nextPos; }
            }
        }
        return bestMove;
    }
    Vector2 FindRandomValidDirection(Vector2 pos)
    {
        List<Vector2> validSpots = new List<Vector2>();
        foreach (Vector2 dir in directions)
        {
            Vector2 checkPos = pos + dir;
            if (!Physics2D.OverlapCircle(checkPos, 0.4f, obstacleLayer) && !IsTileDangerous(checkPos)) validSpots.Add(checkPos);
        }
        if (validSpots.Count > 0) return validSpots[Random.Range(0, validSpots.Count)];
        return pos;
    }
    void PlantBomb(Vector2 pos)
    {
        if (Physics2D.OverlapCircle(pos, 0.1f, obstacleLayer)) return;
        GameObject bombObj = Instantiate(bombPrefab, pos, Quaternion.identity);
        bombObj.GetComponent<NetworkObject>().Spawn();
        bombObj.GetComponent<Bomb>().Initialize(NetworkObjectId, explosionRange);
        lastBombTime = Time.time;
    }
    void UpdateAnimations(Vector2 moveDir)
    {
        if (animator == null) return;
        if (moveDir.magnitude > 0.1f)
        {
            animator.SetFloat("InputX", moveDir.x);
            animator.SetFloat("InputY", moveDir.y);
            animator.SetBool("IsMoving", true);
        }
        else animator.SetBool("IsMoving", false);
    }

    // --- HÀM HẤP THỤ ITEM SỨC MẠNH ---
    public void UpgradeStat(int statType)
    {
        if (!IsServer) return;

        switch (statType)
        {
            case 0: // Speed (Tăng tốc độ chạy)
                moveSpeed += 0.5f;
                if (moveSpeed > 7f) moveSpeed = 7f; // Giới hạn tốc độ để tránh Bot chạy xuyên tường
                break;

            case 1: // Bomb Up (Giảm thời gian hồi bom, giúp Bot đặt bom nhanh hơn)
                bombCooldown -= 0.5f;
                if (bombCooldown < 0.5f) bombCooldown = 0.5f; // Giới hạn tối thiểu 0.5s để chống lag
                break;

            case 2: // Fire (Tăng tầm nổ của tia lửa)
                explosionRange += 1;
                if (explosionRange > 6) explosionRange = 6; // Giới hạn max 6 ô
                break;

            case 3: // RARE (Buff tất cả chỉ số cùng lúc)
                moveSpeed += 0.5f;
                if (moveSpeed > 7f) moveSpeed = 7f;

                bombCooldown -= 0.5f;
                if (bombCooldown < 0.5f) bombCooldown = 0.5f;

                explosionRange += 1;
                if (explosionRange > 6) explosionRange = 6;
                break;
        }
    }
}