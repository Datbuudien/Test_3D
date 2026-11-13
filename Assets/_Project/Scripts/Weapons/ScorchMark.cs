using System;
using Unity.Netcode;
using UnityEngine;

// Mot vet no trong NetworkList: du de moi may (ke ca join muon) dung lai decal y het nhau
public struct ScorchMark : INetworkSerializable, IEquatable<ScorchMark>
{
    public Vector3 Position;
    public Vector3 Normal;
    public float RotationAngle;

    public ScorchMark(Vector3 position, Vector3 normal, float rotationAngle)
    {
        Position = position;
        Normal = normal;
        RotationAngle = rotationAngle;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Normal);
        serializer.SerializeValue(ref RotationAngle);
    }

    public bool Equals(ScorchMark other)
    {
        return Position.Equals(other.Position) && Normal.Equals(other.Normal) && RotationAngle.Equals(other.RotationAngle);
    }

    public override bool Equals(object obj)
    {
        return obj is ScorchMark other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Normal, RotationAngle);
    }
}
