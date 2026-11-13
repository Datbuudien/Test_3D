using GenericEventBus;
using UnityEngine;

// Bus su kien toan cuc (GenericEventBus, cung cach dung voi StackMarker) cho thong bao XUYEN SCENE / tu object sinh luc chay,
// noi hai ben khong gan Inspector cho nhau duoc (KI-34). Component trong cung prefab van dung C# event + [SerializeField].
// Bus chi chay trong 1 tien trinh -> KHONG thay RPC / NetworkVariable.
public static class EventManager
{
    private static GenericEventBus<IEvent> bus = new GenericEventBus<IEvent>();

    // Listener nam trong dictionary static theo instance bus -> Dispose bus cu de khong giu handler cua lan Play truoc
    // (Enter Play Mode co the tat domain reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBus()
    {
        bus.Dispose();
        bus = new GenericEventBus<IEvent>();
    }

    // Dang phat su kien khac thi xep hang, phat ngay sau khi su kien do xong
    public static bool Raise<TEvent>(in TEvent eventData) where TEvent : IEvent
    {
        return bus.Raise(eventData);
    }

    // Phat ngay ca khi dang phat su kien khac: chi cho su kien rang buoc thu tu (SessionEndingEvent phai xong truoc Shutdown)
    public static bool RaiseImmediately<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        return bus.RaiseImmediately(eventData);
    }

    public static void SubscribeTo<TEvent>(GenericEventBus<IEvent>.EventHandler<TEvent> handler, float priority = 0f)
        where TEvent : IEvent
    {
        bus.SubscribeTo(handler, priority);
    }

    public static void UnsubscribeFrom<TEvent>(GenericEventBus<IEvent>.EventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        bus.UnsubscribeFrom(handler);
    }
}
