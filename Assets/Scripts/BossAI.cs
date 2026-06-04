using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // Dùng cho thanh máu UI

public class BossAI : NetworkBehaviour
{
    [Header("Boss Stats")]
    public float baseMoveSpeed = 2f;
    public int maxHP = 1000;

    [Header("Prefabs & UI")]
    public Image hpSlider;
    public GameObject warningPrefab;
    public GameObject explosionPrefab;
    public GameObject coinPrefab;

    [Header("Layers")]
    public LayerMask obstacleLayer;

    // --- BIẾN ĐỒNG BỘ MẠNG ---
    public NetworkVariable<int> currentHP = new NetworkVariable<int>(1000);
    public NetworkVariable<bool> isPhase2 = new NetworkVariable<bool>(false);
    public NetworkVariable<Vector2> netMoveDir = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 targetPosition;
    public bool isDead = false;
    private bool isAttacking = false; 

    private bool isInvincible = false;
    private float invincibilityDuration = 1.0f;

    private PlayerMovement currentTarget;
    private bool isResting = false;
    private float stateTimer = 0f;
    private float restDuration = 1.5f;

    private float attackCooldown = 3f;
    private float lastAttackTime = 0f;

    private bool isIntro = true;
    private float introTimer = 4.0f;

    private Vector2 lastClientPos;

    private List<Vector2> activeBossExplosions = new List<Vector2>();

    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Đồng bộ thanh máu cho mọi Client mỗi khi bị đánh
        currentHP.OnValueChanged += (oldVal, newVal) => { if (hpSlider != null) hpSlider.fillAmount = (float)newVal / maxHP; };

