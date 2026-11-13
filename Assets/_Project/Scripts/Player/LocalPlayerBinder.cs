using Unity.Cinemachine;
using UnityEngine;

// Player sinh luc chay (NetworkObject) -> nhan LocalPlayerSpawnedEvent qua EventManager (KI-34)
// roi noi camera / HUD / quy dao / Esc / F5 trong scene vao nhan vat local qua Bind
public class LocalPlayerBinder : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform mainCamera;
    [SerializeField] private GameplayHud hud;
    [SerializeField] private GrenadeTrajectoryPreview trajectoryPreview;
    [SerializeField] private SceneNavigator sceneNavigator;
    [SerializeField] private ScenarioPositionRecorder positionRecorder;

    private PlayerNetwork boundPlayer;

    void OnEnable()
    {
        EventManager.SubscribeTo<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventManager.SubscribeTo<LocalPlayerDespawnedEvent>(OnLocalPlayerDespawned);
        // Nhan vat local spawn truoc khi binder bat -> da lo su kien, doc trang thai hien tai
        if (PlayerNetwork.Local != null)
        {
            Bind(PlayerNetwork.Local);
        }
    }

    void OnDisable()
    {
        EventManager.UnsubscribeFrom<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventManager.UnsubscribeFrom<LocalPlayerDespawnedEvent>(OnLocalPlayerDespawned);
        Unbind();
    }

    private void OnLocalPlayerSpawned(ref LocalPlayerSpawnedEvent eventData)
    {
        Bind(eventData.Player);
    }

    private void OnLocalPlayerDespawned(ref LocalPlayerDespawnedEvent eventData)
    {
        if (eventData.Player == boundPlayer)
        {
            Unbind();
        }
    }

    private void Bind(PlayerNetwork player)
    {
        if (player == null || player == boundPlayer)
        {
            return;
        }

        Unbind();
        boundPlayer = player;
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = player.CameraTarget;
            // Cat ngay toi nhan vat, khong luot camera tu vi tri cu
            cinemachineCamera.PreviousStateIsValid = false;
        }

        if (mainCamera != null)
        {
            player.Movement.SetCameraTransform(mainCamera);
        }

        if (hud != null)
        {
            hud.Bind(player.Holder, player.Thrower);
        }

        if (trajectoryPreview != null)
        {
            trajectoryPreview.Bind(player.Thrower, player.Holder);
        }

        if (sceneNavigator != null)
        {
            sceneNavigator.Bind(player.InputReader);
        }

        if (positionRecorder != null)
        {
            positionRecorder.Bind(player.InputReader);
        }
    }

    private void Unbind()
    {
        // ReferenceEquals: player da bi Destroy (== null) van phai go HUD/Esc
        if (ReferenceEquals(boundPlayer, null))
        {
            return;
        }

        boundPlayer = null;
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = null;
        }

        if (hud != null)
        {
            hud.Unbind();
        }

        if (trajectoryPreview != null)
        {
            trajectoryPreview.Unbind();
        }

        if (sceneNavigator != null)
        {
            sceneNavigator.Unbind();
        }

        if (positionRecorder != null)
        {
            positionRecorder.Unbind();
        }
    }
}
