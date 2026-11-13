// PlayerNetwork (prefab sinh luc chay) -> LocalPlayerBinder trong scene Gameplay noi camera / HUD / Esc / F5.
// Chi phat cho nhan vat cua MAY NAY (IsOwner)
public readonly struct LocalPlayerSpawnedEvent : IEvent
{
    public PlayerNetwork Player { get; }

    public LocalPlayerSpawnedEvent(PlayerNetwork player)
    {
        Player = player;
    }
}

public readonly struct LocalPlayerDespawnedEvent : IEvent
{
    public PlayerNetwork Player { get; }

    public LocalPlayerDespawnedEvent(PlayerNetwork player)
    {
        Player = player;
    }
}
