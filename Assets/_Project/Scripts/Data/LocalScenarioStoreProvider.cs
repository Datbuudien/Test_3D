using UnityEngine;

[CreateAssetMenu(menuName = "Test3D/Scenario Store/Local File")]
public class LocalScenarioStoreProvider : AbstractScenarioStoreProvider
{
    public override IScenarioStore CreateStore()
    {
        return new ScenarioRepository(ScenarioRepository.DefaultDirectory);
    }
}
