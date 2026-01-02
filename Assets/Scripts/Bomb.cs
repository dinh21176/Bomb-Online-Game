using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Bomb : NetworkBehaviour
{
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] float fuseTime = 2f;
    [SerializeField] Collider2D bombCollider; 

    private int explosionRange = 1;
    private ulong ownerId;

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

    // This makes the bomb solid only after the player leaves it
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && IsServer)
        {
            bombCollider.isTrigger = false; // Now it's a solid wall
        }
    }

    IEnumerator ExplodeRoutine()
    {
        yield return new WaitForSeconds(fuseTime);

        // Spawn Center
        SpawnExplosion(transform.position);

        // Spawn arms based on Range
        SpawnExplosionArm(Vector3.up);
        SpawnExplosionArm(Vector3.down);
        SpawnExplosionArm(Vector3.left);
        SpawnExplosionArm(Vector3.right);

        // Notify Player to restore ammo
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerId, out var client))
        {
            var player = client.PlayerObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.RestoreBombAmmo();
            }
        }

        // Destroy Bomb
        GetComponent<NetworkObject>().Despawn();
    }

    void SpawnExplosionArm(Vector3 direction)
    {
        for (int i = 1; i <= explosionRange; i++)
        {
            // Simple check to see if we hit a wall (optional, depends on your map)
            // if (Physics2D.OverlapCircle(transform.position + direction * i, 0.4f, wallLayer)) break;

            SpawnExplosion(transform.position + (direction * i)); // Spacing assumed 1 unit
        }
    }

    void SpawnExplosion(Vector3 position)
    {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        explosion.GetComponent<NetworkObject>().Spawn();
    }
}