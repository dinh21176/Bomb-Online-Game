using System.Collections;
using Unity.Netcode;
using UnityEngine;

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
            Debug.LogError("ANIMATOR NOT FOUND! Make sure the Animator component is attached to the Player or a child object.");
        }

        isDead.OnValueChanged += OnDeathStateChanged;
    }

    private void Update()
    {
        if (isDead.Value) return;

        if (IsOwner)
        {
            float x = Input.GetAxis("Horizontal");
            float y = Input.GetAxis("Vertical");
            Vector2 currentInput = new Vector2(x, y).normalized;

            // Send input to the server/other clients
            netInput.Value = currentInput;

            // Bomb Input (Owner Only)
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
            return baseMoveSpeed + (ABSOLUTE_MAX_SPEED_LEVEL * speedStep) + 2f;
        }

        return speed;
    }

    [Rpc(SendTo.Server)]
    void TryPlantBombServerRpc()
    {
        if (isDead.Value || currentActiveBombs >= maxBombs.Value) return;
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

    // --- RARE ITEM LOGIC ---

    IEnumerator RarePowerUpRoutine(float duration)
    {
        // 1. If we are already in Rare Mode, just extend the timer? 
        // For simplicity, we'll just ignore overlapping pickups or reset the timer in a complex version.
        // Here, we check if it's already active to avoid double-saving stats.
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

    public void Die()
    {
        if (!IsServer || isDead.Value) return;

        Debug.Log($"Player {OwnerClientId} Died!");

        // --- FEATURE: SCORE PENALTY ---
        // Subtract 15 points
        ScoreBoardManager.Instance.IncreasePlayerScoreRpc(OwnerClientId, -10);

        isDead.Value = true;

        // If they died with the Rare Mode active, cancel it immediately
        if (isRareModeActive)
        {
            isRareModeActive = false;
            maxBombs.Value = savedBombCount;
            explosionRange.Value = savedExplosionRange;
            SetRareModeClientRpc(false);
        }

        StartCoroutine(RespawnCoroutine());
    }

    IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(3f);

        // ---  USE SAFE SPAWN POSITION ---
        if (GameManager.Instance != null)
        {
            // Prevents spawning inside walls!
            transform.position = GameManager.Instance.GetSafeSpawnPosition();
        }

        isDead.Value = false;
    }

    private void OnDeathStateChanged(bool prev, bool current)
    {
        if (visuals) visuals.enabled = !current;
        if (col) col.enabled = !current;
    }
}