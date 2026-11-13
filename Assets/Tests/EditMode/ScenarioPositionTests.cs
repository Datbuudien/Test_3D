using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class ScenarioPositionTests
{
    private const float POSITION_TOLERANCE = 0.0001f;
    private const string BASE_NAME = "Demo 1";
    // File kich ban Task 1 (truoc khi co savedBy / players)
    private const string TASK1_JSON = "{\n    \"scenarioName\": \"Old\",\n    \"characterId\": \"polish_soldier\",\n    \"characterName\": \"Polish Soldier\",\n    \"weaponId\": \"grenade\",\n    \"weaponName\": \"Grenade\",\n    \"createdAt\": \"2026-11-10T15:00:00\"\n}";
    private static readonly DateTime FIXED_TIME = new DateTime(2026, 11, 13, 10, 15, 3);

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
    public void SaveAndLoad_WithPlayers_RoundTripsPositionRotationClientIdAndRole()
    {
        ScenarioData saved = CreateBase().WithPlayers(ScenarioData.ROLE_HOST, new[]
        {
            PlayerSnapshot.Create(ScenarioData.ROLE_HOST, 0, "polish_soldier", "Polish Soldier", new Vector3(12.3f, 0.1f, -40.2f), 87.5f, FIXED_TIME),
            PlayerSnapshot.Create(ScenarioData.ROLE_CLIENT, 1, "green_swat", "Green SWAT", new Vector3(-5f, 2.25f, 7.75f), 270f, FIXED_TIME)
        });

        SaveResult result = repository.Save(saved, true);
        Assert.AreEqual(SaveStatus.Success, result.Status);
        Assert.IsTrue(repository.TryLoad(result.FilePath, out ScenarioData loaded));

        Assert.AreEqual(2, loaded.players.Length);
        PlayerSnapshot client = loaded.players[1];
        Assert.AreEqual(ScenarioData.ROLE_CLIENT, client.role);
        Assert.AreEqual(1L, client.clientId);
        Assert.AreEqual("green_swat", client.characterId);
        Assert.AreEqual(-5f, client.position.x, POSITION_TOLERANCE);
        Assert.AreEqual(2.25f, client.position.y, POSITION_TOLERANCE);
        Assert.AreEqual(7.75f, client.position.z, POSITION_TOLERANCE);
        Assert.AreEqual(270f, client.rotationY, POSITION_TOLERANCE);
        Assert.AreEqual("2026-11-13T10:15:03", client.capturedAt);
    }

    [Test]
    public void SnapshotClientId_LargeValue_SurvivesJsonRoundTrip()
    {
        PlayerSnapshot snapshot = PlayerSnapshot.Create(ScenarioData.ROLE_CLIENT, long.MaxValue, "a", "A", Vector3.zero, 0f, FIXED_TIME);
        PlayerSnapshot loaded = JsonUtility.FromJson<PlayerSnapshot>(JsonUtility.ToJson(snapshot));
        Assert.AreEqual(long.MaxValue, loaded.clientId);
    }

    [Test]
    public void TryLoad_Task1JsonWithoutPlayers_LoadsWithEmptyPlayers()
    {
        Directory.CreateDirectory(rootDirectory);
        string filePath = Path.Combine(rootDirectory, "Old.json");
        File.WriteAllText(filePath, TASK1_JSON);

        Assert.IsTrue(repository.TryLoad(filePath, out ScenarioData loaded));
        Assert.AreEqual("Old", loaded.scenarioName);
        Assert.IsNotNull(loaded.players);
        Assert.AreEqual(0, loaded.players.Length);
        Assert.AreEqual(string.Empty, loaded.savedBy);
    }

    [Test]
    public void WithPlayers_HostAndClient_FileNamesHaveRoleSuffix()
    {
        ScenarioData scenario = CreateBase();
        string hostPath = repository.GetFilePath(scenario.WithPlayers(ScenarioData.ROLE_HOST, null).scenarioName);
        string clientPath = repository.GetFilePath(scenario.WithPlayers(ScenarioData.ROLE_CLIENT, null).scenarioName);

        Assert.AreEqual("Demo 1_host.json", Path.GetFileName(hostPath));
        Assert.AreEqual("Demo 1_client.json", Path.GetFileName(clientPath));
    }

    [Test]
    public void WithPlayers_Role_SetsSavedByAndKeepsOriginal()
    {
        ScenarioData scenario = CreateBase();
        ScenarioData copy = scenario.WithPlayers(ScenarioData.ROLE_CLIENT, null);

        Assert.AreEqual(ScenarioData.ROLE_CLIENT, copy.savedBy);
        Assert.IsNotNull(copy.players);
        Assert.AreEqual(BASE_NAME, scenario.scenarioName);
        Assert.AreEqual(scenario.characterId, copy.characterId);
    }

    [Test]
    public void BuildSessionScenarioName_LongName_KeepsRoleSuffixAfterSanitize()
    {
        string sessionName = ScenarioData.BuildSessionScenarioName(new string('x', 200), ScenarioData.ROLE_CLIENT);
        string fileName = ScenarioRepository.SanitizeFileName(sessionName);

        Assert.IsTrue(fileName.EndsWith("_client", StringComparison.Ordinal));
        Assert.LessOrEqual(fileName.Length, ScenarioRepository.MAX_FILE_NAME_LENGTH);
    }

    [Test]
    public void GetRole_HostFlag_ReturnsMatchingRole()
    {
        Assert.AreEqual(ScenarioData.ROLE_HOST, ScenarioData.GetRole(true));
        Assert.AreEqual(ScenarioData.ROLE_CLIENT, ScenarioData.GetRole(false));
    }

    [Test]
    public void Save_WithPlayers_JsonContainsPlayersAndPositionKeys()
    {
        ScenarioData saved = CreateBase().WithPlayers(ScenarioData.ROLE_HOST, new[]
        {
            PlayerSnapshot.Create(ScenarioData.ROLE_HOST, 0, "polish_soldier", "Polish Soldier", new Vector3(1f, 2f, 3f), 45f, FIXED_TIME)
        });

        SaveResult result = repository.Save(saved, true);
        string json = File.ReadAllText(result.FilePath);

        StringAssert.Contains("\"savedBy\": \"host\"", json);
        StringAssert.Contains("\"players\"", json);
        StringAssert.Contains("\"position\"", json);
        StringAssert.Contains("\"rotationY\"", json);
    }

    private static ScenarioData CreateBase()
    {
        return ScenarioData.Create(BASE_NAME, "polish_soldier", "Polish Soldier", "grenade", "Grenade", FIXED_TIME);
    }
}
