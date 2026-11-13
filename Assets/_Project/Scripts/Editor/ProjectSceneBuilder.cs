using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dung tu dong: CharacterDefinition, RenderTexture, scene Bootstrap (NetworkManager), ScenarioSetup (UI + preview + Host/Join), noi Gameplay (spawner, HUD, quy dao, Esc), Build Settings
public static class ProjectSceneBuilder
{
    private const string MENU_BUILD_ALL = "Tools/Test3D/Build Scenario Setup + Wire Gameplay";
    private const string DIALOG_TITLE = "Test3D Builder";
    private const string DIALOG_OK = "OK";
    private const string LOG_PREFIX = "[ProjectSceneBuilder]";

    private const string SETUP_SCENE_PATH = "Assets/_Project/Scenes/ScenarioSetup.unity";
    internal const string GAMEPLAY_SCENE_PATH = "Assets/_Project/Scenes/Gameplay.unity";
    private const string DATA_FOLDER = "Assets/_Project/Data";
    private const string TEXTURE_FOLDER = "Assets/_Project/Textures";
    private const string CHARACTER_PREFAB_FOLDER = "Assets/_Project/Prefabs/Characters";
    internal const string TRAJECTORY_MATERIAL_PATH = "Assets/_Project/VFX/M_FX_Trajectory.mat";
    private const string CHARACTER_RT_PATH = TEXTURE_FOLDER + "/RT_CharacterPreview.renderTexture";
    private const string WEAPON_RT_PATH = TEXTURE_FOLDER + "/RT_WeaponPreview.renderTexture";
    private const string AK47_DEFINITION_PATH = DATA_FOLDER + "/WPN_AK47.asset";
    private const string GRENADE_DEFINITION_PATH = DATA_FOLDER + "/WPN_Grenade.asset";
    internal const string LOCAL_STORE_PROVIDER_PATH = DATA_FOLDER + "/ScenarioStore_Local.asset";

    private const int RENDER_TEXTURE_SIZE = 1024;
    private const int BUILD_WINDOW_WIDTH = 1280;
    private const int BUILD_WINDOW_HEIGHT = 720;
    private const int RENDER_TEXTURE_DEPTH = 24;
    private const float PREVIEW_FIELD_OF_VIEW = 35f;
    private const float CHARACTER_DISTANCE = 3.6f;
    private const float DEFAULT_WEAPON_DISTANCE = 1.5f;
    private const float AK47_DISTANCE = 1.8f;
    private const float GRENADE_DISTANCE = 0.45f;
    private static readonly Vector3 CHARACTER_STAGE_POSITION = new Vector3(0f, -500f, 0f);
    private static readonly Vector3 WEAPON_STAGE_POSITION = new Vector3(100f, -500f, 0f);
    private static readonly Vector3 CHARACTER_LOOK_OFFSET = new Vector3(0f, 0.95f, 0f);
    private static readonly Vector3 CHARACTER_VIEW_DIRECTION = new Vector3(0f, -0.2f, -1f);
    private static readonly Vector3 WEAPON_VIEW_DIRECTION = new Vector3(-1f, -0.3f, -0.35f);

    private static readonly (string assetName, string id, string displayName, string prefabName)[] DEFAULT_CHARACTERS =
    {
        ("CHR_DEF_PolishSoldier", "polish_soldier", "Polish Soldier", "CHR_PolishSoldier"),
        ("CHR_DEF_GreenSwat", "green_swat", "Green SWAT", "CHR_GreenSwat")
    };

