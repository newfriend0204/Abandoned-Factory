using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveGameData {
    public int slotVersion = 1;
    public int currentChapter = 1;

    public PlayerGlobalData player = new PlayerGlobalData();
    public Chap1SaveData chap1 = new Chap1SaveData();
    public Chap2SaveData chap2 = new Chap2SaveData();
}

[Serializable]
public class PlayerGlobalData {
    public bool hasHeadlamp;
    public float savedSprintStamina = -1f;
    public bool savedIsExhausted = false;
}

[Serializable]
public class Chap1SaveData {
    public bool hasCheckpoint = false;
    public Chap1CheckpointData last;
}

[Serializable]
public class Chap1CheckpointData {
    public string sceneName;

    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public float playerPitch;
    public float playerSprintStamina;
    public bool playerIsExhausted;

    public int chapStateInt;
    public int[] auxPowerStates;
    public bool[] pipeSolved;

    public List<string> consumedCheckpointZoneIds;

    public bool hasHeadlamp;
}

[Serializable]
public class Chap2SaveData {
    public bool hasCheckpoint = false;
    public Chap2CheckpointData last;

    public int chap2StateInt = 0;

    public int yCurrentStep = 1;

    public List<Chap2StepCheckpointEntry> stepCheckpoints = new List<Chap2StepCheckpointEntry>();
}

[Serializable]
public class Chap2StepCheckpointEntry {
    public int step;
    public Chap2CheckpointData data;
}

[Serializable]
public class Chap2CheckpointData {
    public string sceneName;

    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public float playerPitch;
    public float playerSprintStamina;
    public bool playerIsExhausted;

    public bool hasHeadlamp;

    public List<string> consumedCheckpointZoneIds;
}