using System;
using System.IO;
using NUnit.Framework;

public class ScenarioRepositoryTests
{
    private const string VALID_NAME = "Tap kich ban 1";
    private const string CORRUPT_JSON = "{ not valid json";

    private string rootDirectory;
    private ScenarioRepository repository;

    [SetUp]
    public void SetUp()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), "Test3D_" + Guid.NewGuid().ToString("N"));
        repository = new ScenarioRepository(rootDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, true);
        }
    }

    [Test]
    public void SanitizeFileName_InvalidCharacters_ReplacedWithUnderscore()
    {
        Assert.AreEqual("a_b_c_d_e_f_g_h_i_j", ScenarioRepository.SanitizeFileName("a/b\\c:d*e?f\"g<h>i|j"));
    }

    [Test]
    public void SanitizeFileName_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, ScenarioRepository.SanitizeFileName("   \t "));
        Assert.AreEqual(string.Empty, ScenarioRepository.SanitizeFileName(null));
    }

    [Test]
    public void SanitizeFileName_WindowsReservedName_Prefixed()
    {
        Assert.AreEqual("_con", ScenarioRepository.SanitizeFileName("con"));
        Assert.AreEqual("_COM1", ScenarioRepository.SanitizeFileName("COM1"));
        Assert.AreEqual("_NUL.backup", ScenarioRepository.SanitizeFileName("NUL.backup"));
        Assert.AreEqual("CONSOLE", ScenarioRepository.SanitizeFileName("CONSOLE"));
    }

    [Test]
    public void SanitizeFileName_VietnameseName_KeepsDiacritics()
    {
        Assert.AreEqual("Tấn công cứ điểm", ScenarioRepository.SanitizeFileName("  Tấn công cứ điểm  "));
    }

    [Test]
    public void SanitizeFileName_TrailingDotsAndSpaces_Trimmed()
    {
        Assert.AreEqual("Mission", ScenarioRepository.SanitizeFileName("Mission. . ."));
        Assert.AreEqual(string.Empty, ScenarioRepository.SanitizeFileName("..."));
    }

    [Test]
    public void SanitizeFileName_TooLong_TruncatedToMaxLength()
    {
        string sanitized = ScenarioRepository.SanitizeFileName(new string('x', 200));
        Assert.AreEqual(ScenarioRepository.MAX_FILE_NAME_LENGTH, sanitized.Length);
    }

    [Test]
    public void Save_EmptyName_ReturnsEmptyName()
    {
        SaveResult result = repository.Save(CreateData("   "), false);

        Assert.AreEqual(SaveStatus.EmptyName, result.Status);
        Assert.IsFalse(Directory.Exists(rootDirectory));
    }

    [Test]
    public void Save_NullData_ReturnsEmptyName()
    {
        Assert.AreEqual(SaveStatus.EmptyName, repository.Save(null, false).Status);
    }

    [Test]
    public void Save_NewScenario_CreatesDirectoryAndFile()
    {
        SaveResult result = repository.Save(CreateData(VALID_NAME), false);

        Assert.AreEqual(SaveStatus.Success, result.Status);
        Assert.IsTrue(File.Exists(result.FilePath));
        Assert.AreEqual(Path.Combine(rootDirectory, VALID_NAME + ScenarioRepository.FILE_EXTENSION), result.FilePath);
    }

    [Test]
    public void Save_InvalidCharactersInName_SavesWithSanitizedFileNameAndKeepsOriginalName()
    {
        const string rawName = "Trận: A/B?";
        SaveResult result = repository.Save(CreateData(rawName), false);

        Assert.AreEqual(SaveStatus.Success, result.Status);
        Assert.AreEqual("Trận_ A_B_" + ScenarioRepository.FILE_EXTENSION, Path.GetFileName(result.FilePath));
        Assert.IsTrue(repository.TryLoad(result.FilePath, out ScenarioData loaded));
        Assert.AreEqual(rawName, loaded.scenarioName);
    }

    [Test]
    public void Save_ExistingWithoutOverwrite_ReturnsAlreadyExists()
    {
        repository.Save(CreateData(VALID_NAME), false);

        SaveResult result = repository.Save(CreateData(VALID_NAME), false);

        Assert.AreEqual(SaveStatus.AlreadyExists, result.Status);
        Assert.IsTrue(repository.Exists(VALID_NAME));
    }

    [Test]
    public void Save_ExistingWithOverwrite_ReplacesFile()
    {
        repository.Save(CreateData(VALID_NAME), false);
        ScenarioData replacement = CreateData(VALID_NAME);
        replacement.weaponId = "grenade";

        SaveResult result = repository.Save(replacement, true);

        Assert.AreEqual(SaveStatus.Success, result.Status);
        Assert.IsTrue(repository.TryLoad(result.FilePath, out ScenarioData loaded));
        Assert.AreEqual("grenade", loaded.weaponId);
    }

    [Test]
    public void SaveThenLoad_RoundTrip_FieldsEqual()
    {
        ScenarioData original = CreateData(VALID_NAME);
        SaveResult result = repository.Save(original, false);

        Assert.IsTrue(repository.TryLoad(result.FilePath, out ScenarioData loaded));
        Assert.AreEqual(original.scenarioName, loaded.scenarioName);
        Assert.AreEqual(original.characterId, loaded.characterId);
        Assert.AreEqual(original.characterName, loaded.characterName);
        Assert.AreEqual(original.weaponId, loaded.weaponId);
        Assert.AreEqual(original.weaponName, loaded.weaponName);
        Assert.AreEqual(original.createdAt, loaded.createdAt);
    }

    [Test]
    public void LoadAll_CorruptFile_IsSkipped()
    {
        repository.Save(CreateData(VALID_NAME), false);
        File.WriteAllText(Path.Combine(rootDirectory, "broken" + ScenarioRepository.FILE_EXTENSION), CORRUPT_JSON);

        Assert.AreEqual(1, repository.LoadAll().Count);
    }

    [Test]
    public void LoadAll_MissingDirectory_ReturnsEmpty()
    {
        Assert.AreEqual(0, repository.LoadAll().Count);
    }

    [Test]
    public void LoadAll_MultipleScenarios_SortedByCreatedAtDescending()
    {
        ScenarioData older = CreateData("Older", new DateTime(2026, 1, 1, 8, 0, 0));
        ScenarioData newer = CreateData("Newer", new DateTime(2026, 6, 1, 8, 0, 0));
        repository.Save(older, false);
        repository.Save(newer, false);

        Assert.AreEqual("Newer", repository.LoadAll()[0].scenarioName);
    }

    [Test]
    public void Create_GivenDate_FormatsInvariantIso()
    {
        ScenarioData data = CreateData(VALID_NAME, new DateTime(2026, 11, 10, 16, 5, 9));
        Assert.AreEqual("2026-11-10T16:05:09", data.createdAt);
    }

    private static ScenarioData CreateData(string name)
    {
        return CreateData(name, new DateTime(2026, 11, 10, 12, 0, 0));
    }

    private static ScenarioData CreateData(string name, DateTime createdAt)
    {
        return ScenarioData.Create(name, "polish_soldier", "Polish Soldier", "ak47", "AK-47", createdAt);
    }
}
