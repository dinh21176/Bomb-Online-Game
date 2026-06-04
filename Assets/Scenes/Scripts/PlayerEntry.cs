using UnityEngine;
using TMPro;

public class PlayerEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI playerScore;


    public void SetPlayerEntry(string name, int score)
    {
        playerName.text = name;
        playerScore.text = score.ToString();
    }
}
