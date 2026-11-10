using System;
using System.Globalization;

// Field camelCase vi JsonUtility lay ten field lam key JSON (ngoai le da ghi trong unity-coding.md)
[Serializable]
public class ScenarioData
{
    public const string DATE_FORMAT = "yyyy-MM-ddTHH:mm:ss";

    public string scenarioName;
    public string characterId;
    public string characterName;
    public string weaponId;
    public string weaponName;
    public string createdAt;

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

    public bool HasRequiredFields()
    {
        return !string.IsNullOrWhiteSpace(scenarioName)
            && !string.IsNullOrWhiteSpace(characterId)
            && !string.IsNullOrWhiteSpace(weaponId);
    }
}
