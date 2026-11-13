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
    private const int ADDRESS_CHARACTER_LIMIT = 15;
    private const int PORT_CHARACTER_LIMIT = 5;

    private static readonly Color BACKGROUND_COLOR = new Color(0.07f, 0.08f, 0.1f);
    internal static readonly Color CARD_COLOR = new Color(0.13f, 0.14f, 0.17f);
    private static readonly Color ACCENT_COLOR = new Color(0.85f, 0.55f, 0.15f);
    private static readonly Color BUTTON_COLOR = new Color(0.22f, 0.24f, 0.29f);
    private static readonly Color MUTED_TEXT_COLOR = new Color(0.7f, 0.72f, 0.76f);
    private static readonly Color AMBIENT_COLOR = new Color(0.55f, 0.56f, 0.6f);

    private static readonly Color HOST_COLOR = new Color(0.2f, 0.55f, 0.35f);
    private static readonly Color JOIN_COLOR = new Color(0.2f, 0.4f, 0.65f);

    private static readonly Vector2 CHARACTER_CARD_POSITION = new Vector2(-420f, 120f);
    private static readonly Vector2 WEAPON_CARD_POSITION = new Vector2(420f, 120f);
    private static readonly Vector2 CARD_SIZE = new Vector2(640f, 600f);
    private static readonly Vector2 PREVIEW_SIZE = new Vector2(440f, 440f);
    private static readonly Vector2 ARROW_SIZE = new Vector2(70f, 70f);
    private static readonly Vector2 ACTION_BUTTON_SIZE = new Vector2(300f, 60f);
    private static readonly Vector2 NETWORK_BUTTON_SIZE = new Vector2(220f, 60f);
    private static readonly Vector2 ADDRESS_INPUT_SIZE = new Vector2(340f, 60f);
    private static readonly Vector2 PORT_INPUT_SIZE = new Vector2(170f, 60f);

    private const float NAME_ROW_Y = -215f;
    private const float SCENARIO_BUTTON_ROW_Y = -290f;
    private const float NETWORK_ROW_Y = -380f;
    private const float CONNECTION_STATUS_Y = -445f;
    private const float TOAST_Y = -500f;

    private struct CardWidgets
    {
        public TMP_Text NameText;
        public Button PrevButton;
        public Button NextButton;
    }

    internal static void Build(string scenePath, string[] characterPaths, string[] weaponPaths,
        string characterTexturePath, string weaponTexturePath, string storeProviderPath)
    {
        Scene scene = ProjectSceneBuilder.CreateEmptyScene(scenePath);
        // Nap asset SAU khi tao scene (KI-19)
        CharacterDefinition[] characters = ProjectSceneBuilder.LoadAssetsAtPaths<CharacterDefinition>(characterPaths);
        WeaponDefinition[] weapons = ProjectSceneBuilder.LoadAssetsAtPaths<WeaponDefinition>(weaponPaths);
        RenderTexture characterTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(characterTexturePath);
        RenderTexture weaponTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(weaponTexturePath);
        AbstractScenarioStoreProvider storeProvider = AssetDatabase.LoadAssetAtPath<AbstractScenarioStoreProvider>(storeProviderPath);
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
            NAME_CHARACTER_LIMIT, UiFactory.CENTER, new Vector2(0f, NAME_ROW_Y), new Vector2(900f, 64f));
        Button saveButton = UiFactory.CreateButton(root, "SaveButton", "Save Scenario", 28f, ACCENT_COLOR,
            UiFactory.CENTER, new Vector2(-170f, SCENARIO_BUTTON_ROW_Y), ACTION_BUTTON_SIZE);
        Button openFolderButton = UiFactory.CreateButton(root, "OpenFolderButton", "Open Folder", 28f, BUTTON_COLOR,
            UiFactory.CENTER, new Vector2(170f, SCENARIO_BUTTON_ROW_Y), ACTION_BUTTON_SIZE);

        TMP_InputField addressInput = UiFactory.CreateInputField(root, "AddressInput", "Host IP (127.0.0.1)", 28f,
            ADDRESS_CHARACTER_LIMIT, UiFactory.CENTER, new Vector2(-405f, NETWORK_ROW_Y), ADDRESS_INPUT_SIZE);
        addressInput.contentType = TMP_InputField.ContentType.Standard;
        TMP_InputField portInput = UiFactory.CreateInputField(root, "PortInput", "Port", 28f,
            PORT_CHARACTER_LIMIT, UiFactory.CENTER, new Vector2(-135f, NETWORK_ROW_Y), PORT_INPUT_SIZE);
        portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        Button hostButton = UiFactory.CreateButton(root, "HostButton", "Host", 28f, HOST_COLOR,
            UiFactory.CENTER, new Vector2(100f, NETWORK_ROW_Y), NETWORK_BUTTON_SIZE);
        Button joinButton = UiFactory.CreateButton(root, "JoinButton", "Join", 28f, JOIN_COLOR,
            UiFactory.CENTER, new Vector2(340f, NETWORK_ROW_Y), NETWORK_BUTTON_SIZE);

        TMP_Text connectionStatusText = UiFactory.CreateText(root, "ConnectionStatusText", string.Empty, 26f, MUTED_TEXT_COLOR,
            UiFactory.CENTER, new Vector2(0f, CONNECTION_STATUS_Y), new Vector2(1700f, 44f), TextAlignmentOptions.Center);
        TMP_Text statusText = UiFactory.CreateText(root, "StatusText", string.Empty, 24f, Color.white,
            UiFactory.CENTER, new Vector2(0f, TOAST_Y), new Vector2(1700f, 56f), TextAlignmentOptions.Center);
        statusText.textWrappingMode = TextWrappingModes.Normal;

        ScenarioSetupPanel panel = canvas.gameObject.AddComponent<ScenarioSetupPanel>();
        SerializedObject serialized = new SerializedObject(panel);
        serialized.FindProperty("storeProvider").objectReferenceValue = storeProvider;
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
        serialized.FindProperty("addressInput").objectReferenceValue = addressInput;
        serialized.FindProperty("portInput").objectReferenceValue = portInput;
        serialized.FindProperty("hostButton").objectReferenceValue = hostButton;
        serialized.FindProperty("joinButton").objectReferenceValue = joinButton;
        serialized.FindProperty("connectionStatusText").objectReferenceValue = connectionStatusText;
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
