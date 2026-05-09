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

                // Tái sử dụng logic tỷ lệ độ hiếm (Rarity) của bạn
                int type = 0;
                float r = Random.Range(0f, 100f);
                if (r > 95f) type = 6;       // Rare
                else if (r > 90f) type = 1;  // Diamond
                else if (r > 75f) type = 2;  // Trap
                else if (r > 65f) type = 5;  // Fire
                else if (r > 55f) type = 4;  // Bomb Up
                else if (r > 45f) type = 3;  // Speed

                item.GetComponent<Coin>().coinType.Value = type;
            }
        }

        // Hủy bức tường
        GetComponent<NetworkObject>().Despawn(true);
    }
}