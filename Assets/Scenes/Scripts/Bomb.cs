using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Bomb : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] Collider2D bombCollider;
    [SerializeField] LayerMask hardWallLayer;
    [SerializeField] LayerMask softWallLayer;
    [Header("Settings")]
    [SerializeField] float fuseTime = 2f;

    private int explosionRange = 1;
    private ulong ownerId;
    private bool hasDetonated = false;

    public override void OnNetworkSpawn()
    {
        bombCollider.isTrigger = true;

        if (IsServer)
        {
            // Initially a trigger so the player doesn't get stuck
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
        if (other.CompareTag("Player"))
        {
            bombCollider.isTrigger = false; 
        }
    }

    IEnumerator ExplodeRoutine()
    {
        yield return new WaitForSeconds(fuseTime);

        // Explode 
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

            // Check Layer
            LayerMask obstacles = hardWallLayer | softWallLayer;

            Collider2D hit = Physics2D.OverlapCircle(targetPos, 0.4f, obstacles);

            if (hit != null)
            {
                if (((1 << hit.gameObject.layer) & softWallLayer) != 0)
                {
                    SpawnExplosion(targetPos);
                }

                break;
            }

            SpawnExplosion(targetPos);
        }
    }

    void SpawnExplosion(Vector3 position)
    {
        GameObject explosionObj = Instantiate(explosionPrefab, position, Quaternion.identity);

        //  Assign the killerID BEFORE spawning so the logic is ready on the server
        Explosion explosionScript = explosionObj.GetComponent<Explosion>();
        if (explosionScript != null)
        {
            explosionScript.killerId = ownerId;
        }

        explosionObj.GetComponent<NetworkObject>().Spawn();
    }
}