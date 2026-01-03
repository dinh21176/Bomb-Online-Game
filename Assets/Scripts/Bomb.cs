using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Bomb : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] Collider2D bombCollider;
    [SerializeField] LayerMask wallLayer; // 
    [Header("Settings")]
    [SerializeField] float fuseTime = 2f;

    private int explosionRange = 1;
    private ulong ownerId;
    private bool hasDetonated = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initially a trigger so the player doesn't get stuck
            bombCollider.isTrigger = true;
            StartCoroutine(ExplodeRoutine());
        }
    }

    public void Initialize(ulong playerId, int range)
    {
        ownerId = playerId;
        explosionRange = range;
    }

    // Makes the bomb solid
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && IsServer)
        {
            bombCollider.isTrigger = false; 
        }
    }

    IEnumerator ExplodeRoutine()
    {
        yield return new WaitForSeconds(fuseTime);

        // Explode after the fuse time
        Detonate();
    }

    public void Detonate()
    {
        if (!IsServer || hasDetonated) return;

        hasDetonated = true; // Mark as exploded immediately so it doesn't trigger again

        // Spawn Center
        SpawnExplosion(transform.position);

        // Spawn Arms
        SpawnExplosionArm(Vector3.up);
        SpawnExplosionArm(Vector3.down);
        SpawnExplosionArm(Vector3.left);
        SpawnExplosionArm(Vector3.right);

        // Restore Ammo to owner
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerId, out var client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.RestoreBombAmmo();
            }
        }

        // Destroy this bomb
        GetComponent<NetworkObject>().Despawn();
    }

    void SpawnExplosionArm(Vector3 direction)
    {
        for (int i = 1; i <= explosionRange; i++)
        {
            Vector3 targetPos = transform.position + (direction * i);

            // RAYCAST CHECK: Is there a wall here?
            // We use OverlapCircle to check the spot before spawning
            if (Physics2D.OverlapCircle(targetPos, 0.4f, wallLayer))
            {
                // Stop the explosion in this direction.
                break;
            }

            SpawnExplosion(targetPos);
        }
    }

    void SpawnExplosion(Vector3 position)
    {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.GetComponent<NetworkObject>().Spawn();
    }
}