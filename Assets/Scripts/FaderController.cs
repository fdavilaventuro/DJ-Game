using UnityEngine;

[RequireComponent(typeof(OVRGrabbable))]
public class FaderController : MonoBehaviour
{
    // Minimum and maximum allowed local positions (editable in Inspector)
    [SerializeField]
    private Vector3 minPosition = new Vector3(0.02858078f, -0.05f, 0.0327f);

    [SerializeField]
    private Vector3 maxPosition = new Vector3(0.02858078f, -0.0025f, 0.0327f);

    private OVRGrabbable grabbable;

    // On start, set default position to midpoint between min and max
    void Start()
    {
        grabbable = GetComponent<OVRGrabbable>();
        transform.localPosition = (minPosition + maxPosition) / 2f;
    }

    // Ensure we never move outside the allowed bounds, but only when not grabbed by XR
    void Update()
    {
        if (grabbable.grabbedBy == null)
        {
            transform.localPosition = ClampLocalPosition(transform.localPosition);
        }
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
}
