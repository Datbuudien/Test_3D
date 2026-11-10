using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScenarioSetupPanel : MonoBehaviour
{
    private const string DEFAULT_SCENARIO_NAME = "Untitled";
    private const string FILE_URL_PREFIX = "file:///";
    private const string MESSAGE_MISSING_CATALOG = "No character or weapon definitions assigned.";
    private const string MESSAGE_EMPTY_NAME = "Please enter a scenario name.";
    private const string MESSAGE_ALREADY_EXISTS = "Scenario '{0}' already exists - press Save again within {1:0} s to overwrite.";
    private const string MESSAGE_IO_ERROR = "Could not save scenario: {0}";
    private const string MESSAGE_SAVED = "Saved: {0}";
    private const string MESSAGE_FOLDER_ERROR = "Could not open folder: {0}";

    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private PreviewStage characterStage;
    [SerializeField] private PreviewStage weaponStage;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField scenarioNameInput;
    [SerializeField] private Button characterPrevButton;
    [SerializeField] private Button characterNextButton;
    [SerializeField] private Button weaponPrevButton;
    [SerializeField] private Button weaponNextButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button openFolderButton;
    [SerializeField] private Button startButton;
    [SerializeField] private float statusDuration = 4f;
    [SerializeField] private float overwriteConfirmWindow = 3f;
    [SerializeField] private Color successColor = new Color(0.45f, 0.9f, 0.5f);
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color errorColor = new Color(1f, 0.4f, 0.4f);

    private ScenarioRepository repository;
    private int characterIndex;
    private int weaponIndex;
    private string pendingOverwriteName;
    private float pendingOverwriteTime;
    private Coroutine statusRoutine;

    public CharacterDefinition SelectedCharacter => IsCatalogValid() ? characters[characterIndex] : null;
    public WeaponDefinition SelectedWeapon => IsCatalogValid() ? weapons[weaponIndex] : null;

    void Awake()
    {
        repository = new ScenarioRepository(ScenarioRepository.DefaultDirectory);
        AddListener(characterPrevButton, OnCharacterPrevClicked);
        AddListener(characterNextButton, OnCharacterNextClicked);
        AddListener(weaponPrevButton, OnWeaponPrevClicked);
        AddListener(weaponNextButton, OnWeaponNextClicked);
        AddListener(saveButton, Save);
        AddListener(openFolderButton, OpenFolder);
        AddListener(startButton, StartGameplay);
        if (scenarioNameInput != null)
        {
            scenarioNameInput.onSubmit.AddListener(OnScenarioNameSubmitted);
        }
    }

    void Start()
    {
        if (!IsCatalogValid())
        {
            ShowStatus(MESSAGE_MISSING_CATALOG, errorColor);
            SetInteractable(saveButton, false);
            SetInteractable(startButton, false);
            return;
        }

        RefreshCharacter();
        RefreshWeapon();
    }

    void OnDestroy()
    {
        RemoveListeners(characterPrevButton);
        RemoveListeners(characterNextButton);
        RemoveListeners(weaponPrevButton);
        RemoveListeners(weaponNextButton);
        RemoveListeners(saveButton);
        RemoveListeners(openFolderButton);
        RemoveListeners(startButton);
        if (scenarioNameInput != null)
        {
            scenarioNameInput.onSubmit.RemoveListener(OnScenarioNameSubmitted);
        }
    }

    public void Save()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        string rawName = scenarioNameInput != null ? scenarioNameInput.text : string.Empty;
        bool overwrite = IsOverwriteConfirmed(rawName);
        SaveResult result = repository.Save(CreateScenarioData(rawName), overwrite);
        pendingOverwriteName = null;

        switch (result.Status)
        {
            case SaveStatus.Success:
                ShowStatus(string.Format(MESSAGE_SAVED, result.FilePath), successColor);
                break;
            case SaveStatus.EmptyName:
                ShowStatus(MESSAGE_EMPTY_NAME, errorColor);
                break;
            case SaveStatus.AlreadyExists:
                pendingOverwriteName = rawName.Trim();
                pendingOverwriteTime = Time.unscaledTime;
                ShowStatus(string.Format(MESSAGE_ALREADY_EXISTS, rawName.Trim(), overwriteConfirmWindow), warningColor);
                break;
            case SaveStatus.IoError:
                ShowStatus(string.Format(MESSAGE_IO_ERROR, result.ErrorMessage), errorColor);
                break;
        }
    }

    public void OpenFolder()
    {
        if (!repository.TryEnsureRootDirectory())
        {
            ShowStatus(string.Format(MESSAGE_FOLDER_ERROR, repository.RootDirectory), errorColor);
            return;
        }

        Application.OpenURL(FILE_URL_PREFIX + repository.RootDirectory.Replace('\\', '/'));
    }

    public void StartGameplay()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        string rawName = scenarioNameInput != null ? scenarioNameInput.text.Trim() : string.Empty;
        string scenarioName = rawName.Length > 0 ? rawName : DEFAULT_SCENARIO_NAME;
        ScenarioSession.Set(CreateScenarioData(scenarioName));
        SceneManager.LoadScene(Constants.SCENE_GAMEPLAY);
    }

    private void OnCharacterPrevClicked()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        characterIndex = Wrap(characterIndex - 1, characters.Length);
        RefreshCharacter();
    }

    private void OnCharacterNextClicked()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        characterIndex = Wrap(characterIndex + 1, characters.Length);
        RefreshCharacter();
    }

    private void OnWeaponPrevClicked()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        weaponIndex = Wrap(weaponIndex - 1, weapons.Length);
        RefreshWeapon();
    }

    private void OnWeaponNextClicked()
    {
        if (!IsCatalogValid())
        {
            return;
        }

        weaponIndex = Wrap(weaponIndex + 1, weapons.Length);
        RefreshWeapon();
    }

    private void OnScenarioNameSubmitted(string submittedName)
    {
        Save();
    }

    private void RefreshCharacter()
    {
        CharacterDefinition character = characters[characterIndex];
        SetText(characterNameText, character.DisplayName);
        if (characterStage != null)
        {
            characterStage.Show(character.PreviewPrefab, character.PreviewDistance);
        }
    }

    private void RefreshWeapon()
    {
        WeaponDefinition weapon = weapons[weaponIndex];
        SetText(weaponNameText, weapon.DisplayName);
        if (weaponStage != null)
        {
            weaponStage.Show(weapon.PreviewPrefab, weapon.PreviewDistance);
        }
    }

    private ScenarioData CreateScenarioData(string scenarioName)
    {
        CharacterDefinition character = characters[characterIndex];
        WeaponDefinition weapon = weapons[weaponIndex];
        return ScenarioData.Create(scenarioName, character.Id, character.DisplayName, weapon.Id, weapon.DisplayName, DateTime.Now);
    }

    private bool IsOverwriteConfirmed(string rawName)
    {
        return pendingOverwriteName != null
            && pendingOverwriteName == rawName.Trim()
            && Time.unscaledTime - pendingOverwriteTime <= overwriteConfirmWindow;
    }

    private bool IsCatalogValid()
    {
        return characters != null && characters.Length > 0 && weapons != null && weapons.Length > 0
            && Array.IndexOf(characters, null) < 0 && Array.IndexOf(weapons, null) < 0;
    }

    private void ShowStatus(string message, Color color)
    {
        if (statusText == null)
        {
            return;
        }

        if (statusRoutine != null)
        {
            StopCoroutine(statusRoutine);
        }

        statusText.text = message;
        statusText.color = color;
        statusRoutine = StartCoroutine(ClearStatusAfterDelay());
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSecondsRealtime(statusDuration);
        statusText.text = string.Empty;
        statusRoutine = null;
    }

    // Modulo an toan voi so am de vong tron < >
    private static int Wrap(int index, int count)
    {
        return ((index % count) + count) % count;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value;
        }
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveListeners(Button button)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
    }
}
