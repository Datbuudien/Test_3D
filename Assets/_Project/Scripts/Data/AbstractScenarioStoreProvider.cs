using UnityEngine;

// Asset chon noi luu kich ban: doi asset tren Inspector la doi noi luu, khong sua code UI
public abstract class AbstractScenarioStoreProvider : ScriptableObject
{
    public abstract IScenarioStore CreateStore();
}
