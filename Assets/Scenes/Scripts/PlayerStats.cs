using System;
using Unity.Netcode;
using Unity.Collections; 

public struct PlayerStats : INetworkSerializable, IEquatable<PlayerStats>
{
    public ulong playerId;
    public int score;
    public FixedString32Bytes playerName; 

    public bool Equals(PlayerStats other)
    {
        return playerId == other.playerId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref playerName); 
    }
}