using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    // 0=Score, 1=Diamond, 2=Trap, 3=Speed, 4=BombUp, 5=Fire(Range), 6=RARE
    public NetworkVariable<int> coinType = new NetworkVariable<int>(0);
    [SerializeField] private SpriteRenderer spriteRenderer;
    private int scoreValue = 0;

    public override void OnNetworkSpawn()
    {
        coinType.OnValueChanged += (old, val) => ApplyVisuals(val);
        ApplyVisuals(coinType.Value);
    }

    private void ApplyVisuals(int type)
    {
        switch (type)
        {
            case 0: spriteRenderer.color = Color.yellow; scoreValue = 1; break; // Coin
            case 1: spriteRenderer.color = Color.cyan; scoreValue = 5; break; // Diamond
            case 2: spriteRenderer.color = Color.red; scoreValue = -3; break; // Trap
            case 3: spriteRenderer.color = Color.white; break; // Speed
            case 4: spriteRenderer.color = Color.black; break; // Bomb Up
            case 5: spriteRenderer.color = Color.grey; break; // Fire (Orange)
            case 6: spriteRenderer.color = Color.magenta; break; // RARE (Max)
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            ulong playerId = other.GetComponent<NetworkObject>().OwnerClientId;
            var player = other.GetComponent<PlayerMovement>();

            // Handle Score items
            if (scoreValue != 0)
                ScoreBoardManager.Instance.IncreasePlayerScoreRpc(playerId, scoreValue);

            // Handle Stat items
            if (player != null)
            {
                if (coinType.Value == 3) player.UpgradeStat(0); // Speed
                if (coinType.Value == 4) player.UpgradeStat(1); // Bomb Up
                if (coinType.Value == 5) player.UpgradeStat(2); // Fire
                if (coinType.Value == 6) player.UpgradeStat(3); // RARE
            }

            DesTroyCoinRpc();
        }
    }

    private void OnCoinTypeChanged(int oldType, int newType)
    {
        ApplyVisuals(newType);
    }

    //void OnTriggerEnter2D(Collider2D other)
    //{
    //    if (!IsServer) return;

    //    if (other.CompareTag("Player"))
    //    {
    //        ulong playerId = other.GetComponent<NetworkObject>().OwnerClientId;

    //        // Check for Speed Boost
    //        if (coinType.Value == 3)
    //        {
    //            var playerMovement = other.GetComponent<PlayerMovement>();
    //            if (playerMovement != null)
    //            {
    //                // Double speed for 5 seconds
    //                playerMovement.ApplySpeedBoost(2.0f, 5.0f);
    //            }
    //        }
    //        else
    //        {
    //            // Normal Score Logic
    //            ScoreBoardManager.Instance.IncreasePlayerScoreRpc(playerId, finalValue);
    //        }

    //        DesTroyCoinRpc();
    //    }
    //}

    [Rpc(SendTo.Server)]
    //public void DesTroyCoinRpc()
    //{
    //    if (IsServer)
    //    {
    //        // Despawn(true) automatically destroys the GameObject on both server and clients
    //        GetComponent<NetworkObject>().Despawn(true);
    //    }
    //}
    public void DesTroyCoinRpc() { GetComponent<NetworkObject>().Despawn(true); }
}