using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Noi Gameplay: PlayerSpawner, SceneNavigator (Esc), quy dao nem, HUD. Chay lai se xoa ban cu roi tao moi
internal static class GameplaySceneWiring
{
    private const string LOG_PREFIX = "[GameplaySceneWiring]";
    private const float TRAJECTORY_WIDTH = 0.04f;
    private static readonly Vector3 LANDING_MARKER_SCALE = new Vector3(0.6f, 0.005f, 0.6f);
    private static readonly Color TRAJECTORY_START_COLOR = new Color(1f, 0.85f, 0.3f, 0.9f);
    private static readonly Color TRAJECTORY_END_COLOR = new Color(1f, 0.45f, 0.1f, 0.4f);
    private static readonly Color HUD_PANEL_COLOR = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color CHARGE_BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color CHARGE_FILL_COLOR = new Color(1f, 0.6f, 0.15f);

    internal static bool Wire(string scenePath, string[] characterPaths, string[] weaponPaths, string trajectoryMaterialPath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Nap asset SAU khi mo scene (KI-19)
        CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
        WeaponDefinition[] weapons = ProjectSceneBuilder.LoadAssetsAtPaths<WeaponDefinition>(weaponPaths);
        Material trajectoryMaterial = AssetDatabase.LoadAssetAtPath<Material>(trajectoryMaterialPath);
        InputReader inputReader = Object.FindAnyObjectByType<InputReader>();
        WeaponHolder weaponHolder = Object.FindAnyObjectByType<WeaponHolder>();
        GrenadeThrower thrower = Object.FindAnyObjectByType<GrenadeThrower>();
        if (inputReader == null || weaponHolder == null || thrower == null)
        {
            Debug.LogError($"{LOG_PREFIX} Gameplay needs Player with InputReader, WeaponHolder and GrenadeThrower.");
            return false;
        }

        RemoveExisting<SceneNavigator>();
        RemoveExisting<GrenadeTrajectoryPreview>();
        RemoveExisting<GameplayHud>();

        WirePlayerSpawner(inputReader.gameObject, characters, weapons, weaponHolder);
        CreateSceneNavigator(inputReader);
        CreateTrajectory(thrower, weaponHolder, trajectoryMaterial);
        CreateHud(weaponHolder, thrower);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static void RemoveExisting<T>() where T : Component
    {
        T existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        while (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
            existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }
    }

    private static void WirePlayerSpawner(GameObject player, CharacterDefinition[] characters, WeaponDefinition[] weapons, WeaponHolder weaponHolder)
    {
        if (!player.TryGetComponent(out PlayerSpawner spawner))
        {
            spawner = player.AddComponent<PlayerSpawner>();
        }

        SerializedObject holder = new SerializedObject(weaponHolder);
        SerializedObject serialized = new SerializedObject(spawner);
        AssignArray(serialized.FindProperty("characters"), characters);
        AssignArray(serialized.FindProperty("weapons"), weapons);
        serialized.FindProperty("currentVisual").objectReferenceValue = holder.FindProperty("characterVisual").objectReferenceValue;
        serialized.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
        serialized.FindProperty("visualParent").objectReferenceValue = player.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSceneNavigator(InputReader inputReader)
    {
        SceneNavigator navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
        SerializedObject serialized = new SerializedObject(navigator);
        serialized.FindProperty("inputReader").objectReferenceValue = inputReader;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateTrajectory(GrenadeThrower thrower, WeaponHolder weaponHolder, Material material)
    {
        GameObject trajectory = new GameObject("GrenadeTrajectory");
        LineRenderer line = trajectory.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.widthMultiplier = TRAJECTORY_WIDTH;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = TRAJECTORY_START_COLOR;
        line.endColor = TRAJECTORY_END_COLOR;
        line.enabled = false;

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "LandingMarker";
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.transform.SetParent(trajectory.transform, false);
        marker.transform.localScale = LANDING_MARKER_SCALE;
        MeshRenderer markerRenderer = marker.GetComponent<MeshRenderer>();
        markerRenderer.sharedMaterial = material;
        markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        marker.SetActive(false);

        GrenadeTrajectoryPreview preview = trajectory.AddComponent<GrenadeTrajectoryPreview>();
        SerializedObject serialized = new SerializedObject(preview);
        serialized.FindProperty("thrower").objectReferenceValue = thrower;
        serialized.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
        serialized.FindProperty("lineRenderer").objectReferenceValue = line;
        serialized.FindProperty("landingMarker").objectReferenceValue = marker.transform;
        serialized.FindProperty("collisionMask").intValue = LayerMask.GetMask(Constants.LAYER_GROUND);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateHud(WeaponHolder weaponHolder, GrenadeThrower thrower)
    {
        Canvas canvas = ProjectSceneBuilder.CreateCanvas("HUD");
        Transform root = canvas.transform;
        UiFactory.CreateImage(root, "Panel", HUD_PANEL_COLOR, UiFactory.BOTTOM_CENTER, Vector2.zero, new Vector2(1920f, 130f));
        TMP_Text controlsText = UiFactory.CreateText(root, "ControlsText", string.Empty, 24f, Color.white,
            UiFactory.BOTTOM_CENTER, new Vector2(0f, 20f), new Vector2(1860f, 40f), TextAlignmentOptions.Center);
        TMP_Text statusText = UiFactory.CreateText(root, "StatusText", string.Empty, 32f, new Color(1f, 0.85f, 0.4f),
            UiFactory.BOTTOM_CENTER, new Vector2(0f, 70f), new Vector2(1860f, 50f), TextAlignmentOptions.Center);

        Image chargeBackground = UiFactory.CreateImage(root, "ChargeBar", CHARGE_BACKGROUND_COLOR, UiFactory.BOTTOM_CENTER,
            new Vector2(0f, 150f), new Vector2(420f, 22f));
        Image chargeFill = UiFactory.CreateImage(chargeBackground.transform, "Fill", CHARGE_FILL_COLOR, UiFactory.CENTER,
            Vector2.zero, Vector2.zero);
        UiFactory.Stretch(chargeFill.rectTransform);
        chargeFill.type = Image.Type.Filled;
        chargeFill.fillMethod = Image.FillMethod.Horizontal;
        chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        chargeFill.fillAmount = 0f;
        chargeBackground.gameObject.SetActive(false);

        GameplayHud hud = canvas.gameObject.AddComponent<GameplayHud>();
        SerializedObject serialized = new SerializedObject(hud);
        serialized.FindProperty("weaponHolder").objectReferenceValue = weaponHolder;
        serialized.FindProperty("thrower").objectReferenceValue = thrower;
        serialized.FindProperty("controlsText").objectReferenceValue = controlsText;
        serialized.FindProperty("statusText").objectReferenceValue = statusText;
        serialized.FindProperty("chargeRoot").objectReferenceValue = chargeBackground.gameObject;
        serialized.FindProperty("chargeFill").objectReferenceValue = chargeFill;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
