using UnityEngine;

// Attach to a horizontal fader with kinematic Rigidbody.
// Clamps local X strictly between -0.019 and 0.019, and pins Y/Z to initial values.
public class HorizontalFaderController : MonoBehaviour
{
    [Header("X Range (Local Space)")]
    [SerializeField] private float minX = -0.019f;
    [SerializeField] private float maxX = 0.019f;

    [Header("Behavior")]
    [SerializeField] private bool setDefaultAtStart = true; // set to midpoint on Start

    // Normalized value in [0..1]: 0 at minX, 1 at maxX
    public float Value { get; private set; }

    private float _baseY;
    private float _baseZ;

    private void Awake()
    {
        var lp = transform.localPosition;
        _baseY = lp.y;
        _baseZ = lp.z;
        EnsureOrderedRange();
    }

    private void Start()
    {
        if (setDefaultAtStart)
        {
            var lp = transform.localPosition;
            lp.y = _baseY;
            lp.z = _baseZ;
            lp.x = 0.5f * (minX + maxX);
            transform.localPosition = lp;
        }

        UpdateValueFromCurrent();
    }

    private void Update()
    {
        var lp = transform.localPosition;
        // Pin Y/Z and clamp X each frame
        lp.y = _baseY;
        lp.z = _baseZ;
        lp.x = Mathf.Clamp(lp.x, minX, maxX);
        transform.localPosition = lp;

        UpdateValueFromCurrent();
    }

    private void UpdateValueFromCurrent()
    {
        Value = Mathf.InverseLerp(minX, maxX, transform.localPosition.x);
    }

    private void OnValidate()
    {
        EnsureOrderedRange();
        if (Application.isPlaying) return;
        var lp = transform.localPosition;
        _baseY = lp.y;
        _baseZ = lp.z;
        lp.x = Mathf.Clamp(lp.x, minX, maxX);
        transform.localPosition = lp;
    }

    private void EnsureOrderedRange()
    {
        if (minX > maxX)
        {
            var t = minX;
            minX = maxX;
            maxX = t;
        }
    }
}
