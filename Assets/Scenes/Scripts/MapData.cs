using UnityEngine;

[CreateAssetMenu(fileName = "NewMapData", menuName = "BoomOnline/Map Data")]
public class MapData : ScriptableObject
{
    public string mapName = "Map Rừng Cấm";

    [Header("0: Empty | 1: HardWall  | 2: SoftWall")]
    [Tooltip("Input your map data here.")]
    [TextArea(10, 20)] 
    public string mapLayout =
@"111111111111111
100000000000001
101212121212101
102222222222201
101212121212101
102222222222201
101212121212101
102222222222201
101212121212101
100000000000001
111111111111111";
}