// NetworkSession (scene Bootstrap, DontDestroyOnLoad) -> man Setup cap nhat dong trang thai ket noi
public readonly struct ConnectionStatusChangedEvent : IEvent
{
    public ConnectionStatus Status { get; }

    public ConnectionStatusChangedEvent(ConnectionStatus status)
    {
        Status = status;
    }
}

// 1 lan/phien khi nhan vat VAN CON trong scene (truoc Shutdown) -> ScenarioPositionRecorder tu luu toa do (KI-29).
// IsHost = vai tro cua MAY dang roi phien, dung cho hau to ten file
public readonly struct SessionEndingEvent : IEvent
{
    public bool IsHost { get; }

    public SessionEndingEvent(bool isHost)
    {
        IsHost = isHost;
    }
}
