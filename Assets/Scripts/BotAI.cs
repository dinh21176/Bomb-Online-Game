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
    [SerializeField] TextMeshPro nameText;

    [Header("Layers")]
    [SerializeField] LayerMask obstacleLayer;
    [SerializeField] LayerMask softWallLayer;
    [SerializeField] LayerMask playerLayer;
    [SerializeField] LayerMask itemLayer;

    public NetworkVariable<FixedString32Bytes> botName = new NetworkVariable<FixedString32Bytes>("Bot");
    public NetworkVariable<int> botScore = new NetworkVariable<int>(0);

    private Animator animator;
    private Collider2D col;
    private SpriteRenderer[] renderers;
    private Vector2 targetPosition;

    private Coroutine thinkingCoroutine;
    public bool isDead = false;
    public int explosionRange = 2;

    private float lastBombTime = 0f;
    private float bombCooldown = 2.5f;
    private float stuckTimer = 0f;

    private Vector2 lastClientPos;
    private Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();

        if (IsServer) botName.Value = "Bot " + Random.Range(100, 999);
        botName.OnValueChanged += (oldValue, newValue) => { if (nameText != null) nameText.text = newValue.ToString(); };
        if (nameText != null) nameText.text = botName.Value.ToString();

        if (!IsServer)
        {
            lastClientPos = transform.position;
            return;
        }

        SnapToGrid();
    }

    private void Update()
    {
        if (isDead) return;

        if (!IsServer)
        {
            Vector2 currentPos = transform.position;
            float dist = Vector2.Distance(currentPos, lastClientPos);

            // Nếu khoảng cách thay đổi đủ lớn (đang di chuyển)
            if (dist > 0.001f)
            {
                Vector2 moveDir = (currentPos - lastClientPos).normalized;
                if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y)) moveDir = new Vector2(Mathf.Sign(moveDir.x), 0);
                else moveDir = new Vector2(0, Mathf.Sign(moveDir.y));

                UpdateAnimations(moveDir);
            }
            else
            {
                UpdateAnimations(Vector2.zero); // Đứng im
            }

            lastClientPos = currentPos;
            return;
        }

        if (GameManager.Instance != null && !GameManager.Instance.gameActive.Value)
        {
            UpdateAnimations(Vector2.zero);
            return;
        }

        Vector2 currentGridPos = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));

        if (IsTileDangerous(targetPosition) || IsTileDangerous(currentGridPos))
        {
            Vector2 escape = FindEscapeRoute(currentGridPos);
            if (escape != currentGridPos) targetPosition = escape;
        }

        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // Trực tiếp update animation cho Host
        Vector2 moveDirServer = (targetPosition - (Vector2)transform.position).normalized;
        UpdateAnimations(moveDirServer);

        if (Vector2.Distance(transform.position, targetPosition) <= 0.05f)
        {
            stuckTimer = 0f;
            transform.position = targetPosition;

            if (thinkingCoroutine != null) StopCoroutine(thinkingCoroutine);
            thinkingCoroutine = StartCoroutine(ThinkRoutine());
        }
        else
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 0.4f)
            {
                stuckTimer = 0f;
                SnapToGrid();
                if (thinkingCoroutine != null) StopCoroutine(thinkingCoroutine);
                thinkingCoroutine = StartCoroutine(ThinkRoutine());
            }
        }
    }

    IEnumerator ThinkRoutine()
    {
        Vector2 currentPos = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));

        if (IsTileDangerous(currentPos))
        {
            Vector2 escape = FindEscapeRoute(currentPos);
            targetPosition = (escape != currentPos) ? escape : FindRandomValidDirection(currentPos);
        }
        else if (FindNearbyTarget(currentPos, itemLayer, out Vector2 itemPos))
        {
            targetPosition = GetNextMoveToBFS(currentPos, itemPos);
        }
        else if (FindNearbyTarget(currentPos, playerLayer, out Vector2 playerPos) && Vector2.Distance(currentPos, playerPos) > 1f)
        {
            targetPosition = GetNextMoveToBFS(currentPos, playerPos);
        }
        else if (IsNextToTarget(currentPos, softWallLayer) || IsNextToTarget(currentPos, playerLayer))
        {
            if (Time.time - lastBombTime >= bombCooldown && CanEscapeAfterPlanting(currentPos))
            {
                PlantBomb(currentPos);
                targetPosition = FindEscapeRoute(currentPos, currentPos);
            }
            else targetPosition = FindRandomValidDirection(currentPos);
        }
        else targetPosition = FindRandomValidDirection(currentPos);

        yield return null;
    }

    Vector2 GetNextMoveToBFS(Vector2 startPos, Vector2 targetPos)
    {
        Queue<Vector2> queue = new Queue<Vector2>();
        Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        queue.Enqueue(startPos);
        cameFrom[startPos] = startPos;
        bool found = false;
        int maxLoops = 200;
        int loopCount = 0;

        while (queue.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;
            Vector2 curr = queue.Dequeue();
            if (curr == targetPos) { found = true; break; }
            foreach (Vector2 dir in directions)
            {
                Vector2 next = curr + dir;
                if (!cameFrom.ContainsKey(next) && !Physics2D.OverlapCircle(next, 0.4f, obstacleLayer) && !IsTileDangerous(next))
                {
                    cameFrom[next] = curr;
                    queue.Enqueue(next);
                }
            }
        }
        if (found)
        {
            Vector2 step = targetPos;
            while (cameFrom.ContainsKey(step) && cameFrom[step] != startPos) step = cameFrom[step];
            return step;
        }
        return FindRandomValidDirection(startPos);
    }

    Vector2 FindEscapeRoute(Vector2 startPos, Vector2? simulatedBombPos = null)
    {
        Queue<Vector2> queue = new Queue<Vector2>();
        Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        queue.Enqueue(startPos);
        cameFrom[startPos] = startPos;
        Vector2 safeSpot = startPos;
        int maxLoops = 200;
        int loopCount = 0;

        while (queue.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;
            Vector2 curr = queue.Dequeue();
            if (!IsTileDangerous(curr, simulatedBombPos)) { safeSpot = curr; break; }
            foreach (Vector2 dir in directions)
            {
                Vector2 next = curr + dir;
                if (!cameFrom.ContainsKey(next) && !Physics2D.OverlapCircle(next, 0.4f, obstacleLayer))
                {
                    cameFrom[next] = curr;
                    queue.Enqueue(next);
                }
            }
        }
        if (safeSpot == startPos) return startPos;
        Vector2 step = safeSpot;
        while (cameFrom.ContainsKey(step) && cameFrom[step] != startPos) step = cameFrom[step];
        return step;
    }

    bool CanEscapeAfterPlanting(Vector2 pos) { return FindEscapeRoute(pos, pos) != pos; }

    bool IsTileDangerous(Vector2 pos, Vector2? simulatedBombPos = null)
    {
        // --- ĐÃ FIX: CHẠM VÀO TIA LỬA THẬT (Bắt bằng vật lý để chống hở frame) ---
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, 0.4f);
        foreach (var hit in hits)
        {
            // Kiểm tra xem tại vị trí này có bất kỳ Collider nào là tia lửa không
            if (hit.GetComponent<Explosion>() != null || hit.CompareTag("Explosion") || hit.name.Contains("Explosion"))
            {
                return true;
            }
        }

        // --- CHECK BOM SẮP NỔ ---
        Bomb[] allBombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
        List<Vector2> dangerCenters = new List<Vector2>();
        foreach (Bomb bomb in allBombs) dangerCenters.Add(new Vector2(Mathf.Round(bomb.transform.position.x), Mathf.Round(bomb.transform.position.y)));
        if (simulatedBombPos.HasValue) dangerCenters.Add(simulatedBombPos.Value);

        foreach (Vector2 center in dangerCenters)
        {
            if (pos == center) return true;
            foreach (Vector2 dir in directions)
            {
                for (int i = 1; i <= 6; i++)
                {
                    Vector2 checkPos = center + (dir * i);
                    if (pos == checkPos) return true;
                    if (Physics2D.OverlapCircle(checkPos, 0.4f, obstacleLayer) || Physics2D.OverlapCircle(checkPos, 0.4f, softWallLayer)) break;
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

    void SnapToGrid() { targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y)); }

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

    public void Die()
    {
        if (!IsServer || isDead) return;
        if (thinkingCoroutine != null) StopCoroutine(thinkingCoroutine);
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        isDead = true;
        SetBotVisualsRpc(false);
        transform.position = new Vector2(-999, -999);
        yield return new WaitForSeconds(3f);
        if (GameManager.Instance != null && GameManager.Instance.gameActive.Value)
        {
            Vector2 safePos = GameManager.Instance.GetSafeSpawnPosition();
            if (safePos != Vector2.zero)
            {
                transform.position = safePos;
                SnapToGrid();
            }
        }
        SetBotVisualsRpc(true);
        isDead = false;
    }

    [Rpc(SendTo.Everyone)]
    void SetBotVisualsRpc(bool state)
    {
        if (col != null) col.enabled = state;
        foreach (var r in renderers) r.enabled = state;
        if (nameText != null) nameText.enabled = state;
    }

    public void UpgradeStat(int statType)
    {
        if (!IsServer) return;
        switch (statType)
        {
            case 0: moveSpeed += 0.5f; if (moveSpeed > 7f) moveSpeed = 7f; break;
            case 1: bombCooldown -= 0.5f; if (bombCooldown < 0.5f) bombCooldown = 0.5f; break;
            case 2: explosionRange += 1; if (explosionRange > 6) explosionRange = 6; break;
            case 3:
                moveSpeed += 0.5f; if (moveSpeed > 7f) moveSpeed = 7f;
                bombCooldown -= 0.5f; if (bombCooldown < 0.5f) bombCooldown = 0.5f;
                explosionRange += 1; if (explosionRange > 6) explosionRange = 6; break;
        }
    }
}