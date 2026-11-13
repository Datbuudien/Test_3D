using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Noi Gameplay: SceneNavigator (Esc), quy dao nem, HUD, ScenarioPositionRecorder (F5), LocalPlayerBinder. Player la prefab mang sinh luc chay (N2)
// -> cac thanh phan nay khong giu tham chieu toi Player, LocalPlayerBinder noi vao luc chay. Chay lai se xoa ban cu roi tao moi
internal static class GameplaySceneWiring
{
    private const string LOG_PREFIX = "[GameplaySceneWiring]";
    private const string LANDING_MARKER_MATERIAL_PATH = "Assets/_Project/VFX/M_FX_LandingMarker.mat";
    private const string MAIN_CAMERA_TAG = "MainCamera";
    private const float TRAJECTORY_WIDTH = 0.08f;
    private static readonly Vector3 LANDING_MARKER_SCALE = new Vector3(0.9f, 0.01f, 0.9f);
    private static readonly Color TRAJECTORY_START_COLOR = new Color(0.85f, 1f, 0.1f, 1f);
    private static readonly Color TRAJECTORY_END_COLOR = new Color(1f, 0.1f, 0.15f, 1f);
    private static readonly Color LANDING_MARKER_COLOR = new Color(1f, 0.1f, 0.15f, 0.85f);
    private static readonly Color HUD_PANEL_COLOR = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color CHARGE_BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color CHARGE_FILL_COLOR = new Color(1f, 0.6f, 0.15f);

    internal static bool Wire(string scenePath, string trajectoryMaterialPath, string storeProviderPath, string[] characterPaths)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Nap asset SAU khi mo scene (KI-19)
        Material trajectoryMaterial = AssetDatabase.LoadAssetAtPath<Material>(trajectoryMaterialPath);
        AbstractScenarioStoreProvider storeProvider = AssetDatabase.LoadAssetAtPath<AbstractScenarioStoreProvider>(storeProviderPath);
        CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
        if (storeProvider == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} No store provider at '{storeProviderPath}' - F5 will not save positions.");
        }
        CinemachineCamera cinemachineCamera = Object.FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        Camera mainCamera = FindMainCamera();
        if (cinemachineCamera == null || mainCamera == null)
        {
            Debug.LogError($"{LOG_PREFIX} Gameplay needs a CinemachineCamera and a Camera tagged {MAIN_CAMERA_TAG}.");
            return false;
        }

        if (Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include) != null)
        {
            Debug.LogWarning($"{LOG_PREFIX} Gameplay still contains a non-networked Player - run {nameof(NetworkPlayerBuilder)} first.");
        }

        RemoveExisting<SceneNavigator>();
        RemoveExisting<GrenadeTrajectoryPreview>();
        RemoveExisting<GameplayHud>();
        RemoveExisting<LocalPlayerBinder>();
        RemoveExisting<ScenarioPositionRecorder>();

        // Player cu da thanh prefab -> target cu thanh Missing; binder gan lai luc chay
        cinemachineCamera.Follow = null;
        cinemachineCamera.LookAt = null;
        EditorUtility.SetDirty(cinemachineCamera);

        SceneNavigator navigator = new GameObject("SceneNavigator").AddComponent<SceneNavigator>();
        GrenadeTrajectoryPreview trajectory = CreateTrajectory(trajectoryMaterial);
        GameplayHud hud = CreateHud();
        ScenarioPositionRecorder recorder = CreateRecorder(storeProvider, characters, hud);
        CreateBinder(cinemachineCamera, mainCamera.transform, hud, trajectory, navigator, recorder);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    private static Camera FindMainCamera()
    {
        foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.CompareTag(MAIN_CAMERA_TAG))
            {
                return candidate;
            }
        }

        return null;
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

    private static void CreateBinder(CinemachineCamera cinemachineCamera, Transform mainCamera, GameplayHud hud,
        GrenadeTrajectoryPreview trajectory, SceneNavigator navigator, ScenarioPositionRecorder recorder)
    {
        LocalPlayerBinder binder = new GameObject("LocalPlayerBinder").AddComponent<LocalPlayerBinder>();
        SerializedObject serialized = new SerializedObject(binder);
        serialized.FindProperty("cinemachineCamera").objectReferenceValue = cinemachineCamera;
        serialized.FindProperty("mainCamera").objectReferenceValue = mainCamera;
        serialized.FindProperty("hud").objectReferenceValue = hud;
        serialized.FindProperty("trajectoryPreview").objectReferenceValue = trajectory;
        serialized.FindProperty("sceneNavigator").objectReferenceValue = navigator;
        serialized.FindProperty("positionRecorder").objectReferenceValue = recorder;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ScenarioPositionRecorder CreateRecorder(AbstractScenarioStoreProvider storeProvider, CharacterDefinition[] characters, GameplayHud hud)
    {
        ScenarioPositionRecorder recorder = new GameObject("ScenarioPositionRecorder").AddComponent<ScenarioPositionRecorder>();
        SerializedObject serialized = new SerializedObject(recorder);
        serialized.FindProperty("storeProvider").objectReferenceValue = storeProvider;
        serialized.FindProperty("hud").objectReferenceValue = hud;
        SerializedProperty characterArray = serialized.FindProperty("characters");
        characterArray.arraySize = characters.Length;
        for (int index = 0; index < characters.Length; index++)
        {
            characterArray.GetArrayElementAtIndex(index).objectReferenceValue = characters[index];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return recorder;
    }

    private static GrenadeTrajectoryPreview CreateTrajectory(Material material)
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
        markerRenderer.sharedMaterial = CreateMarkerMaterial();
        markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
        marker.SetActive(false);

        GrenadeTrajectoryPreview preview = trajectory.AddComponent<GrenadeTrajectoryPreview>();
        SerializedObject serialized = new SerializedObject(preview);
        serialized.FindProperty("lineRenderer").objectReferenceValue = line;
        serialized.FindProperty("landingMarker").objectReferenceValue = marker.transform;
        serialized.FindProperty("collisionMask").intValue = LayerMask.GetMask(Constants.LAYER_GROUND);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return preview;
    }

    // Mesh khong co vertex color nhu LineRenderer -> mau dat thang tren material
    private static Material CreateMarkerMaterial()
    {
        Material material = ExplosionPrefabBuilder.CreateSolidMaterial(LANDING_MARKER_MATERIAL_PATH);
        material.SetColor("_BaseColor", LANDING_MARKER_COLOR);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static GameplayHud CreateHud()
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
        serialized.FindProperty("controlsText").objectReferenceValue = controlsText;
        serialized.FindProperty("statusText").objectReferenceValue = statusText;
        serialized.FindProperty("chargeRoot").objectReferenceValue = chargeBackground.gameObject;
        serialized.FindProperty("chargeFill").objectReferenceValue = chargeFill;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return hud;
    }
}
