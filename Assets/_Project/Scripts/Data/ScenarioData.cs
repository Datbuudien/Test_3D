using System;
using System.Globalization;

// Field camelCase vi JsonUtility lay ten field lam key JSON (ngoai le da ghi trong unity-coding.md)
[Serializable]
public class ScenarioData
{
    public const string DATE_FORMAT = "yyyy-MM-ddTHH:mm:ss";
    public const string ROLE_HOST = "host";
    public const string ROLE_CLIENT = "client";
    private const string ROLE_SUFFIX_SEPARATOR = "_";

    public string scenarioName;
    public string characterId;
    public string characterName;
    public string weaponId;
    public string weaponName;
    public string createdAt;
    // Task 2: may nao ghi file + toa do moi nhan vat. JSON Task 1 khong co 2 field nay -> Normalize ve rong
    public string savedBy = string.Empty;
    public PlayerSnapshot[] players = Array.Empty<PlayerSnapshot>();

    public static ScenarioData Create(string name, string characterId, string characterName,
        string weaponId, string weaponName, DateTime now)
    {
        return new ScenarioData
        {
            scenarioName = name,
            characterId = characterId,
            characterName = characterName,
            weaponId = weaponId,
            weaponName = weaponName,
            createdAt = now.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
        };
    }

    public static string GetRole(bool isHost)
    {
        return isHost ? ROLE_HOST : ROLE_CLIENT;
    }

    // 2 instance tren cung 1 may dung chung persistentDataPath -> hau to role de khong ghi de file cua nhau (KI-21)
    // Ten file bi cat con MAX_FILE_NAME_LENGTH -> cat phan ten goc truoc de hau to role khong bi mat
    public static string BuildSessionScenarioName(string baseName, string role)
    {
        string suffix = ROLE_SUFFIX_SEPARATOR + role;
        string trimmedBase = (baseName ?? string.Empty).Trim();
        int maxBaseLength = Math.Max(0, ScenarioRepository.MAX_FILE_NAME_LENGTH - suffix.Length);
        if (trimmedBase.Length > maxBaseLength)
        {
            trimmedBase = trimmedBase.Substring(0, maxBaseLength).TrimEnd();
        }

        return trimmedBase + suffix;
    }

    public bool HasRequiredFields()
    {
        return !string.IsNullOrWhiteSpace(scenarioName)
            && !string.IsNullOrWhiteSpace(characterId)
            && !string.IsNullOrWhiteSpace(weaponId);
    }

    // Ban sao kem toa do: ten file theo role cua may dang luu, ScenarioSession giu nguyen ban goc
    public ScenarioData WithPlayers(string savedByRole, PlayerSnapshot[] snapshots)
    {
        return new ScenarioData
        {
            scenarioName = BuildSessionScenarioName(scenarioName, savedByRole),
            characterId = characterId,
            characterName = characterName,
            weaponId = weaponId,
            weaponName = weaponName,
            createdAt = createdAt,
            savedBy = savedByRole ?? string.Empty,
            players = snapshots ?? Array.Empty<PlayerSnapshot>()
        };
    }

    public void Normalize()
    {
        savedBy ??= string.Empty;
        players ??= Array.Empty<PlayerSnapshot>();
    }
}
