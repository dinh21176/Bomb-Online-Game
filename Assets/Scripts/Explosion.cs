using Unity.Netcode;
using UnityEngine;

public class Explosion : NetworkBehaviour
{
    public ulong killerId;

    private static float lastSoundTime = 0f;
    public override void OnNetworkSpawn()
    {
        if (AudioManager.Instance != null)
        {
            // CHỈ CHO PHÉP phát tiếng nổ nếu cách tiếng trước đó ít nhất 0.1 giây
            if (Time.time - lastSoundTime > 0.1f)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.explodeSFX);
                lastSoundTime = Time.time;
            }
        }

        if (IsServer)
        {
            Invoke(nameof(DestroyExplosion), 0.3f);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            // Kiểm tra nếu là người chơi thật
            var player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.Die(killerId);
            }

            // KIỂM TRA NẾU LÀ BOT THÌ GIẾT BOT VÀ CỘNG ĐIỂM
            var bot = other.GetComponent<BotAI>();
            if (bot != null && !bot.isDead) // Thêm check !isDead để không cộng điểm 2 lần
            {
                bot.Die();

                // Gọi hàm cộng 10 điểm cho người chơi hạ gục Bot[cite: 16]
                if (ScoreBoardManager.Instance != null)
                {
                    ScoreBoardManager.Instance.IncreasePlayerScoreRpc(killerId, 10);
                }
            }
        }
        else if (other.CompareTag("Bomb"))
        {
            var bomb = other.GetComponent<Bomb>();
            if (bomb != null) bomb.Detonate();
        }
        else if (other.CompareTag("SoftWall"))
        {
            var softWall = other.GetComponent<DestructibleWall>();
            if (softWall != null) softWall.DestroyWall();
        }
    }

    void DestroyExplosion()
    {
        if (IsSpawned) GetComponent<NetworkObject>().Despawn();
    }
}