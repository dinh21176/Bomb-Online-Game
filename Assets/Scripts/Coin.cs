using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    private const int SlowItemType = 7;
    private const int ReverseItemType = 8;
    private const int InvisibleItemType = 9;
    private const int InvincibleItemType = 10;

    public NetworkVariable<int> coinType = new NetworkVariable<int>(0);
    [SerializeField] private SpriteRenderer spriteRenderer;

    // Drag your sprites here in the Inspector!
    [Header("Item Sprites")]
    [SerializeField] Sprite coinSprite;
    [SerializeField] Sprite diamondSprite;
    [SerializeField] Sprite trapSprite;
    [SerializeField] Sprite speedSprite; 
    [SerializeField] Sprite bombUpSprite; 
    [SerializeField] Sprite fireSprite; 
    [SerializeField] Sprite rareSprite;
    [SerializeField] Sprite slowSprite;
    [SerializeField] Sprite reverseSprite;
    [SerializeField] Sprite invisibleSprite;
    [SerializeField] Sprite invincibleSprite;

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
                spriteRenderer.color = Color.red; 
                break;
            case 3: spriteRenderer.sprite = speedSprite; break;
            case 4: spriteRenderer.sprite = bombUpSprite; break;
            case 5: spriteRenderer.sprite = fireSprite; break;
            case 6: spriteRenderer.sprite = rareSprite; break;
            case SlowItemType:
                spriteRenderer.sprite = slowSprite;
                spriteRenderer.color = new Color(0.45f, 0.75f, 1f, 1f);
                break;
            case ReverseItemType:
                spriteRenderer.sprite = reverseSprite;
                spriteRenderer.color = new Color(1f, 0.45f, 0.45f, 1f);
                break;
            case InvisibleItemType:
                spriteRenderer.sprite = invisibleSprite;
                spriteRenderer.color = new Color(0.55f, 0.95f, 1f, 1f);
                break;
            case InvincibleItemType:
                spriteRenderer.sprite = invincibleSprite;
                spriteRenderer.color = new Color(1f, 0.88f, 0.22f, 1f);
                break;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlayGlobalSFXRpc(1);
            }

            // 1. NẾU LÀ NGƯỜI CHƠI THẬT
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                ulong playerId = other.GetComponent<NetworkObject>().OwnerClientId;

                // Cộng điểm
                if (scoreValue != 0)
                    ScoreBoardManager.Instance.IncreasePlayerScoreRpc(playerId, scoreValue);

                // Nâng cấp chỉ số
                if (coinType.Value == 3) player.UpgradeStat(0); // Speed
                if (coinType.Value == 4) player.UpgradeStat(1); // Bomb Up
                if (coinType.Value == 5) player.UpgradeStat(2); // Fire
                if (coinType.Value == 6) player.UpgradeStat(3); // RARE
                if (coinType.Value >= SlowItemType && coinType.Value <= InvincibleItemType)
                    player.ApplyItemEffect(coinType.Value);
            }

            // 2. NẾU LÀ BOT AI
            var bot = other.GetComponent<BotAI>();
            if (bot != null)
            {
                // Cộng điểm cho Bot
                if (scoreValue != 0)
                    bot.botScore.Value += scoreValue;

                // Nâng cấp chỉ số cho Bot
                if (coinType.Value == 3) bot.UpgradeStat(0); // Speed
                if (coinType.Value == 4) bot.UpgradeStat(1); // Bomb Up
                if (coinType.Value == 5) bot.UpgradeStat(2); // Fire
                if (coinType.Value == 6) bot.UpgradeStat(3); // RARE
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
