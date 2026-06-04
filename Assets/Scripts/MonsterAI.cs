using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MonsterAI : NetworkBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 3f;

    [Header("Layers")]
    public LayerMask obstacleLayer;
    public LayerMask bombLayer;

    public NetworkVariable<Vector2> netMoveDir = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector2 targetPosition;
    private Vector2 currentDirection;
    private bool isDead = false;

    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2[] allDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    public override void OnNetworkSpawn()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (!IsServer) return;

        targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        PickRandomDirection();
    }

    private void Update()
    {
        UpdateAnimations(netMoveDir.Value);

        if (!IsServer || isDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.gameActive.Value) return;

        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // KHI QUÁI ĐÃ ĐI ĐẾN TÂM CỦA Ô ĐÍCH
        if (Vector2.Distance(transform.position, targetPosition) <= 0.05f)
        {
            transform.position = targetPosition;
            Vector2 nextPos = targetPosition + currentDirection;
            if (CanMoveTo(nextPos)) targetPosition = nextPos;
            else PickRandomDirection();
        }
        else
        {
            // Nếu đang đi giữa chừng mà ô đích đột ngột có Bom chặn lại
            if (!CanMoveTo(targetPosition))
            {
                // Lập tức "quay xe" về lại tâm của ô hiện tại và tìm đường khác
                targetPosition = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
                PickRandomDirection();
            }
        }

        Vector2 moveDirServer = (targetPosition - (Vector2)transform.position).normalized;
        if (moveDirServer.magnitude > 0.1f)
        {
            if (Mathf.Abs(moveDirServer.x) > Mathf.Abs(moveDirServer.y)) moveDirServer = new Vector2(Mathf.Sign(moveDirServer.x), 0);
            else moveDirServer = new Vector2(0, Mathf.Sign(moveDirServer.y));
        }
        else moveDirServer = Vector2.zero;

        netMoveDir.Value = moveDirServer;
    }

    private bool CanMoveTo(Vector2 pos)
    {
        if (Physics2D.OverlapCircle(pos, 0.4f, obstacleLayer) || Physics2D.OverlapCircle(pos, 0.4f, bombLayer)) return false;
        return true;
    }

    private void PickRandomDirection()
    {
        List<Vector2> validDirs = new List<Vector2>();
        foreach (Vector2 dir in allDirections)
        {
            if (CanMoveTo(targetPosition + dir)) validDirs.Add(dir);
        }

        if (validDirs.Count > 0)
        {
            currentDirection = validDirs[Random.Range(0, validDirs.Count)];
            targetPosition += currentDirection;
        }
        else currentDirection = Vector2.zero;
    }

    private void UpdateAnimations(Vector2 moveDir)
    {
        if (animator == null || spriteRenderer == null) return;

        if (moveDir.magnitude > 0.1f)
        {
            animator.SetBool("IsMoving", true);

            // --- TỰ ĐỘNG LẬT MẶT (FLIP X) CHO QUÁI VẬT PLATFORMER ---
            if (moveDir.x > 0) spriteRenderer.flipX = true; // Quay sang phải
            else if (moveDir.x < 0) spriteRenderer.flipX = false; // Quay sang trái
        }
        else animator.SetBool("IsMoving", false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || isDead) return;

        if (collision.GetComponent<Explosion>() != null || collision.CompareTag("Explosion"))
        {
            Die();
        }

        if (collision.TryGetComponent(out PlayerMovement player))
        {
            player.Die(999);
        }
    }

    private void Die()
    {
        isDead = true;
        GetComponent<NetworkObject>().Despawn(true);
    }
}