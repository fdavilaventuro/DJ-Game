using UnityEngine;
using Oculus.Interaction;
using UnityEngine.Events;

public class Slider : MonoBehaviour
{
    [SerializeField] Transform zeroPoint;
    [SerializeField] Transform onePoint;
    [SerializeField] float min = 0f;
    [SerializeField] float max = 1f;
    [SerializeField] float initialValue = 0.5f;

    OneGrabTranslateTransformer transformer;
    public UnityEvent<float> WhenValueChanged;

    float value;
    float lastValue;

    [Header("Optional DJ Link")]
    public DJTable djTable;
    public bool isPitchSlider = false; // ✅ set in Inspector for pitch faders

    void Awake()
    {
        transformer = GetComponent<OneGrabTranslateTransformer>();
    }

    void Start()
    {
        // Calculate world-space direction between zero and one
        Vector3 zeroToOne = onePoint.position - zeroPoint.position;
        Quaternion rotation = Quaternion.LookRotation(zeroToOne.normalized, Vector3.up);

        // Create a parent aligned with the slider's axis
        GameObject parent = new GameObject("Slider Parent");
        parent.transform.position = zeroPoint.position;
        parent.transform.rotation = rotation;

        transform.SetParent(parent.transform, true);

        float zDistance = Vector3.Distance(zeroPoint.position, onePoint.position);

        // ✅ Keep your original absolute constraints (DO NOT CHANGE)
        var constraints = new OneGrabTranslateTransformer.OneGrabTranslateConstraints()
        {
            ConstraintsAreRelative = false,
            MinX = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxX = new FloatConstraint() { Constrain = true, Value = 0f },
            MinY = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxY = new FloatConstraint() { Constrain = true, Value = 0f },
            MinZ = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxZ = new FloatConstraint() { Constrain = true, Value = zDistance },
        };
        transformer.Constraints = constraints;

        // ✅ Set initial position in world space
        float t = Mathf.InverseLerp(min, max, initialValue);
        transform.position = Vector3.Lerp(zeroPoint.position, onePoint.position, t);

        // ✅ Convert to local space so Update() math works correctly
        transform.localPosition = transform.parent.InverseTransformPoint(transform.position);

        lastValue = -1f; // force first update
    }

    void Update()
    {
        float minZ = transformer.Constraints.MinZ.Value;
        float maxZ = transformer.Constraints.MaxZ.Value;
        float norm = transform.localPosition.z;

        // normalized slider value (0–1)
        float delta = Mathf.Clamp((norm - minZ) / (maxZ - minZ), 0.0f, 1.0f);
        value = Mathf.Lerp(min, max, delta);

        if (Mathf.Abs(value - lastValue) > 0.0001f)
        {
            // Trigger UnityEvent (optional)
            WhenValueChanged?.Invoke(value);

            // Direct binding to DJTable (optional)
            if (djTable != null)
            {
                if (isPitchSlider)
                {
                    // map to ±6%
                    float mappedPitch = 1f + (value - 0.5f) * 0.12f;
                    djTable.SetPitch(mappedPitch);
                }
                else
                {
                    djTable.SetBaseVolume(value); // ahora sólo ajusta el volumen propio del deck
                }
            }

            lastValue = value;
        }
    }
}
