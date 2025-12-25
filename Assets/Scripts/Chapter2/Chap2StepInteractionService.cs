public static class Chap2StepInteractionService {
    public static bool IsInStepMode { get; private set; }

    public static void SetStepMode(bool active) {
        IsInStepMode = active;
    }
}