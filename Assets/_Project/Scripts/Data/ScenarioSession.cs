// Truyen kich ban tu ScenarioSetup sang Gameplay qua lan doi scene
public static class ScenarioSession
{
    public static ScenarioData Current { get; private set; }

    public static bool HasScenario => Current != null;

    public static void Set(ScenarioData data)
    {
        Current = data;
    }

    public static void Clear()
    {
        Current = null;
    }
}
