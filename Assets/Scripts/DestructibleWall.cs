using Unity.Netcode;
using UnityEngine;

public class DestructibleWall : NetworkBehaviour
{
    [Header("Item Settings")]
    [SerializeField] private GameObject coinPrefab; // Kéo prefab Coin vào đây
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.35f; // 35% tỷ lệ rớt đồ

    public void DestroyWall()
    {
        if (!IsServer) return; // Tránh việc Client tự vỡ tường gây lỗi đồng bộ

        // Đổ xúc xắc xem có rớt đồ không
        if (Random.value <= dropChance && coinPrefab != null)
        {
            GameObject item = Instantiate(coinPrefab, transform.position, Quaternion.identity);
            NetworkObject netObj = item.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn(); // Đồng bộ item cho mọi người

                item.GetComponent<Coin>().coinType.Value = RollDroppedItemType();
            }
        }

        // Hủy bức tường
        GetComponent<NetworkObject>().Despawn(true);
    }

    private int RollDroppedItemType()
    {
        float r = Random.Range(0f, 100f);

        if (r > 98f) return 6;   // Rare
        if (r > 94f) return 10;  // Invincible
        if (r > 90f) return 9;   // Invisible
        if (r > 84f) return 8;   // Reverse controls
        if (r > 78f) return 7;   // Slow
        if (r > 70f) return 1;   // Diamond
        if (r > 62f) return 2;   // Trap
        if (r > 52f) return 5;   // Fire
        if (r > 42f) return 4;   // Bomb Up
        if (r > 32f) return 3;   // Speed

        return 0;                // Coin
    }
}
