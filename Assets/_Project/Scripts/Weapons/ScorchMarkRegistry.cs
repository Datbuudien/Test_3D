using Unity.Netcode;
using UnityEngine;

// In-scene NetworkObject trong Gameplay. Server ghi NetworkList, moi may dung decal tu list -> client join muon thay du vet cu (KI-27)
public class ScorchMarkRegistry : NetworkBehaviour
{
    public const int MAX_SCORCH_MARKS = 32;
    private const float FULL_TURN_DEGREES = 360f;

    [SerializeField] private ScorchDecal decalPrefab;

    private NetworkList<ScorchMark> marks;
    private ScorchDecal[] pool;

    // NetworkList phai tao truoc khi spawn (Awake), khong o field initializer
    void Awake()
    {
        marks = new NetworkList<ScorchMark>();
    }

    public override void OnNetworkSpawn()
    {
        if (decalPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ScorchMarkRegistry)} on '{name}' needs Decal Prefab assigned in the Inspector.", this);
#endif
            return;
        }

        CreatePool();
        marks.OnListChanged += OnMarksChanged;
        RefreshDecals();
    }

    public override void OnNetworkDespawn()
    {
        marks.OnListChanged -= OnMarksChanged;
    }

    // Chi server. Day thi bo vet cu nhat; goc xoay do server chon de moi may giong nhau
    public void AddMark(Vector3 point, Vector3 normal)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        while (marks.Count >= MAX_SCORCH_MARKS)
        {
            marks.RemoveAt(0);
        }

        marks.Add(new ScorchMark(point, normal, Random.Range(0f, FULL_TURN_DEGREES)));
    }

    private void CreatePool()
    {
        pool = new ScorchDecal[MAX_SCORCH_MARKS];
        for (int index = 0; index < MAX_SCORCH_MARKS; index++)
        {
            pool[index] = Instantiate(decalPrefab, transform);
            pool[index].Hide();
        }
    }

    private void OnMarksChanged(NetworkListEvent<ScorchMark> changeEvent)
    {
        RefreshDecals();
    }

    // Toi da 32 phan tu -> dung lai ca pool moi lan doi cho don gian, khong lech thu tu khi RemoveAt(0)
    private void RefreshDecals()
    {
        if (pool == null)
        {
            return;
        }

        int markCount = Mathf.Min(marks.Count, pool.Length);
        for (int index = 0; index < pool.Length; index++)
        {
            if (index < markCount)
            {
                pool[index].Show(marks[index]);
            }
            else
            {
                pool[index].Hide();
            }
        }
    }
}
