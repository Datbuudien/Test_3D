using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class ScenarioSetupSceneFactory
{
    private const string MAIN_CAMERA_TAG = "MainCamera";
    private const int NAME_CHARACTER_LIMIT = 64;

    private static readonly Color BACKGROUND_COLOR = new Color(0.07f, 0.08f, 0.1f);
    internal static readonly Color CARD_COLOR = new Color(0.13f, 0.14f, 0.17f);
    private static readonly Color ACCENT_COLOR = new Color(0.85f, 0.55f, 0.15f);
    private static readonly Color BUTTON_COLOR = new Color(0.22f, 0.24f, 0.29f);
    private static readonly Color MUTED_TEXT_COLOR = new Color(0.7f, 0.72f, 0.76f);
    private static readonly Color AMBIENT_COLOR = new Color(0.55f, 0.56f, 0.6f);

    private static readonly Vector2 CHARACTER_CARD_POSITION = new Vector2(-420f, 110f);
    private static readonly Vector2 WEAPON_CARD_POSITION = new Vector2(420f, 110f);
    private static readonly Vector2 CARD_SIZE = new Vector2(640f, 640f);
    private static readonly Vector2 PREVIEW_SIZE = new Vector2(520f, 520f);
    private static readonly Vector2 ARROW_SIZE = new Vector2(70f, 70f);
    private static readonly Vector2 ACTION_BUTTON_SIZE = new Vector2(300f, 70f);

    private struct CardWidgets
    {
        public TMP_Text NameText;
        public Button PrevButton;
        public Button NextButton;
    }

    internal static void Build(string scenePath, string[] characterPaths, string[] weaponPaths,
        string characterTexturePath, string weaponTexturePath)
    {
        Scene scene = ProjectSceneBuilder.CreateEmptyScene();
        // Nap asset SAU khi tao scene (KI-19)
        CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
        WeaponDefinition[] weapons = ProjectSceneBuilder.LoadAssetsAtPaths<WeaponDefinition>(weaponPaths);
        RenderTexture characterTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(characterTexturePath);
        RenderTexture weaponTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(weaponTexturePath);
        CreateMainCamera();
        CreateLighting();
        PreviewStage characterStage = ProjectSceneBuilder.CreateStage("CharacterStage", true, characterTexture);
        PreviewStage weaponStage = ProjectSceneBuilder.CreateStage("WeaponStage", false, weaponTexture);
        ProjectSceneBuilder.CreateEventSystem();

        Canvas canvas = ProjectSceneBuilder.CreateCanvas("Canvas");
        Transform root = canvas.transform;
        UiFactory.Stretch(UiFactory.CreateImage(root, "Background", BACKGROUND_COLOR, UiFactory.CENTER, Vector2.zero, Vector2.zero).rectTransform);
        UiFactory.CreateText(root, "Title", "SCENARIO SETUP", 56f, Color.white, UiFactory.TOP_CENTER,
            new Vector2(0f, -40f), new Vector2(1200f, 80f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

        CardWidgets characterCard = CreateCard(root, "CharacterCard", "CHARACTER", CHARACTER_CARD_POSITION, characterTexture);
        CardWidgets weaponCard = CreateCard(root, "WeaponCard", "WEAPON", WEAPON_CARD_POSITION, weaponTexture);

        TMP_InputField nameInput = UiFactory.CreateInputField(root, "ScenarioNameInput", "Enter scenario name...", 30f,
            NAME_CHARACTER_LIMIT, UiFactory.CENTER, new Vector2(0f, -290f), new Vector2(900f, 70f));
        Button saveButton = UiFactory.CreateButton(root, "SaveButton", "Save Scenario", 28f, ACCENT_COLOR,
            UiFactory.CENTER, new Vector2(-330f, -385f), ACTION_BUTTON_SIZE);
        Button openFolderButton = UiFactory.CreateButton(root, "OpenFolderButton", "Open Folder", 28f, BUTTON_COLOR,
            UiFactory.CENTER, new Vector2(0f, -385f), ACTION_BUTTON_SIZE);
        Button startButton = UiFactory.CreateButton(root, "StartButton", "Start", 28f, BUTTON_COLOR,
            UiFactory.CENTER, new Vector2(330f, -385f), ACTION_BUTTON_SIZE);
        TMP_Text statusText = UiFactory.CreateText(root, "StatusText", string.Empty, 26f, Color.white,
            UiFactory.CENTER, new Vector2(0f, -465f), new Vector2(1700f, 60f), TextAlignmentOptions.Center);
        statusText.textWrappingMode = TextWrappingModes.Normal;

        ScenarioSetupPanel panel = canvas.gameObject.AddComponent<ScenarioSetupPanel>();
        SerializedObject serialized = new SerializedObject(panel);
        AssignArray(serialized.FindProperty("characters"), characters);
        AssignArray(serialized.FindProperty("weapons"), weapons);
        serialized.FindProperty("characterStage").objectReferenceValue = characterStage;
        serialized.FindProperty("weaponStage").objectReferenceValue = weaponStage;
        serialized.FindProperty("characterNameText").objectReferenceValue = characterCard.NameText;
        serialized.FindProperty("weaponNameText").objectReferenceValue = weaponCard.NameText;
        serialized.FindProperty("statusText").objectReferenceValue = statusText;
        serialized.FindProperty("scenarioNameInput").objectReferenceValue = nameInput;
        serialized.FindProperty("characterPrevButton").objectReferenceValue = characterCard.PrevButton;
        serialized.FindProperty("characterNextButton").objectReferenceValue = characterCard.NextButton;
        serialized.FindProperty("weaponPrevButton").objectReferenceValue = weaponCard.PrevButton;
        serialized.FindProperty("weaponNextButton").objectReferenceValue = weaponCard.NextButton;
        serialized.FindProperty("saveButton").objectReferenceValue = saveButton;
        serialized.FindProperty("openFolderButton").objectReferenceValue = openFolderButton;
        serialized.FindProperty("startButton").objectReferenceValue = startButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static CardWidgets CreateCard(Transform parent, string cardName, string title, Vector2 position, Texture previewTexture)
    {
        Transform card = UiFactory.CreateImage(parent, cardName, CARD_COLOR, UiFactory.CENTER, position, CARD_SIZE).transform;
        UiFactory.CreateText(card, "Label", title, 26f, MUTED_TEXT_COLOR, UiFactory.TOP_CENTER,
            new Vector2(0f, -14f), new Vector2(600f, 40f), TextAlignmentOptions.Center).characterSpacing = 8f;
        UiFactory.CreateRawImage(card, "Preview", previewTexture, UiFactory.CENTER, new Vector2(0f, 10f), PREVIEW_SIZE);

        return new CardWidgets
        {
            NameText = UiFactory.CreateText(card, "NameText", string.Empty, 32f, Color.white, UiFactory.BOTTOM_CENTER,
                new Vector2(0f, 18f), new Vector2(420f, 60f), TextAlignmentOptions.Center),
            PrevButton = UiFactory.CreateButton(card, "PrevButton", "<", 36f, BUTTON_COLOR, UiFactory.BOTTOM_CENTER,
                new Vector2(-250f, 14f), ARROW_SIZE),
            NextButton = UiFactory.CreateButton(card, "NextButton", ">", 36f, BUTTON_COLOR, UiFactory.BOTTOM_CENTER,
                new Vector2(250f, 14f), ARROW_SIZE)
        };
    }

    private static void CreateMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera") { tag = MAIN_CAMERA_TAG };
        Camera mainCamera = cameraObject.AddComponent<Camera>();
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = BACKGROUND_COLOR;
        mainCamera.cullingMask = ~(1 << LayerMask.NameToLayer(Constants.LAYER_PREVIEW));
        mainCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        Light keyLight = lightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.3f;
        keyLight.shadows = LightShadows.Soft;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AMBIENT_COLOR;
        RenderSettings.sun = keyLight;
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
