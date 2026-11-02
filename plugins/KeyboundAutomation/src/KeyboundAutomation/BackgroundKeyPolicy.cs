namespace KeyboundAutomation;

public static class BackgroundKeyPolicy
{
    public static bool Allowed(char key) => key is '\b' or '\r' or >= ' ' and <= '~';
}
