using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections; 
using TMPro;

public class PlayerName : NetworkBehaviour
{
    [SerializeField] private TextMeshPro playerNameText;

    public NetworkVariable<FixedString32Bytes> networkPlayerName =
        new NetworkVariable<FixedString32Bytes>("Unknown", NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", "Player");
            networkPlayerName.Value = new FixedString32Bytes(savedName);

            StartCoroutine(RegisterWithScoreboard(savedName));
        }

        playerNameText.text = networkPlayerName.Value.ToString();
        networkPlayerName.OnValueChanged += (oldVal, newVal) => {
            playerNameText.text = newVal.ToString();
        };
    }

    private IEnumerator RegisterWithScoreboard(string name)
    {
        // Keep checking until the Instance exists
        while (ScoreBoardManager.Instance == null)
        {
            yield return null;
        }

        ScoreBoardManager.Instance.AddPlayerServerRpc(OwnerClientId, name);
    }
}