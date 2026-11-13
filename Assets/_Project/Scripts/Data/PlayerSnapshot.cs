using System;
using System.Globalization;
using UnityEngine;

// Toa do 1 nhan vat tai thoi diem luu. Field camelCase vi JsonUtility lay ten field lam key; Vector3 ghi thanh {"x","y","z"}.
// clientId dung long: JsonUtility khong ho tro ulong (KI-29)
[Serializable]
public class PlayerSnapshot
{
    public string role;
    public long clientId;
    public string characterId;
    public string characterName;
    public Vector3 position;
    public float rotationY;
    public string capturedAt;

    public static PlayerSnapshot Create(string role, long clientId, string characterId, string characterName,
        Vector3 position, float rotationY, DateTime capturedAt)
    {
        return new PlayerSnapshot
        {
            role = role,
            clientId = clientId,
            characterId = characterId,
            characterName = characterName,
            position = position,
            rotationY = rotationY,
            capturedAt = capturedAt.ToString(ScenarioData.DATE_FORMAT, CultureInfo.InvariantCulture)
        };
    }
}
