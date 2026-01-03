using Unity.Netcode;
using UnityEngine;

public class Explosion : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(DestroyExplosion), 0.5f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;
        Debug.Log($"Explosion hit: {other.name} with Tag: {other.tag}");

        // 1. Kill Player
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.Die();
            }
        }
        // 2. Trigger Chain Reaction (Bomb)
        else if (other.CompareTag("Bomb"))
        {
            Debug.Log("Found a bomb! Detonating now.");
            var bomb = other.GetComponent<Bomb>();
            if (bomb != null)
            {
                // Explode immediately!
                bomb.Detonate();
            }
        }
    }

    void DestroyExplosion()
    {
        if (IsSpawned) GetComponent<NetworkObject>().Despawn();
    }
}