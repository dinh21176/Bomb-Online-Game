using Unity.Netcode;
using UnityEngine;

public class DestructibleWall : NetworkBehaviour
{
    [SerializeField] private GameObject[] itemPrefabs; 
    [SerializeField] private float dropRate = 0.3f; 

    public void DestroyWall()
    {
        if (!IsServer) return; // Chỉ Server mới có quyền quyết định phá tường và rớt đồ

        // Kiểm tra tỷ lệ rớt vật phẩm ngẫu nhiên
        if (Random.value <= dropRate && itemPrefabs.Length > 0)
        {
            // Chọn ngẫu nhiên 1 item và spawn ra
            int randomIndex = Random.Range(0, itemPrefabs.Length);
            GameObject item = Instantiate(itemPrefabs[randomIndex], transform.position, Quaternion.identity);

            // Spawn item lên mạng để mọi client đều thấy
            item.GetComponent<NetworkObject>().Spawn();

            // (Bạn có thể tái sử dụng logic Random item type từ GameManager.cs vào đây để set coinType)
        }

        // Hủy bức tường trên toàn mạng lưới
        GetComponent<NetworkObject>().Despawn(true);
    }
}