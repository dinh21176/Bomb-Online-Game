using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MapGenerator : NetworkBehaviour
{
    public static MapGenerator Instance;

    [Header("Prefabs")]
    [SerializeField] private GameObject hardWallPrefab;
    [SerializeField] private GameObject softWallPrefab;

    [Header("Map Database")]
    public MapData[] mapDatabase;

    // Lưu danh sách tường để xóa khi reset ván mới
    private List<GameObject> spawnedWalls = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateMap(int mapIndex)
    {
        if (!IsServer) return;

        ClearCurrentMap(); 

        if (mapIndex < 0 || mapIndex >= mapDatabase.Length) return;

        MapData currentMap = mapDatabase[mapIndex];

        // Tách đoạn text thành từng dòng (loại bỏ các ký tự xuống dòng thừa)
        string[] rows = currentMap.mapLayout.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        int height = rows.Length;
        if (height == 0) return;
        int width = rows[0].Length;

        // TỐI ƯU HÓA: Tự động tính toán để Map luôn nằm chính giữa màn hình (tọa độ 0,0)
        float startX = -width / 2f + 0.5f;
        float startY = height / 2f - 0.5f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.spawnAreaMin = new Vector2(startX, startY - height + 1);
            GameManager.Instance.spawnAreaMax = new Vector2(startX + width - 1, startY);
        }

        for (int y = 0; y < height; y++)
        {
            string row = rows[y].Trim(); // Cắt khoảng trắng thừa
            for (int x = 0; x < width; x++)
            {
                // Nếu lỡ gõ dòng ngắn dòng dài thì bỏ qua để tránh lỗi
                if (x >= row.Length) break;

                char tile = row[x];
                Vector2 spawnPos = new Vector2(startX + x, startY - y);
                GameObject wallObj = null;

                if (tile == '1')
                {
                    wallObj = Instantiate(hardWallPrefab, spawnPos, Quaternion.identity);
                }
                else if (tile == '2')
                {
                    wallObj = Instantiate(softWallPrefab, spawnPos, Quaternion.identity);
                }

                if (wallObj != null)
                {
                    wallObj.GetComponent<NetworkObject>().Spawn();
                    spawnedWalls.Add(wallObj);
                }
            }
        }

        Debug.Log($"Đã tạo xong map: {currentMap.mapName} với kích thước {width}x{height}");
    }

    public void ClearCurrentMap()
    {
        foreach (GameObject wall in spawnedWalls)
        {
            if (wall != null && wall.GetComponent<NetworkObject>().IsSpawned)
            {
                wall.GetComponent<NetworkObject>().Despawn(true);
            }
        }
        spawnedWalls.Clear();
    }
    public void RegisterWall(GameObject wall)
    {
        if (wall != null && !spawnedWalls.Contains(wall))
        {
            spawnedWalls.Add(wall);
        }
    }
}