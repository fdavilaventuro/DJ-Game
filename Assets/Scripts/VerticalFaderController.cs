using UnityEngine;

// Attach this to your fader GameObject. It assumes a kinematic Rigidbody with
// X,Z translation frozen and all rotations frozen. It limits movement strictly on Y.
public class VerticalFaderController : MonoBehaviour
{
    [Header("Y Range (Local Space)")]
    [SerializeField] private float minY = -0.05f;
    [SerializeField] private float maxY = -0.0025f;

    [Header("Behavior")]
    [SerializeField] private bool setDefaultAtStart = true; // set to midpoint on Start

    // Normalized value in [0..1]: 0 at minY, 1 at maxY
    public float Value { get; private set; }

    private float _baseX;
    private float _baseZ;

    private void Awake()
    {
        // Remember baseline X/Z from current local position
        var lp = transform.localPosition;
        _baseX = lp.x;
        _baseZ = lp.z;
        EnsureOrderedRange();
    }

    private void Start()
    {
        if (setDefaultAtStart)
        {
            // Set to midpoint in Y, keep baseline X/Z
            var lp = transform.localPosition;
            lp.x = _baseX;
            lp.z = _baseZ;
            lp.y = 0.5f * (minY + maxY);
            transform.localPosition = lp;
        }

        UpdateValueFromCurrent();
    }

    private void Update()
    {
        // Clamp strictly on Y each frame and pin X/Z to baseline
        var lp = transform.localPosition;
        lp.x = _baseX;
        lp.z = _baseZ;
        lp.y = Mathf.Clamp(lp.y, minY, maxY);
        transform.localPosition = lp;

        UpdateValueFromCurrent();
    }

    private void UpdateValueFromCurrent()
    {
        Value = Mathf.InverseLerp(minY, maxY, transform.localPosition.y);
    }

    private void OnValidate()
    {
        EnsureOrderedRange();
        // In editor, keep within bounds and maintain baseline X/Z as much as possible
        if (Application.isPlaying) return;
        var lp = transform.localPosition;
        _baseX = lp.x;
        _baseZ = lp.z;
        lp.y = Mathf.Clamp(lp.y, minY, maxY);
        transform.localPosition = lp;
    }

    private void EnsureOrderedRange()
    {
        if (minY > maxY)
        {
            var t = minY;
            minY = maxY;
            maxY = t;
        }
    }
}
