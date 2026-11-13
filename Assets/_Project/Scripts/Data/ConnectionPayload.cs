using System;
using System.Text;
using UnityEngine;

// Lua chon cua client gui kem yeu cau ket noi (NetworkConfig.ConnectionData).
// Field camelCase vi JsonUtility lay ten field lam key JSON.
[Serializable]
public class ConnectionPayload
{
    public const int MAX_PAYLOAD_BYTES = 1024;
    public const int MAX_TEXT_LENGTH = 64;

    public string characterId = string.Empty;
    public string weaponId = string.Empty;
    public string scenarioName = string.Empty;

    public static ConnectionPayload Create(string characterId, string weaponId, string scenarioName)
    {
        ConnectionPayload payload = new ConnectionPayload
        {
            characterId = characterId,
            weaponId = weaponId,
            scenarioName = scenarioName
        };
        payload.Normalize();
        return payload;
    }

    public byte[] ToBytes()
    {
        return Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));
    }

    // Du lieu tu mang khong dang tin: rong / qua dai / JSON hong -> payload mac dinh, khong nem exception
    public static ConnectionPayload FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0 || bytes.Length > MAX_PAYLOAD_BYTES)
        {
            return new ConnectionPayload();
        }

        ConnectionPayload payload;
        try
        {
            payload = JsonUtility.FromJson<ConnectionPayload>(Encoding.UTF8.GetString(bytes));
        }
        catch (ArgumentException)
        {
            return new ConnectionPayload();
        }

        if (payload == null)
        {
            return new ConnectionPayload();
        }

        payload.Normalize();
        return payload;
    }

    private void Normalize()
    {
        characterId = Clamp(characterId);
        weaponId = Clamp(weaponId);
        scenarioName = Clamp(scenarioName);
    }

    private static string Clamp(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        return trimmed.Length > MAX_TEXT_LENGTH ? trimmed.Substring(0, MAX_TEXT_LENGTH) : trimmed;
    }
}
