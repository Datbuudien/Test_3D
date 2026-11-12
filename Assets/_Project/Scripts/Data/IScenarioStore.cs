using System.Collections.Generic;
using System.Threading.Tasks;

// Noi luu kich ban (file local, server...). Bat dong bo de doi sang server khong phai sua UI
public interface IScenarioStore
{
    Task<SaveResult> SaveAsync(ScenarioData data, bool overwrite);

    Task<IReadOnlyList<ScenarioData>> LoadAllAsync();

    // Duong dan mo duoc bang trinh duyet/Explorer (thu muc local); store khong co thi tra false
    bool TryGetBrowseUrl(out string url);
}
