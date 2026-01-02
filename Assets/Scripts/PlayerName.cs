using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using System;
using TMPro.EditorUtilities;
using TMPro;

public class PlayerName : NetworkBehaviour
{
    [SerializeField] private TextMeshPro playerName;

    public NetworkVariable<FixedString32Bytes> networkPlayerName =
        new NetworkVariable<FixedString32Bytes>("Unknown", NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

    public event Action<string> OnNameChanged;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            string inputName = FindFirstObjectByType<UIManager>()
                .GetComponent<UIManager>()
                .nameinputField.text;

            networkPlayerName.Value = new FixedString32Bytes(inputName);

        }

        playerName.text = networkPlayerName.Value.ToString();
        networkPlayerName.OnValueChanged += NetworkPlayerName_OnValueChanged;

        OnNameChanged?.Invoke(networkPlayerName.Value.ToString());
    }

    private void NetworkPlayerName_OnValueChanged(FixedString32Bytes previousValue,
        FixedString32Bytes newValue)
    {
        playerName.text = newValue.ToString();
        OnNameChanged?.Invoke(newValue.Value);
    }

    public string GetPlayerName()
    {
        return networkPlayerName.Value.ToString();
    }
}