    [MenuItem(MENU_BUILD_ALL)]
    private static void BuildAll()
    {
        if (!EnsureTextMeshProResources() || !EnsurePreviewLayer())
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        // NewScene/OpenScene (Single) do asset khong con tham chieu -> bien C# giu asset cu thanh null khi gan.
        // Chi truyen duong dan, factory nap lai SAU khi scene da mo.
        if (!TryCollectCatalogPaths(out string[] characterPaths, out string[] weaponPaths))
        {
            return;
        }

        EnsureRenderTexture(CHARACTER_RT_PATH);
        EnsureRenderTexture(WEAPON_RT_PATH);
        LoadTrajectoryMaterial();
        string storeProviderPath = EnsureStoreProvider();
        AssetDatabase.SaveAssets();

        ScenarioSetupSceneFactory.Build(SETUP_SCENE_PATH, characterPaths, weaponPaths, CHARACTER_RT_PATH, WEAPON_RT_PATH, storeProviderPath);
        BootstrapSceneFactory.Build(BootstrapPlayModeStarter.BOOTSTRAP_SCENE_PATH, characterPaths, weaponPaths);
        bool gameplayWired = NetworkPlayerBuilder.Build(GAMEPLAY_SCENE_PATH, characterPaths, weaponPaths)
            && NetworkGrenadeBuilder.Build()
            && ScorchDecalBuilder.Build(GAMEPLAY_SCENE_PATH)
            && NetworkPropsBuilder.Build(GAMEPLAY_SCENE_PATH) >= 0
            && GameplaySceneWiring.Wire(GAMEPLAY_SCENE_PATH, TRAJECTORY_MATERIAL_PATH, storeProviderPath, characterPaths);
        UpdateBuildSettings();
        ConfigurePlayerSettings();
        BootstrapPlayModeStarter.Apply();
        EditorSceneManager.OpenScene(SETUP_SCENE_PATH, OpenSceneMode.Single);

        string gameplayMessage = gameplayWired ? "Gameplay wired (NET_Player, NET_GrenadeProjectile, scorch decals, networked props, spawner, HUD, trajectory, F5 positions, Esc)." : "Gameplay NOT wired - see Console.";
        Debug.Log($"{LOG_PREFIX} Done. Bootstrap + {SETUP_SCENE_PATH} built. {gameplayMessage}");
        EditorUtility.DisplayDialog(DIALOG_TITLE, $"Bootstrap + ScenarioSetup scenes built.\n{gameplayMessage}\nBuild Settings: Bootstrap, ScenarioSetup, Gameplay.\n\nPress Play from any scene - Play Mode always starts in Bootstrap.", DIALOG_OK);
    }

    internal static bool TryCollectCatalogPaths(out string[] characterPaths, out string[] weaponPaths)
    {
        CharacterDefinition[] characters = EnsureCharacterDefinitions();
        WeaponDefinition[] weapons = LoadWeaponDefinitions();
        characterPaths = ToAssetPaths(characters);
        weaponPaths = ToAssetPaths(weapons);
        if (characters.Length > 0 && weapons.Length > 0)
        {
            return true;
        }

        EditorUtility.DisplayDialog(DIALOG_TITLE, "Missing character prefabs or WPN_AK47 / WPN_Grenade definitions in Assets/_Project/Data.", DIALOG_OK);
        return false;
    }

