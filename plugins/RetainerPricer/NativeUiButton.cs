using FFXIVClientStructs.FFXIV.Component.GUI;
namespace RetainerPricer;
internal static unsafe class NativeUiButton
{
    public static bool Click(AtkUnitBase* addon, AtkComponentButton* button)
    {
        if (addon == null || !addon->IsReady || !addon->IsVisible || button == null || !button->IsEnabled || button->OwnerNode == null || !button->OwnerNode->IsVisible()) return false;
        var ev = button->OwnerNode->AtkEventManager.Event;
        for (int n=0; ev!=null && n<64; n++,ev=ev->NextEvent)
        {
            if (ev->State.EventType != AtkEventType.ButtonClick || ev->Listener != addon) continue;
            AtkEvent copy = *ev; copy.NextEvent = null; copy.State.ReturnFlags = 0; copy.State.StateFlags = AtkEventStateFlags.None;
            AtkEventData eventData = default;
            ev->Listener->ReceiveEvent(AtkEventType.ButtonClick, (int)ev->Param, &copy, &eventData);
            return true;
        }
        return false;
    }
}
