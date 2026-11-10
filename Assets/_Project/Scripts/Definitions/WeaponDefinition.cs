using UnityEngine;

[CreateAssetMenu(menuName = "Test3D/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private WeaponType type;
    [SerializeField] private AbstractWeapon heldPrefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private float previewDistance = 1.5f;

    public string Id => id;
    public string DisplayName => displayName;
    public WeaponType Type => type;
    public AbstractWeapon HeldPrefab => heldPrefab;
    public GameObject PreviewPrefab => previewPrefab;
    public float PreviewDistance => previewDistance;
}
