using Unity.Netcode;
using UnityEngine;

public class Explosion : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Automatically destroy the explosion after 0.5 seconds
        if (IsServer)
        {
            Invoke(nameof(DestroyExplosion), 0.5f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return; // Only server handles damage

        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.Die(); // Kill the player
            }
        }
    }

    void DestroyExplosion()
    {
        GetComponent<NetworkObject>().Despawn();
    }
}