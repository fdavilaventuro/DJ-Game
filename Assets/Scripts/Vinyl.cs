using UnityEngine;

public class Vinyl : MonoBehaviour
{
    [Header("DJ Table Reference")]
    [SerializeField]
    public DJTable djTable;

    [Header("Scratch Detection")]
    public float grabRotationThreshold = 5f;  // degrees per second needed to detect "grab"
    public float releaseThreshold = 1f;       // degrees per second to detect release
    public float momentumMultiplier = 0.4f;
    public float releaseFriction = 1.4f;

    private bool isGrabbed = false;
    private float lastAngle = 0f;
    private float currentAngle = 0f;
    private float angularVelocity = 0f;

    void Start()
    {
        lastAngle = GetLocalYAngle();
    }

    void Update()
    {
        currentAngle = GetLocalYAngle();

        // Compute angular speed
        float delta = Mathf.DeltaAngle(lastAngle, currentAngle);
        angularVelocity = delta / Time.deltaTime;

        // -----------------------------------------
        //      DETECT GRAB BY MOVEMENT ALONE
        // -----------------------------------------

        if (!isGrabbed)
        {
            // If user rotates the disc fast → they grabbed it
            if (Mathf.Abs(angularVelocity) > grabRotationThreshold)
            {
                BeginGrab();
            }
            else
            {
                HandleFreeRotation();
            }
        }
        else
        {
            // If user stopped rotating sufficiently → release
            if (Mathf.Abs(angularVelocity) < releaseThreshold)
            {
                EndGrab();
            }
            else
            {
                HandleScratching(delta);
            }
        }

        lastAngle = currentAngle;
    }

    // -----------------------------------------------------------
    //                      SCRATCHING
    // -----------------------------------------------------------

    void BeginGrab()
    {
        isGrabbed = true;
        djTable.BeginScratch(currentAngle);
    }

    void HandleScratching(float delta)
    {
        // Send rotation to DJTable
        djTable.ScratchUpdate(currentAngle, Time.deltaTime);
    }

    void EndGrab()
    {
        isGrabbed = false;

        // Apply momentum
        angularVelocity *= momentumMultiplier;

        djTable.EndScratch();
    }

    // -----------------------------------------------------------
    //                     FREE ROTATION
    // -----------------------------------------------------------

    void HandleFreeRotation()
    {
        if (Mathf.Abs(angularVelocity) > 0.1f)
        {
            transform.Rotate(Vector3.up, angularVelocity * Time.deltaTime, Space.Self);
            angularVelocity = Mathf.Lerp(angularVelocity, 0f, Time.deltaTime * releaseFriction);
        }
    }

    // -----------------------------------------------------------
    //                     HELPERS
    // -----------------------------------------------------------

    private float GetLocalYAngle()
    {
        return transform.localEulerAngles.y;
    }
}