    // TMP chua import Essential Resources -> text khong hien; import roi yeu cau chay lai menu
    private static bool EnsureTextMeshProResources()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            return true;
        }

        TMP_PackageResourceImporter.ImportResources(true, false, false);
        EditorUtility.DisplayDialog(DIALOG_TITLE, "TextMeshPro Essential Resources are being imported.\nWait for the import to finish, then run this menu again.", DIALOG_OK);
        return false;
    }

    private static bool EnsurePreviewLayer()
    {
        if (LayerMask.NameToLayer(Constants.LAYER_PREVIEW) >= 0)
        {
            return true;
        }

        EditorUtility.DisplayDialog(DIALOG_TITLE, $"Layer '{Constants.LAYER_PREVIEW}' does not exist. Add it in Project Settings > Tags and Layers.", DIALOG_OK);
        return false;
    }

    private static CharacterDefinition[] EnsureCharacterDefinitions()
    {
        List<CharacterDefinition> existing = LoadAssets<CharacterDefinition>();
        if (existing.Count > 0)
        {
            return existing.ToArray();
        }

        List<CharacterDefinition> created = new List<CharacterDefinition>();
        foreach (var entry in DEFAULT_CHARACTERS)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CHARACTER_PREFAB_FOLDER}/{entry.prefabName}.prefab");
            if (prefab == null || !prefab.TryGetComponent(out CharacterVisual visual))
            {
                Debug.LogWarning($"{LOG_PREFIX} Prefab '{entry.prefabName}' missing or has no CharacterVisual - skipped.");
                continue;
            }

            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = entry.id;
            serialized.FindProperty("displayName").stringValue = entry.displayName;
            serialized.FindProperty("visualPrefab").objectReferenceValue = visual;
            serialized.FindProperty("previewPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("previewDistance").floatValue = CHARACTER_DISTANCE;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, $"{DATA_FOLDER}/{entry.assetName}.asset");
            created.Add(definition);
        }

        return created.ToArray();
    }

    private static WeaponDefinition[] LoadWeaponDefinitions()
    {
        WeaponDefinition ak47 = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AK47_DEFINITION_PATH);
        WeaponDefinition grenade = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(GRENADE_DEFINITION_PATH);
        if (ak47 == null || grenade == null)
        {
            return new WeaponDefinition[0];
        }

        TuneWeaponDistance(ak47, AK47_DISTANCE);
        TuneWeaponDistance(grenade, GRENADE_DISTANCE);
        return new[] { ak47, grenade };
    }

    // Chi doi khi con gia tri mac dinh -> khong de len so nguoi dung da chinh
    private static void TuneWeaponDistance(WeaponDefinition definition, float distance)
    {
        SerializedObject serialized = new SerializedObject(definition);
        SerializedProperty property = serialized.FindProperty("previewDistance");
        if (Mathf.Abs(property.floatValue - DEFAULT_WEAPON_DISTANCE) < Mathf.Epsilon)
        {
            property.floatValue = distance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
    }

    // Da co provider nao (local/server nguoi dung tao) thi dung lai, khong thi tao ban luu file local
    private static string EnsureStoreProvider()
    {
        // Loc theo kieu bang code: filter "t:LopCha" cua FindAssets khong chac bat duoc asset lop con
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DATA_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is AbstractScenarioStoreProvider)
            {
                return path;
            }
        }

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LocalScenarioStoreProvider>(), LOCAL_STORE_PROVIDER_PATH);
        return LOCAL_STORE_PROVIDER_PATH;
    }

    private static RenderTexture EnsureRenderTexture(string path)
    {
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (texture != null)
        {
            return texture;
        }

        texture = new RenderTexture(RENDER_TEXTURE_SIZE, RENDER_TEXTURE_SIZE, RENDER_TEXTURE_DEPTH, RenderTextureFormat.ARGB32);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    // Luon cap nhat thong so de material cu (additive mo) duoc doi sang mau dac
    private static Material LoadTrajectoryMaterial()
    {
        return ExplosionPrefabBuilder.CreateSolidMaterial(TRAJECTORY_MATERIAL_PATH);
    }

    // Test mang (N6): cua so mat focus van phai chay, neu khong ket noi bi ngat; cua so de dat 2 ban build canh nhau khi quay video
    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = BUILD_WINDOW_WIDTH;
        PlayerSettings.defaultScreenHeight = BUILD_WINDOW_HEIGHT;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootstrapPlayModeStarter.BOOTSTRAP_SCENE_PATH, true),
            new EditorBuildSettingsScene(SETUP_SCENE_PATH, true),
            new EditorBuildSettingsScene(GAMEPLAY_SCENE_PATH, true)
        };
    }

    internal static T[] LoadAssetsAtPaths<T>(string[] paths) where T : Object
    {
        List<T> assets = new List<T>(paths.Length);
        foreach (string path in paths)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                assets.Add(asset);
            }
            else
            {
                Debug.LogError($"{LOG_PREFIX} Could not load {typeof(T).Name} at '{path}'.");
            }
        }

        return assets.ToArray();
    }

    private static string[] ToAssetPaths(Object[] assets)
    {
        string[] paths = new string[assets.Length];
        for (int index = 0; index < assets.Length; index++)
        {
            paths[index] = AssetDatabase.GetAssetPath(assets[index]);
        }

        return paths;
    }

    private static List<T> LoadAssets<T>() where T : Object
    {
        List<T> assets = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        assets.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return assets;
    }

    internal static Camera CreatePreviewCamera(Transform parent, RenderTexture targetTexture)
    {
        GameObject cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(parent, false);
        Camera previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        // Mau trung mau the + alpha 0: neu URP bo kenh alpha thi nen van khop mau the
        Color clearColor = ScenarioSetupSceneFactory.CARD_COLOR;
        clearColor.a = 0f;
        previewCamera.backgroundColor = clearColor;
        previewCamera.cullingMask = 1 << LayerMask.NameToLayer(Constants.LAYER_PREVIEW);
        previewCamera.targetTexture = targetTexture;
        previewCamera.fieldOfView = PREVIEW_FIELD_OF_VIEW;
        previewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        return previewCamera;
    }

    internal static PreviewStage CreateStage(string stageName, bool isCharacter, RenderTexture targetTexture)
    {
        GameObject stage = new GameObject(stageName);
        stage.transform.position = isCharacter ? CHARACTER_STAGE_POSITION : WEAPON_STAGE_POSITION;
        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(stage.transform, false);
        Camera previewCamera = CreatePreviewCamera(stage.transform, targetTexture);

        PreviewStage previewStage = stage.AddComponent<PreviewStage>();
        SerializedObject serialized = new SerializedObject(previewStage);
        serialized.FindProperty("previewCamera").objectReferenceValue = previewCamera;
        serialized.FindProperty("pivot").objectReferenceValue = pivot.transform;
        serialized.FindProperty("lookOffset").vector3Value = isCharacter ? CHARACTER_LOOK_OFFSET : Vector3.zero;
        serialized.FindProperty("viewDirection").vector3Value = isCharacter ? CHARACTER_VIEW_DIRECTION : WEAPON_VIEW_DIRECTION;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Dat camera tam thoi de Camera Preview trong Editor nhin thay pivot
        Vector3 viewDirection = isCharacter ? CHARACTER_VIEW_DIRECTION : WEAPON_VIEW_DIRECTION;
        Vector3 target = pivot.transform.position + (isCharacter ? CHARACTER_LOOK_OFFSET : Vector3.zero);
        previewCamera.transform.position = target - viewDirection.normalized * (isCharacter ? CHARACTER_DISTANCE : AK47_DISTANCE);
        previewCamera.transform.LookAt(target);
        return previewStage;
    }

    internal static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        // Project chi bat Input System moi -> StandaloneInputModule se nem exception (KI-2)
        // AddComponent trong Editor goi Reset() -> module tu gan DefaultInputActions.inputactions cua package (asset co luu).
        // KHONG goi AssignDefaultActions(): no tao asset tam giu trong bien static, OpenScene (Single) do asset do
        // -> lan chay menu thu 2 trong cung domain nem "Action must be part of an InputActionAsset" (KI-28)
        InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        if (module.actionsAsset == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} EventSystem UI module has no actions asset - it will fall back to default actions at runtime.");
        }
    }

    internal static Canvas CreateCanvas(string canvasName)
    {
        GameObject canvasObject = new GameObject(canvasName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UiFactory.REFERENCE_RESOLUTION;
        // Expand: ca khung 1920x1080 luon nam tron trong man hinh o moi ti le (cua so MPPM hep/rong) -> khung khong de len tieu de
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // Scene da co: mo lai roi xoa het object thay vi NewScene -> scene luon co duong dan.
    // NewScene (chua luu, path rong) lam virtual player cua MPPM dang mo nem "Scene file not found: ''"
    internal static Scene CreateEmptyScene(string scenePath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(rootObject);
        }

        return scene;
    }
}
