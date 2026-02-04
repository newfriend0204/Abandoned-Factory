using UnityEngine;

public class SaveIconSwing : MonoBehaviour {
    [Header("Pendulum Swing Settings")]
    public float swingAngle = 10f;

    public float swingSpeed = 2f;

    private Quaternion _initialRotation;

    void Awake() {
        _initialRotation = transform.localRotation;
    }

    void Update() {
        float angle = Mathf.Sin(Time.time * swingSpeed) * swingAngle;

        transform.localRotation = _initialRotation * Quaternion.Euler(0f, 0f, angle);
    }
}