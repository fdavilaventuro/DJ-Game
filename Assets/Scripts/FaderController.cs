using UnityEngine;

public class FaderController : MonoBehaviour
{
    // Minimum and maximum allowed local positions (editable in Inspector)
    [SerializeField]
    private Vector3 minPosition = new Vector3(0.02858078f, -0.05f, 0.0327f);

    [SerializeField]
    private Vector3 maxPosition = new Vector3(0.02858078f, -0.0025f, 0.0327f);

    // References to components (assign in Inspector or auto-detect)
    [SerializeField]
    private OVRGrabbable grabbable;

    [SerializeField]
    private Rigidbody rb;

    // Current normalized value of the fader (0 at min, 1 at max)
    public float Value { get; private set; }

    // On start, set default position to midpoint between min and max
    void Start()
    {
        if (grabbable == null) grabbable = GetComponent<OVRGrabbable>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        transform.localPosition = (minPosition + maxPosition) / 2f;
        UpdateValue();
    }

    // Ensure we never move outside the allowed bounds, but only when not grabbed by XR
    void Update()
    {
        if (grabbable == null || grabbable.grabbedBy == null)
        {
            transform.localPosition = ClampLocalPosition(transform.localPosition);
        }
        UpdateValue();
    }

    // Clamp each component between the corresponding min and max components
    private Vector3 ClampLocalPosition(Vector3 pos)
    {
        return new Vector3(
            Mathf.Clamp(pos.x, minPosition.x, maxPosition.x),
            Mathf.Clamp(pos.y, minPosition.y, maxPosition.y),
            Mathf.Clamp(pos.z, minPosition.z, maxPosition.z)
        );
    }

    // Update the normalized value based on current position
    private void UpdateValue()
    {
        Value = Mathf.InverseLerp(minPosition.y, maxPosition.y, transform.localPosition.y);
    }
}
