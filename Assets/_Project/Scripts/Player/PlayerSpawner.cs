using System;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private CharacterVisual currentVisual;
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private Transform visualParent;

    // Start (khong Awake): moi OnEnable da subscribe WeaponEquipped truoc khi Rebuild phat event
    void Start()
    {
        if (!ScenarioSession.HasScenario || weaponHolder == null || visualParent == null)
        {
            return;
        }

        ScenarioData scenario = ScenarioSession.Current;
        CharacterDefinition character = FindCharacter(scenario.characterId);
        CharacterVisual visual = currentVisual;
        if (character != null && character.VisualPrefab != null)
        {
            visual = Instantiate(character.VisualPrefab, visualParent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }
#if UNITY_EDITOR
        else
        {
            Debug.LogWarning($"{nameof(PlayerSpawner)}: character '{scenario.characterId}' not found, keeping default visual.", this);
        }
#endif

        weaponHolder.Rebuild(visual, weapons, FindWeaponSlot(scenario.weaponId));
        if (visual != currentVisual && currentVisual != null)
        {
            Destroy(currentVisual.gameObject);
        }

        currentVisual = visual;
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

    private int FindWeaponSlot(string weaponId)
    {
        if (weapons == null)
        {
            return 0;
        }

        int slot = Array.FindIndex(weapons, weapon => weapon != null && weapon.Id == weaponId);
        return slot >= 0 ? slot : 0;
    }
}
