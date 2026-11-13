using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// N4: Decal Renderer Feature (DBuffer + Decal Layers) tren PC_Renderer, rendering layer "Environment" cho BG,
// texture/material/prefab vet chay sinh bang code, ScorchMarkRegistry (in-scene NetworkObject) trong Gameplay. Chay lai an toan, giu GUID
internal static class ScorchDecalBuilder
{
    private const string MENU_BUILD = "Tools/Test3D/Build Scorch Decal";
    private const string LOG_PREFIX = "[ScorchDecalBuilder]";
    private const string RENDERER_DATA_PATH = "Assets/Settings/PC_Renderer.asset";
    private const string TAG_MANAGER_PATH = "ProjectSettings/TagManager.asset";
    private const string TEXTURE_PATH = "Assets/_Project/Textures/T_FX_Scorch.png";
    private const string MATERIAL_PATH = "Assets/_Project/VFX/M_FX_ScorchDecal.mat";
    private const string PREFAB_PATH = "Assets/_Project/VFX/FX_ScorchDecal.prefab";
    private const string DECAL_SHADER_NAME = "Shader Graphs/Decal";
    private const string BASE_MAP_PROPERTY = "Base_Map";
    private const string NORMAL_BLEND_PROPERTY = "Normal_Blend";
    private const string ENVIRONMENT_LAYER_NAME = "Environment";
    private const string REGISTRY_NAME = "ScorchMarkRegistry";
    private const string DECAL_FEATURE_NAME = "Decal";
    private const int MAX_RENDERING_LAYERS = 32;
    // Thu tu enum noi bo cua URP 17.3: DecalTechniqueOption { Automatic, DBuffer, ScreenSpace },
    // DecalSurfaceData { Albedo, AlbedoNormal, AlbedoNormalMAOS }, DecalNormalBlend { Low, Medium, High }
    private const int TECHNIQUE_DBUFFER = 1;
    private const int SURFACE_DATA_ALBEDO = 0;
    private const int NORMAL_BLEND_MEDIUM = 1;

    private const int TEXTURE_SIZE = 512;
    // Nguoi dung thay 2.5 m qua nho -> 4 m (ban kinh no ~6 m); depth 2 m de van om qua bac them cao khi vet rong hon
    private static readonly Vector3 DECAL_SIZE = new Vector3(4f, 4f, 2f);
    private const float DECAL_DRAW_DISTANCE = 120f;
    private const float DECAL_FADE_SCALE = 0.9f;
    // Goc giua phap tuyen diem no va mat nhan: mat nghieng it nhan du, mat vuong goc (tuong, mep via he) mo dan -> khong keo vet dai
    private const float DECAL_START_ANGLE_FADE = 50f;
    private const float DECAL_END_ANGLE_FADE = 95f;