        if (IsServer)
        {
            currentHP.Value = maxHP;
            targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
            PickNewTarget(); 
        }
        else
        {
            lastClientPos = transform.position;
        }
    }

    private void Update()
    {
        UpdateVisuals(netMoveDir.Value);

        if (!IsServer || isDead || isAttacking) return;
        if (GameManager.Instance != null && !GameManager.Instance.gameActive.Value) return;

        if (isIntro)
        {
            introTimer -= Time.deltaTime;
            if (introTimer <= 0) isIntro = false;

            netMoveDir.Value = Vector2.zero; // Bắt Boss đứng im
            return;
        }

        // 1. CHU KỲ TẤN CÔNG (SKILL AOE)
        if (Time.time - lastAttackTime > attackCooldown)
        {
            StartCoroutine(CastAoESkillRoutine());
            return;
        }

        // 2. DI CHUYỂN ĐUỔI THEO PLAYER GẦN NHẤT
        if (isResting)
        {
            stateTimer -= Time.deltaTime;
            // Ép Boss đứng im chẵn trên ô lưới khi đang nghỉ
            targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));

            if (stateTimer <= 0)
            {
                isResting = false;
                stateTimer = isPhase2.Value ? 4f : 3f; // Phase 2 sẽ rượt lâu hơn (4 giây)
                PickNewTarget(); // Hết nghỉ thì đổi người khác để rượt
            }
        }
        else
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                isResting = true;
                stateTimer = restDuration; // Nghỉ lấy hơi 1.5 giây
            }
        }

        // 3. DI CHUYỂN
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, GetCurrentSpeed() * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;

            // Chỉ tìm đường nếu không phải đang nghỉ
            if (!isResting)
            {
                Vector2 bestMove = FindBestMoveTowardsTarget();
                if (bestMove != targetPosition) targetPosition = bestMove;
            }
        }

        // Đồng bộ hướng đi cho Client chạy Animation
        Vector2 moveDirServer = (targetPosition - (Vector2)transform.position).normalized;
        if (moveDirServer.magnitude > 0.1f)
        {
            if (Mathf.Abs(moveDirServer.x) > Mathf.Abs(moveDirServer.y)) moveDirServer = new Vector2(Mathf.Sign(moveDirServer.x), 0);
            else moveDirServer = new Vector2(0, Mathf.Sign(moveDirServer.y));
        }
        else moveDirServer = Vector2.zero;

        netMoveDir.Value = moveDirServer;
    }

    float GetCurrentSpeed()
    {
        return isPhase2.Value ? baseMoveSpeed * 1.5f : baseMoveSpeed; // Phase 2 chạy nhanh hơn 50%
    }

    // --- HÀM BỐC THĂM MỤC TIÊU MỚI ---
    void PickNewTarget()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<PlayerMovement> alivePlayers = new List<PlayerMovement>();

        foreach (var p in players)
        {
            if (!p.isDead.Value) alivePlayers.Add(p);
        }

        if (alivePlayers.Count > 0)
        {
            // Chọn ngẫu nhiên 1 người chơi còn sống để rượt
            currentTarget = alivePlayers[Random.Range(0, alivePlayers.Count)];
        }
    }

    // --- TÌM ĐƯỜNG ĐẾN MỤC TIÊU CHỈ ĐỊNH ---
    Vector2 FindBestMoveTowardsTarget()
    {
        if (currentTarget == null || currentTarget.isDead.Value)
        {
            PickNewTarget();
            return targetPosition;
        }

        Vector2 startPos = targetPosition;
        Vector2 targetPos = new Vector2(Mathf.Round(currentTarget.transform.position.x), Mathf.Round(currentTarget.transform.position.y));

        Queue<Vector2> queue = new Queue<Vector2>();
        Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();

        queue.Enqueue(startPos);
        cameFrom[startPos] = startPos;
        bool found = false;
        int maxLoops = 200; // Tránh treo máy
        int loopCount = 0;
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        while (queue.Count > 0 && loopCount < maxLoops)
        {
            loopCount++;
            Vector2 curr = queue.Dequeue();

            if (curr == targetPos)
            {
                found = true;
                break;
            }

            foreach (Vector2 dir in dirs)
            {
                Vector2 next = curr + dir;
                // Boss bỏ qua Bom, chỉ bị chặn bởi Tường (obstacleLayer)
                if (!cameFrom.ContainsKey(next) && !Physics2D.OverlapCircle(next, 0.4f, obstacleLayer))
                {
                    cameFrom[next] = curr;
                    queue.Enqueue(next);
                }
            }
        }

        if (found)
        {
            // Truy ngược lại để lấy bước đi ĐẦU TIÊN
            Vector2 step = targetPos;
            while (cameFrom.ContainsKey(step) && cameFrom[step] != startPos)
            {
                step = cameFrom[step];
            }
            return step;
        }

        // Nếu người chơi trốn kĩ quá không tìm được đường, đành đi bừa 1 hướng
        foreach (Vector2 dir in dirs)
        {
            Vector2 next = startPos + dir;
            if (!Physics2D.OverlapCircle(next, 0.4f, obstacleLayer)) return next;
        }

        return startPos; // Bị giam lỏng
    }


    // --- KỸ NĂNG NỔ DIỆN RỘNG (AOE TOÀN BẢN ĐỒ) ---
    IEnumerator CastAoESkillRoutine()
    {
        isAttacking = true;
        netMoveDir.Value = Vector2.zero;
        SetAttackTriggerRpc();

        activeBossExplosions.Clear(); // Làm sạch danh sách bom của Boss

        // --- NÂNG CẤP ĐỘ KHÓ VÀ TẦM ĐÁNH ---
        int totalRandomExplosions = isPhase2.Value ? 30 : 15; // Phẫn nộ thả 30 quả bom ngẫu nhiên
        float warningTime = isPhase2.Value ? 0.65f : 1.0f;    // Thời gian né cực ngắn

        // --- LẤY TỌA ĐỘ RANH GIỚI BẢN ĐỒ (CHỐNG LỖI RA NGOÀI MAP) ---
        int minX = Mathf.RoundToInt(GameManager.Instance.spawnAreaMin.x) + 1;
        int maxX = Mathf.RoundToInt(GameManager.Instance.spawnAreaMax.x) - 1;
        int minY = Mathf.RoundToInt(GameManager.Instance.spawnAreaMin.y) + 1;
        int maxY = Mathf.RoundToInt(GameManager.Instance.spawnAreaMax.y) - 1;

        List<Vector2> warningSpots = new List<Vector2>();

        // 1. Luôn thả 1 quả bom vào chính xác vị trí của từng người chơi đang sống
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (!p.isDead.Value)
            {
                Vector2 pPos = new Vector2(Mathf.Round(p.transform.position.x), Mathf.Round(p.transform.position.y));
                warningSpots.Add(pPos);
            }
        }

        // 2. Thả bom ngẫu nhiên dồn dập khắp bản đồ
        for (int i = 0; i < totalRandomExplosions; i++)
        {
            Vector2 randPos = new Vector2(Random.Range(minX, maxX + 1), Random.Range(minY, maxY + 1));

            // Chỉ thả vào ô trống, không nổ đè lên tường
            if (!Physics2D.OverlapCircle(randPos, 0.4f, obstacleLayer))
            {
                if (!warningSpots.Contains(randPos)) warningSpots.Add(randPos);
            }
        }

        // Hiện cảnh báo nhấp nháy
        List<GameObject> warnings = new List<GameObject>();
        foreach (Vector2 spot in warningSpots)
        {
            GameObject w = Instantiate(warningPrefab, spot, Quaternion.identity);
            w.GetComponent<NetworkObject>().Spawn();
            warnings.Add(w);
        }

        yield return new WaitForSeconds(warningTime); // Thời gian chờ để player né (rất nhanh!)

        // Xóa cảnh báo và tiến hành Nổ
        foreach (var w in warnings) { if (w != null) w.GetComponent<NetworkObject>().Despawn(); }
        foreach (Vector2 spot in warningSpots)
        {
            GameObject exp = Instantiate(explosionPrefab, spot, Quaternion.identity);
            Explosion expScript = exp.GetComponent<Explosion>();
            if (expScript != null) expScript.killerId = 999;

            exp.GetComponent<NetworkObject>().Spawn();

            Collider2D[] hits = Physics2D.OverlapCircleAll(spot, 0.4f);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out PlayerMovement pm)) pm.Die(999);
            }
        }

        lastAttackTime = Time.time;
        attackCooldown = isPhase2.Value ? 3.0f : 4.5f;
        isAttacking = false;
    }


    [Rpc(SendTo.ClientsAndHost)]
    void SetAttackTriggerRpc()
    {
        if (animator != null) animator.SetTrigger("Attack"); 
    }

    // --- XỬ LÝ NHẬN SÁT THƯƠNG ---
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || isDead) return;

        if (collision.GetComponent<Explosion>() != null || collision.CompareTag("Explosion"))
        {
            Explosion exp = collision.GetComponent<Explosion>();

            // NẾU TIA LỬA LÀ DO BOSS TẠO RA -> BỎ QUA KHÔNG TRỪ MÁU!
            if (exp != null && exp.killerId == 999) return;

            if (!isInvincible)
            {
                TakeDamage(100);
                StartCoroutine(InvincibilityRoutine());
            }
        }
        else if (collision.TryGetComponent(out PlayerMovement player))
        {
            player.Die(999);
        }
    }

    // OnTriggerStay2D để đảm bảo Boss hết bất tử mà vẫn dẫm lửa thì sẽ mất máu tiếp
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!IsServer || isDead) return;

        if (collision.GetComponent<Explosion>() != null || collision.CompareTag("Explosion"))
        {
            Explosion exp = collision.GetComponent<Explosion>();
            if (exp != null && exp.killerId == 999) return;

            if (!isInvincible)
            {
                TakeDamage(100);
                StartCoroutine(InvincibilityRoutine());
            }
        }
        else if (collision.TryGetComponent(out PlayerMovement player))
        {
            player.Die(999);
        }
    }

    void TakeDamage(int damage)
    {
        currentHP.Value -= damage;

        // Chuyển Phase 2 khi máu dưới 50%
        if (currentHP.Value <= maxHP / 2 && !isPhase2.Value)
        {
            isPhase2.Value = true;
            SetPhase2VisualsRpc();
        }

        if (currentHP.Value <= 0)
        {
            Die();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetPhase2VisualsRpc()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.red; // Đổi màu thành đỏ sẫm báo hiệu Phẫn nộ
    }

    private void Die()
    {
        isDead = true;
        SetDeathTriggerRpc(); // Gọi hoạt ảnh DEATH
        StartCoroutine(DeathRoutine());
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetDeathTriggerRpc()
    {
        if (animator != null) animator.SetTrigger("Die");
    }

    IEnumerator DeathRoutine()
    {      
        yield return new WaitForSeconds(2f); // Chờ xem hoạt ảnh chết xong
        GetComponent<NetworkObject>().Despawn(true);

        // --- FIX: MƯA COIN TOÀN BẢN ĐỒ ---
        int totalCoins = 40;
        int minX = Mathf.RoundToInt(GameManager.Instance.spawnAreaMin.x) + 1;
        int maxX = Mathf.RoundToInt(GameManager.Instance.spawnAreaMax.x) - 1;
        int minY = Mathf.RoundToInt(GameManager.Instance.spawnAreaMin.y) + 1;
        int maxY = Mathf.RoundToInt(GameManager.Instance.spawnAreaMax.y) - 1;

        for (int i = 0; i < totalCoins; i++)
        {
            Vector2 randPos = new Vector2(Random.Range(minX, maxX + 1), Random.Range(minY, maxY + 1));

            if (!Physics2D.OverlapCircle(randPos, 0.4f, obstacleLayer))
            {
                GameObject coin = Instantiate(coinPrefab, randPos, Quaternion.identity);
                coin.GetComponent<NetworkObject>().Spawn();
            }
        }
    }


    void UpdateVisuals(Vector2 moveDir)
    {
        if (!IsServer)
        {
            Vector2 currentPos = transform.position;
            float dist = Vector2.Distance(currentPos, lastClientPos);
            if (dist > 0.001f)
            {
                moveDir = (currentPos - lastClientPos).normalized;
                if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y)) moveDir = new Vector2(Mathf.Sign(moveDir.x), 0);
                else moveDir = new Vector2(0, Mathf.Sign(moveDir.y));
            }
            else moveDir = Vector2.zero;
            lastClientPos = currentPos;
        }

        if (animator == null || spriteRenderer == null) return;
        if (moveDir.magnitude > 0.1f && !isAttacking)
        {
            animator.SetBool("IsMoving", true);
            if (moveDir.x > 0) spriteRenderer.flipX = true;
            else if (moveDir.x < 0) spriteRenderer.flipX = false;
        }
        else animator.SetBool("IsMoving", false);
    }

    IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        SetInvincibleVisualsRpc(true); // Làm mờ Boss đi để báo hiệu

        yield return new WaitForSeconds(invincibilityDuration); // Chờ 1 giây

        isInvincible = false;
        SetInvincibleVisualsRpc(false); // Sáng lại bình thường
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetInvincibleVisualsRpc(bool state)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            // Nếu đang bất tử thì độ mờ (Alpha) giảm xuống 50%, nếu không thì 100%
            c.a = state ? 0.5f : 1f;
            spriteRenderer.color = c;
        }
    }

}