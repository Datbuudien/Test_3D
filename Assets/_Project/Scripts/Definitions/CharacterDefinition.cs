using UnityEngine;

[CreateAssetMenu(menuName = "Test3D/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private CharacterVisual visualPrefab;
    [SerializeField] private GameObject previewPrefab;
    [SerializeField] private float previewDistance = 3f;

    public string Id => id;
    public string DisplayName => displayName;
    public CharacterVisual VisualPrefab => visualPrefab;
    public GameObject PreviewPrefab => previewPrefab;
    public float PreviewDistance => previewDistance;
}
