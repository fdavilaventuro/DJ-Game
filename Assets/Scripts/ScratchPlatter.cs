using UnityEngine;
using UnityEngine.Events;

public class ScratchPlatter : MonoBehaviour
{
    [Header("Platter Settings")]
    [SerializeField] private Vector3 localAxis = Vector3.up; // Axis the platter rotates around

    [Header("Optional DJ Link")]
    public DJTable djTable;

    [Header("Events")]
    public UnityEvent<float> WhenScratched; // Delta in degrees

    private float lastAngle = 0f;
    private bool firstFrame = true;

    void Update()
    {
        if (djTable == null) return;

        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        Vector3 forward = transform.localRotation * Vector3.forward;
        Vector3 proj = Vector3.ProjectOnPlane(forward, axis).normalized;
        Vector3 refDir = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;

        float currentAngle = Vector3.SignedAngle(refDir, proj, axis);

        if (firstFrame)
        {
            lastAngle = currentAngle;
            firstFrame = false;
            return;
        }

        // Compute delta since last frame
        float deltaAngle = Mathf.DeltaAngle(lastAngle, currentAngle);
        lastAngle = currentAngle;

        float deltaTime = Time.deltaTime;

        // Send delta to DJTable's Pro Scratch system
        djTable.UpdateProScratch(deltaAngle, deltaTime);

        WhenScratched?.Invoke(deltaAngle);
    }
}
