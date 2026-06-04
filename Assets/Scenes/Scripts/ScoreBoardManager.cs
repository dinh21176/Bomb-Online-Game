using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public class ScoreBoardManager : NetworkBehaviour
{
    public static ScoreBoardManager Instance;

    [Header("UI References")]
    [SerializeField] private Transform scoreContainer;
    [SerializeField] private GameObject playerEntryPrefab;

    private NetworkList<PlayerStats> connectedPlayers;

    private void Awake()
    {
        Instance = this;
        connectedPlayers = new NetworkList<PlayerStats>();
    }

    public override void OnNetworkSpawn()
    {
        if (scoreContainer == null)
        {
            Debug.LogError("Score Container is empty! .");
            return;
        }

        connectedPlayers.OnListChanged += HandleListChanged;
        UpdateScoreboardUI();
    }

    public override void OnNetworkDespawn()
    {
        if (connectedPlayers != null)
            connectedPlayers.OnListChanged -= HandleListChanged;
    }

    [Rpc(SendTo.Server)]
    public void AddPlayerServerRpc(ulong id, string name)
    {
        foreach (var player in connectedPlayers)
        {
            if (player.playerId == id) return;
        }

        connectedPlayers.Add(new PlayerStats
        {
            playerId = id,
            score = 0,
            playerName = new FixedString32Bytes(name)
        });
    }

    [Rpc(SendTo.Server)]
    public void IncreasePlayerScoreRpc(ulong id, int amount)
    {
        for (int i = 0; i < connectedPlayers.Count; i++)
        {
            if (connectedPlayers[i].playerId == id)
            {
                var stats = connectedPlayers[i];

                int finalScore = stats.score + amount;

                if (finalScore < 0)
                {
                    finalScore = 0;
                }

                stats.score = finalScore;
                connectedPlayers[i] = stats;
                break;
            }
        }
    }

    private void HandleListChanged(NetworkListEvent<PlayerStats> changeEvent)
    {
        UpdateScoreboardUI();
    }

    private void UpdateScoreboardUI()
    {
        if (scoreContainer == null) return; // Stop if UI is missing

        foreach (Transform child in scoreContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var player in connectedPlayers)
        {
            GameObject entryObj = Instantiate(playerEntryPrefab, scoreContainer);
            PlayerEntry entryScript = entryObj.GetComponent<PlayerEntry>();
            entryScript.SetPlayerEntry(player.playerName.ToString(), player.score);
        }
    }

    public string GetWinnerName()
    {
        if (connectedPlayers.Count == 0) return "No Winner";
        PlayerStats bestPlayer = connectedPlayers[0];
        foreach (var p in connectedPlayers)
        {
            if (p.score > bestPlayer.score) bestPlayer = p;
        }
        return $"{bestPlayer.playerName} Wins!";
    }
}