using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    public NetworkVariable<int> coinType = new NetworkVariable<int>(0);
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Drag your sprites here in the Inspector!
    [Header("Item Sprites")]
    [SerializeField] Sprite coinSprite;
    [SerializeField] Sprite diamondSprite;
    [SerializeField] Sprite trapSprite; // Maybe use a skull or poison icon?
    [SerializeField] Sprite speedSprite; // Your "ItemSpeed" asset
    [SerializeField] Sprite bombUpSprite; // Your "ItemExtraBomb" asset
    [SerializeField] Sprite fireSprite;   // Your "ItemBlastRange" asset
    [SerializeField] Sprite rareSprite;

    private int scoreValue = 0;

    public override void OnNetworkSpawn()
    {
        coinType.OnValueChanged += (old, val) => ApplyVisuals(val);
        ApplyVisuals(coinType.Value);
    }

    private void ApplyVisuals(int type)
    {
        // Reset color to white so the sprite shows its real colors
        spriteRenderer.color = Color.white;

        switch (type)
        {
            case 0: spriteRenderer.sprite = coinSprite; scoreValue = 1; break;
            case 1: spriteRenderer.sprite = diamondSprite; scoreValue = 5; break;
            case 2: // Trap
                spriteRenderer.sprite = trapSprite;
                scoreValue = -3;
                spriteRenderer.color = Color.red; // Tint it red if you don't have a specific trap sprite
                break;
            case 3: spriteRenderer.sprite = speedSprite; break;
            case 4: spriteRenderer.sprite = bombUpSprite; break;
            case 5: spriteRenderer.sprite = fireSprite; break;
            case 6: spriteRenderer.sprite = rareSprite; break;
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