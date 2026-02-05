using Unity.Netcode;
using UnityEngine;

public class Explosion : NetworkBehaviour
{
    public ulong killerId;
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

        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.Die(killerId);
                
            }
        }
        else if (other.CompareTag("Bomb"))
        {
            var bomb = other.GetComponent<Bomb>();
            if (bomb != null)
            {
                bomb.Detonate();
            }
        }
    }

    void DestroyExplosion()
    {
        if (IsSpawned) GetComponent<NetworkObject>().Despawn();
    }
}