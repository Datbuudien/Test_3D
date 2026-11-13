using UnityEngine;

// Doi visual + loadout theo id nhan tu mang (PlayerNetwork goi Apply tren moi may, ke ca client join muon)
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private CharacterVisual currentVisual;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private Transform visualParent;

    private string appliedCharacterId = string.Empty;

    public void Apply(string characterId, string weaponId)
    {
        if (weaponHolder == null || visualParent == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(PlayerSpawner)} on '{name}' needs WeaponHolder and Visual Parent.", this);
#endif
            return;
        }

        CharacterVisual visual = ResolveVisual(characterId);
        weaponHolder.Rebuild(visual, BuildLoadout(weaponId), 0);
    }

    private CharacterVisual ResolveVisual(string characterId)
    {
        CharacterDefinition character = FindCharacter(characterId);
        if (character == null || character.VisualPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"{nameof(PlayerSpawner)}: character '{characterId}' not found, keeping current visual.", this);
#endif
            return currentVisual;
        }

        if (character.Id == appliedCharacterId && currentVisual != null)
        {
            return currentVisual;
        }

        CharacterVisual visual = Instantiate(character.VisualPrefab, visualParent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        if (currentVisual != null)
        {
            Destroy(currentVisual.gameObject);
        }

        currentVisual = visual;
        appliedCharacterId = character.Id;
        return visual;
    }

    // Phien mang: vu khi da chon o slot 0 + luu dan (neu chua chon) de may nao cung demo nem duoc
    private WeaponDefinition[] BuildLoadout(string weaponId)
    {
        WeaponDefinition selected = FindWeapon(weaponId);
        if (selected == null)
        {
            selected = FirstValid(weapons);
        }

        if (selected == null)
        {
            return weapons;
        }

        WeaponDefinition grenade = FindGrenade();
        return grenade == null || grenade == selected
            ? new[] { selected }
            : new[] { selected, grenade };
    }

    private CharacterDefinition FindCharacter(string characterId)
    {
        if (characters == null)
        {
            return null;
        }

        foreach (CharacterDefinition character in characters)
        {
            if (character != null && character.Id == characterId)
            {
                return character;
            }
        }

        return null;
    }

    private WeaponDefinition FindWeapon(string weaponId)
    {
        if (weapons == null)
        {
            return null;
        }

        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon != null && weapon.Id == weaponId)
            {
                return weapon;
            }
        }

        return null;
    }

    private WeaponDefinition FindGrenade()
    {
        if (weapons == null)
        {
            return null;
        }

        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon != null && weapon.Type == WeaponType.Grenade)
            {
                return weapon;
            }
        }

        return null;
    }

    private static WeaponDefinition FirstValid(WeaponDefinition[] definitions)
    {
        if (definitions == null)
        {
            return null;
        }

        foreach (WeaponDefinition definition in definitions)
        {
            if (definition != null)
            {
                return definition;
            }
        }

        return null;
    }
}