    [MenuItem(MENU_BUILD)]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        bool built = Build(ProjectSceneBuilder.GAMEPLAY_SCENE_PATH);
        EditorUtility.DisplayDialog("Test3D Builder", built ? "Scorch decal built and Gameplay wired." : "Build failed - see Console.", "OK");
    }

    internal static bool Build(string gameplayScenePath)
    {
        int environmentLayer = EnsureRenderingLayer(ENVIRONMENT_LAYER_NAME);
        if (environmentLayer < 0 || !EnsureDecalFeature())
        {
            return false;
        }

        Texture2D texture = BuildTexture();
        Material material = BuildMaterial(texture);
        if (material == null || !BuildPrefab(material, environmentLayer))
        {
            return false;
        }

        if (!WireGameplay(gameplayScenePath, environmentLayer))
        {
            return false;
        }

        Debug.Log($"{LOG_PREFIX} Done. Decal feature (DBuffer + Decal Layers) on PC_Renderer, rendering layer '{ENVIRONMENT_LAYER_NAME}' = {environmentLayer}, {PREFAB_PATH}, {REGISTRY_NAME} in Gameplay.");
        return true;
    }

    // Them ten vao danh sach Rendering Layers (TagManager, Unity 6) thay vi doi ten Light Layer co san
    private static int EnsureRenderingLayer(string layerName)
    {
        Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(TAG_MANAGER_PATH);
        if (tagManagerAssets.Length == 0)
        {
            Debug.LogError($"{LOG_PREFIX} {TAG_MANAGER_PATH} not found.");
            return -1;
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("m_RenderingLayers");
        if (layers == null || !layers.isArray)
        {
            Debug.LogError($"{LOG_PREFIX} TagManager has no m_RenderingLayers array.");
            return -1;
        }

        for (int index = 0; index < layers.arraySize; index++)
        {
            if (layers.GetArrayElementAtIndex(index).stringValue == layerName)
            {
                return index;
            }
        }

        if (layers.arraySize >= MAX_RENDERING_LAYERS)
        {
            Debug.LogError($"{LOG_PREFIX} All {MAX_RENDERING_LAYERS} rendering layers are used - free one and run again.");
            return -1;
        }

        int newIndex = layers.arraySize;
        layers.arraySize++;
        layers.GetArrayElementAtIndex(newIndex).stringValue = layerName;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return newIndex;
    }

    // DBuffer: ghi de mau nen (albedo) cua mat duong TRUOC khi tinh anh sang -> vet den that. Screen Space ve decal thanh lop
    // co anh sang rieng, phan chieu bau troi lam den thanh xam (nguoi dung bao "mo nhat"). Shader glTFast (URP Lit Shader Graph,
    // Receive Decals = true) nhan DBuffer. Decal Layers: chi BG nhan, nguoi/vu khi khong; Angle Fade cung can Decal Layers (KI-27)
    private static bool EnsureDecalFeature()
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RENDERER_DATA_PATH);
        if (rendererData == null)
        {
            Debug.LogError($"{LOG_PREFIX} {RENDERER_DATA_PATH} not found.");
            return false;
        }

        DecalRendererFeature feature = null;
        foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
        {
            if (existing is DecalRendererFeature decalFeature)
            {
                feature = decalFeature;
                break;
            }
        }

        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = DECAL_FEATURE_NAME;
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            SerializedObject rendererSerialized = new SerializedObject(rendererData);
            SerializedProperty features = rendererSerialized.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = rendererSerialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject featureSerialized = new SerializedObject(feature);
        featureSerialized.FindProperty("m_Settings.technique").enumValueIndex = TECHNIQUE_DBUFFER;
        featureSerialized.FindProperty("m_Settings.dBufferSettings.surfaceData").enumValueIndex = SURFACE_DATA_ALBEDO;
        featureSerialized.FindProperty("m_Settings.decalLayers").boolValue = true;
        featureSerialized.FindProperty("m_Settings.screenSpaceSettings.normalBlend").enumValueIndex = NORMAL_BLEND_MEDIUM;
        featureSerialized.ApplyModifiedPropertiesWithoutUndo();
        feature.SetActive(true);

        rendererData.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        return true;
    }

    // Vet chay sinh bang code (khong lo license): tam den, vien loang theo noise, vai tia, alpha mem
    private static Texture2D BuildTexture()
    {
        Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TEXTURE_SIZE * TEXTURE_SIZE];
        float half = TEXTURE_SIZE * 0.5f;
        for (int y = 0; y < TEXTURE_SIZE; y++)
        {
            for (int x = 0; x < TEXTURE_SIZE; x++)
            {
                pixels[y * TEXTURE_SIZE + x] = ComputeScorchPixel((x - half) / half, (y - half) / half, x, y);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(TEXTURE_PATH, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(TEXTURE_PATH, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TEXTURE_PATH);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
    }

    private static Color32 ComputeScorchPixel(float u, float v, int x, int y)
    {
        float radius = Mathf.Sqrt(u * u + v * v);
        float angle = Mathf.Atan2(v, u);
        float cosAngle = Mathf.Cos(angle);
        float sinAngle = Mathf.Sin(angle);
        // Noise theo goc (cos/sin -> lien tuc, khong co duong noi o +-180 do)
        float edgeNoise = Mathf.PerlinNoise(cosAngle * 1.6f + 10f, sinAngle * 1.6f + 10f);
        float spokeNoise = Mathf.PerlinNoise(cosAngle * 7f + 40f, sinAngle * 7f + 40f);
        float grain = Mathf.PerlinNoise(x * 0.18f + 11f, y * 0.18f + 5f);

        // Muc tieu: nhin thay ro diem no -> loi DEN DAC alpha 1 toi 85% ban kinh, chi mep loang theo noise
        float edge = 0.62f + 0.2f * edgeNoise + 0.25f * Mathf.Pow(spokeNoise, 4f);
        float normalizedRadius = radius / Mathf.Max(edge, 0.01f);
        float alpha = 1f - EdgeSmoothStep(0.85f, 1f, normalizedRadius);
        float gray = 0.03f * EdgeSmoothStep(0.6f, 1f, normalizedRadius) * Mathf.Lerp(0.8f, 1.2f, grain);
        Color color = new Color(gray * 1.08f, gray, gray * 0.9f, alpha);
        return color;
    }

    // smoothstep kieu shader (0 truoc edge0, 1 sau edge1). KHONG dung Mathf.SmoothStep: ham do noi suy GIUA from/to
    // -> SmoothStep(0.75, 1, 0) = 0.75, lam alpha tam vet chi con ~30% (nguyen nhan vet "mo nhat", KI-27)
    private static float EdgeSmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static Material BuildMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find(DECAL_SHADER_NAME);
        if (shader == null)
        {
            Debug.LogError($"{LOG_PREFIX} Shader '{DECAL_SHADER_NAME}' not found (URP Decal Shader Graph).");
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
        }

        material.shader = shader;
        material.SetTexture(BASE_MAP_PROPERTY, texture);
        // Khong co normal map: Normal_Blend = 0 de khong ghi de phap tuyen mat duong (vet chay phang lep)
        material.SetFloat(NORMAL_BLEND_PROPERTY, 0f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static bool BuildPrefab(Material material, int environmentLayer)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            GameObject skeleton = new GameObject("FX_ScorchDecal");
            PrefabUtility.SaveAsPrefabAsset(skeleton, PREFAB_PATH);
            Object.DestroyImmediate(skeleton);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            DecalProjector projector = GetOrAdd<DecalProjector>(root);
            projector.material = material;
            projector.size = DECAL_SIZE;
            // Pivot 0: hop chieu nam GIUA diem no -> bao ca phia tren lan duoi, om qua mep bac them / doc
            projector.pivot = Vector3.zero;
            projector.drawDistance = DECAL_DRAW_DISTANCE;
            projector.fadeScale = DECAL_FADE_SCALE;
            projector.startAngleFade = DECAL_START_ANGLE_FADE;
            projector.endAngleFade = DECAL_END_ANGLE_FADE;
            projector.uvScale = Vector2.one;
            projector.uvBias = Vector2.zero;
            projector.fadeFactor = 1f;
            projector.renderingLayerMask = 1u << environmentLayer;

            ScorchDecal decal = GetOrAdd<ScorchDecal>(root);
            SerializedObject serialized = new SerializedObject(decal);
            serialized.FindProperty("projector").objectReferenceValue = projector;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool saved);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool WireGameplay(string scenePath, int environmentLayer)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // Nap prefab SAU khi mo scene (KI-19)
        ScorchDecal decalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH).GetComponent<ScorchDecal>();
        int environmentCount = AssignEnvironmentLayer(environmentLayer);
        if (environmentCount == 0)
        {
            Debug.LogWarning($"{LOG_PREFIX} No Renderer on layer '{Constants.LAYER_GROUND}' in Gameplay - decals will not show. Run Setup Selected Environment on BG.");
        }

        ScorchMarkRegistry registry = Object.FindAnyObjectByType<ScorchMarkRegistry>(FindObjectsInactive.Include);
        if (registry == null)
        {
            GameObject registryObject = new GameObject(REGISTRY_NAME);
            registryObject.AddComponent<NetworkObject>();
            registry = registryObject.AddComponent<ScorchMarkRegistry>();
        }

        SerializedObject registrySerialized = new SerializedObject(registry);
        registrySerialized.FindProperty("decalPrefab").objectReferenceValue = decalPrefab;
        registrySerialized.ApplyModifiedPropertiesWithoutUndo();

        NetworkPlayerSpawner spawner = Object.FindAnyObjectByType<NetworkPlayerSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            Debug.LogError($"{LOG_PREFIX} Gameplay has no NetworkPlayerSpawner - run Build Network Player first.");
            return false;
        }

        SerializedObject spawnerSerialized = new SerializedObject(spawner);
        spawnerSerialized.FindProperty("scorchMarkRegistry").objectReferenceValue = registry;
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        NetworkEditorUtility.SaveSceneWithFreshNetworkIds(scene);
        return true;
    }

    // Moi Renderer cua moi truong (layer Ground - EnvironmentSetupTool) giu bit Default cho light layer, them bit Environment cho decal
    private static int AssignEnvironmentLayer(int environmentLayer)
    {
        int groundLayer = LayerMask.NameToLayer(Constants.LAYER_GROUND);
        uint environmentBit = 1u << environmentLayer;
        int count = 0;
        foreach (Renderer environmentRenderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (environmentRenderer.gameObject.layer != groundLayer)
            {
                continue;
            }

            if ((environmentRenderer.renderingLayerMask & environmentBit) == 0)
            {
                environmentRenderer.renderingLayerMask |= environmentBit;
                EditorUtility.SetDirty(environmentRenderer);
            }

            count++;
        }

        return count;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
