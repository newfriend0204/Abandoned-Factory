public static class InteractionModeService {
    public static bool IsInInteractionMode { get; private set; }

    public static void SetInteractionMode(bool active) {
        IsInInteractionMode = active;
    }
}