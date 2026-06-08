using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Components;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] float baseMoveSpeed = 10f;
    [SerializeField] float speedStep = 1f;
    [SerializeField] GameObject bombPrefab;
    [SerializeField] LayerMask bombLayer;

    [Header("Stats")]
    public NetworkVariable<int> maxBombs = new NetworkVariable<int>(1);
    public NetworkVariable<int> explosionRange = new NetworkVariable<int>(1);
    public NetworkVariable<int> speedLevel = new NetworkVariable<int>(0);
    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);

    // Owner writes (Permission.Owner), Everyone reads
    public NetworkVariable<Vector2> netInput = new NetworkVariable<Vector2>(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private int currentActiveBombs = 0;
    private Rigidbody2D rb;
    private Vector2 movement;
    private SpriteRenderer visuals;
    private Collider2D col;
    public Animator animator;

    // Tracks if the Rare Item is active
    private bool isRareModeActive = false;

    // Variables to store original stats
    private int savedBombCount;
    private int savedExplosionRange;

    // Immnunity
    private bool isInvincible = false;
    private int invincibilitySources = 0;

    private bool isSlowActive = false;
    private bool isReverseControlsActive = false;
    private bool visualInvincible = false;
    private bool visualInvisible = false;
    private Coroutine slowRoutine;
    private Coroutine reverseRoutine;
    private Coroutine invisibilityRoutine;
    private Coroutine itemInvincibilityRoutine;

    private const float SLOW_MULTIPLIER = 0.45f;
    private const float SLOW_DURATION = 5f;
    private const float REVERSE_DURATION = 5f;
    private const float INVISIBILITY_DURATION = 6f;
    private const float ITEM_INVINCIBILITY_DURATION = 5f;


    // Constants
    const int ABSOLUTE_MAX_BOMBS = 6;
    const int ABSOLUTE_MAX_RANGE = 6;
    const int ABSOLUTE_MAX_SPEED_LEVEL = 5;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        visuals = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();

        if (animator == null)
        {
            Debug.LogError("ANIMATOR NOT FOUND! .");
        }

        isDead.OnValueChanged += OnDeathStateChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // CHỈ SERVER mới biết kích thước Map để tính góc
        if (IsServer)
        {
            int spawnIndex = (int)(OwnerClientId % 4);
            if (GameManager.Instance != null)
            {
                Vector2 pos = GameManager.Instance.GetCornerSpawnPosition(spawnIndex);

                ForceTeleport(pos);
            }
        }
    }

    private void Update()
    {
        if (isDead.Value)
        {
            if (IsOwner) netInput.Value = Vector2.zero;
            return;
        }

        if (IsOwner)
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");


            if (x != 0)
            {
                y = 0;
            }

            Vector2 currentInput = new Vector2(x, y).normalized;

            if (isReverseControlsActive)
                currentInput *= -1f;

            netInput.Value = currentInput;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryPlantBombServerRpc();
            }
        }
        UpdateAnimations();
    }


    private void FixedUpdate()
    {
        if (!IsOwner) return;

        float currentSpeed = CalculateCurrentSpeed();

        // Use the synced input for movement too
        rb.linearVelocity = netInput.Value * currentSpeed;
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        Vector2 input = netInput.Value;

        // Only update if there is input (prevents snapping to 0,0 for blend tree)
        if (input != Vector2.zero)
        {
            animator.SetFloat("InputX", input.x);
            animator.SetFloat("InputY", input.y);
            animator.SetBool("IsMoving", true);
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }
    }
    private float CalculateCurrentSpeed()
    {
        // 1. Calculate Base Speed
        float speed = baseMoveSpeed + (speedLevel.Value * speedStep);

        // 2. If Rare Mode is active, override with Max Speed
        if (isRareModeActive)
        {
            // Use absolute max level for calculation + small bonus
            speed = baseMoveSpeed + (ABSOLUTE_MAX_SPEED_LEVEL * speedStep) + 2f;
        }

        if (isSlowActive)
            speed *= SLOW_MULTIPLIER;

        return speed;
    }

    [Rpc(SendTo.Server)]
    void TryPlantBombServerRpc()
    {
        if (isDead.Value || currentActiveBombs >= maxBombs.Value) return;
        if (GameManager.Instance != null && !GameManager.Instance.gameActive.Value) return;
        Vector2 spawnPos = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        if (Physics2D.OverlapCircle(spawnPos, 0.1f, bombLayer)) return;

        GameObject bombObj = Instantiate(bombPrefab, spawnPos, Quaternion.identity);
        bombObj.GetComponent<NetworkObject>().Spawn();
        bombObj.GetComponent<Bomb>().Initialize(OwnerClientId, explosionRange.Value);
        currentActiveBombs++;
    }

    public void RestoreBombAmmo()
    {
        currentActiveBombs--;
        if (currentActiveBombs < 0) currentActiveBombs = 0;
    }

    // --- STAT UPGRADES ---

    public void UpgradeStat(int type)
    {
        if (!IsServer) return;

        switch (type)
        {
            case 0: // Speed Item
                // Only upgrade if we aren't already at max level
                if (speedLevel.Value < ABSOLUTE_MAX_SPEED_LEVEL)
                {
                    speedLevel.Value++;
                }
                break;

            case 1: // Bomb Count Up
                // If Rare Mode is active,  upgrade the *saved* stat, keep it after the mode ends
                if (isRareModeActive)
                {
                    if (savedBombCount < ABSOLUTE_MAX_BOMBS) savedBombCount++;
                }
                else
                {
                    if (maxBombs.Value < ABSOLUTE_MAX_BOMBS) maxBombs.Value++;
                }
                break;

            case 2: // Explosion Range Up
                if (isRareModeActive)
                {
                    if (savedExplosionRange < ABSOLUTE_MAX_RANGE) savedExplosionRange++;
                }
                else
                {
                    if (explosionRange.Value < ABSOLUTE_MAX_RANGE) explosionRange.Value++;
                }
                break;

            case 3: // RARE ITEM (God Mode)
                // Start the temporary boost logic on the Server
                StartCoroutine(RarePowerUpRoutine(5f)); // 5 Seconds duration
                break;
        }
    }

    public void ApplyItemEffect(int effectType)
    {
        if (!IsServer) return;

        switch (effectType)
        {
            case 7:
                RestartSlowEffect();
                break;
            case 8:
                RestartReverseEffect();
                break;
            case 9:
                RestartInvisibilityEffect();
                break;
            case 10:
                RestartItemInvincibilityEffect();
                break;
        }
    }

    private void RestartSlowEffect()
    {
        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowEffectRoutine());
    }

    private IEnumerator SlowEffectRoutine()
    {
        SetSlowEffectClientRpc(true);
        yield return new WaitForSeconds(SLOW_DURATION);
        SetSlowEffectClientRpc(false);
        slowRoutine = null;
    }

    private void RestartReverseEffect()
    {
        if (reverseRoutine != null) StopCoroutine(reverseRoutine);
        reverseRoutine = StartCoroutine(ReverseEffectRoutine());
    }

    private IEnumerator ReverseEffectRoutine()
    {
        SetReverseEffectClientRpc(true);
        yield return new WaitForSeconds(REVERSE_DURATION);
        SetReverseEffectClientRpc(false);
        reverseRoutine = null;
    }

    private void RestartInvisibilityEffect()
    {
        if (invisibilityRoutine != null)
        {
            StopCoroutine(invisibilityRoutine);
            SetInvisibilityVisualsRpc(false);
        }

        invisibilityRoutine = StartCoroutine(InvisibilityEffectRoutine());
    }

    private IEnumerator InvisibilityEffectRoutine()
    {
        SetInvisibilityVisualsRpc(true);
        yield return new WaitForSeconds(INVISIBILITY_DURATION);
        SetInvisibilityVisualsRpc(false);
        invisibilityRoutine = null;
    }

    private void RestartItemInvincibilityEffect()
    {
        if (itemInvincibilityRoutine != null)
        {
            StopCoroutine(itemInvincibilityRoutine);
            RemoveInvincibilitySource();
        }

        itemInvincibilityRoutine = StartCoroutine(ItemInvincibilityEffectRoutine());
    }

    private IEnumerator ItemInvincibilityEffectRoutine()
    {
        AddInvincibilitySource();
        yield return new WaitForSeconds(ITEM_INVINCIBILITY_DURATION);
        RemoveInvincibilitySource();
        itemInvincibilityRoutine = null;
    }

    [Rpc(SendTo.Owner)]
    private void SetSlowEffectClientRpc(bool active)
    {
        isSlowActive = active;
    }

    [Rpc(SendTo.Owner)]
    private void SetReverseEffectClientRpc(bool active)
    {
        isReverseControlsActive = active;
    }

    // --- RARE ITEM LOGIC ---

    IEnumerator RarePowerUpRoutine(float duration)
    {

        // check if it's already active to avoid double-saving stats.
        if (isRareModeActive) yield break;

        isRareModeActive = true;

        // 2. Snapshot (Save) current stats
        savedBombCount = maxBombs.Value;
        savedExplosionRange = explosionRange.Value;

        // 3. Apply Max Stats
        maxBombs.Value = ABSOLUTE_MAX_BOMBS;
        explosionRange.Value = ABSOLUTE_MAX_RANGE;

        // Notify Client to enable visual effects (speed calculation)
        SetRareModeClientRpc(true);

        Debug.Log("RARE MODE ACTIVATED: Max Stats!");

        // 4. Wait
        yield return new WaitForSeconds(duration);

        // 5. Restore Stats
        maxBombs.Value = savedBombCount;
        explosionRange.Value = savedExplosionRange;

        isRareModeActive = false;

        // Notify Client to disable visual effects
        SetRareModeClientRpc(false);

        Debug.Log("RARE MODE ENDED: Stats Restored.");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetRareModeClientRpc(bool active)
    {
        // This updates the local variable used in CalculateCurrentSpeed()
        if (IsOwner)
        {
            isRareModeActive = active;
        }
    }

    // --- DEATH LOGIC ---

    public void Die(ulong killerId)
    {
        if (!IsServer || isDead.Value || isInvincible) return;

        if (GameManager.Instance != null && !GameManager.Instance.gameActive.Value) return;
        Debug.Log($"Player {OwnerClientId} was killed by {killerId}!");

        // PENALTY: Victim loses 15 points
        ScoreBoardManager.Instance.IncreasePlayerScoreRpc(OwnerClientId, -15);

        // REWARD: Killer gains 15 points
        // Check to ensure they didn't kill themselves 
        if (killerId != OwnerClientId)
        {
            ScoreBoardManager.Instance.IncreasePlayerScoreRpc(killerId, 15);
        }

        isDead.Value = true;

        if (isRareModeActive)
        {
            isRareModeActive = false;
            maxBombs.Value = savedBombCount;
            explosionRange.Value = savedExplosionRange;
            SetRareModeClientRpc(false);
        }

        ClearTemporaryItemEffects();

        StartCoroutine(RespawnCoroutine());
    }

    IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(3f);

        Vector2 respawnPos = transform.position;

        if (GameManager.Instance != null)
        {
            // KIỂM TRA: Có đang trong vòng bo sinh tử không?
            if (GameManager.Instance.suddenDeathStarted)
            {
                // Nếu CÓ: Dùng hàm GetSafeSpawnPosition để máy quét tìm 1 ô trống ngẫu nhiên không có gạch/bom
                respawnPos = GameManager.Instance.GetSafeSpawnPosition();
            }
            else
            {
                // Nếu KHÔNG: Hồi sinh về góc an toàn của mình như bình thường
                int spawnIndex = (int)(OwnerClientId % 4);
                respawnPos = GameManager.Instance.GetCornerSpawnPosition(spawnIndex);
            }
        }

        isDead.Value = false;
        TeleportPlayerRpc(respawnPos);

        // --- CƠ CHẾ BẤT TỬ 2 GIÂY ---
        AddInvincibilitySource();

        yield return new WaitForSeconds(2f);

        RemoveInvincibilitySource();
    }

    private void OnDeathStateChanged(bool prev, bool current)
    {
        if (visuals) visuals.enabled = !current;
        if (col) col.enabled = !current;
    }

    [Rpc(SendTo.Owner)]
    private void TeleportPlayerRpc(Vector2 position)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }


        if (TryGetComponent(out NetworkTransform netTransform))
        {
            netTransform.Teleport(position, transform.rotation, transform.localScale);
        }
        else
        {
            transform.position = position;
        }

        Debug.Log($"Respawned (Teleported) to {position}");
    }

    // Hàm này cho phép Server ra lệnh cho Player dịch chuyển tức thời
    public void ForceTeleport(Vector2 position)
    {
        if (!IsServer) return;
        transform.position = position;
        TeleportPlayerRpc(position);
        StartCoroutine(SafeTeleportRoutine());
    }

    [Rpc(SendTo.Everyone)]
    private void SetInvincibleVisualsRpc(bool isInvincibleState)
    {
        visualInvincible = isInvincibleState;
        RefreshStatusVisuals();
    }

    [Rpc(SendTo.Everyone)]
    private void SetInvisibilityVisualsRpc(bool isInvisibleState)
    {
        visualInvisible = isInvisibleState;
        RefreshStatusVisuals();
    }

    private void RefreshStatusVisuals()
    {
        if (visuals != null)
        {
            if (visualInvisible)
            {
                visuals.color = IsOwner
                    ? new Color(0.55f, 0.9f, 1f, 0.42f)
                    : new Color(0.55f, 0.9f, 1f, 0.12f);
            }
            else if (visualInvincible)
            {
                visuals.color = new Color(1f, 0.88f, 0.22f, 0.88f);
            }
            else
            {
                visuals.color = Color.white;
            }
        }
    }

    private void AddInvincibilitySource()
    {
        invincibilitySources++;
        isInvincible = true;
        SetInvincibleVisualsRpc(true);
    }

    private void RemoveInvincibilitySource()
    {
        invincibilitySources = Mathf.Max(0, invincibilitySources - 1);
        isInvincible = invincibilitySources > 0;
        SetInvincibleVisualsRpc(isInvincible);
    }

    public void ResetStats()
    {
        if (!IsServer) return;

        maxBombs.Value = 1;
        explosionRange.Value = 1;
        speedLevel.Value = 0;
        currentActiveBombs = 0;

        savedBombCount = 1;
        savedExplosionRange = 1;

        if (isRareModeActive)
        {
            isRareModeActive = false;
            SetRareModeClientRpc(false);
        }

        ClearTemporaryItemEffects();
    }

    private IEnumerator SafeTeleportRoutine()
    {
        AddInvincibilitySource();

        yield return new WaitForSeconds(2.5f); // Đứng bất tử 2.5 giây chờ mạng ổn định

        RemoveInvincibilitySource();
    }

    private void ClearTemporaryItemEffects()
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
            SetSlowEffectClientRpc(false);
        }

        if (reverseRoutine != null)
        {
            StopCoroutine(reverseRoutine);
            reverseRoutine = null;
            SetReverseEffectClientRpc(false);
        }

        if (invisibilityRoutine != null)
        {
            StopCoroutine(invisibilityRoutine);
            invisibilityRoutine = null;
            SetInvisibilityVisualsRpc(false);
        }

        if (itemInvincibilityRoutine != null)
        {
            StopCoroutine(itemInvincibilityRoutine);
            itemInvincibilityRoutine = null;
            RemoveInvincibilitySource();
        }
    }
}
