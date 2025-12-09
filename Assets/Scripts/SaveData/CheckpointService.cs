using UnityEngine;

public interface ICheckpointService {
    bool HasCheckpoint { get; }
    void LoadLastCheckpoint();
}

public static class CheckpointService {
    public static ICheckpointService Current { get; private set; }

    public static void Register(ICheckpointService service) {
        Current = service;
    }
}