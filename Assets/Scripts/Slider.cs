using UnityEngine;
using Oculus.Interaction;
using UnityEngine.Events;
using System;

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

    void Awake()
    {
        transformer = GetComponent<OneGrabTranslateTransformer>();
    }

    void Start()
    {
        // calculate vector from zero to one
        // create parent object rotated based on zero to one vector, and set constraints to relative
        // then we can just set min and max on z
        Vector3 zeroToOne = onePoint.position - zeroPoint.position;
        Quaternion rotation = Quaternion.LookRotation(zeroToOne.normalized, Vector3.up);

        GameObject parent = new GameObject("Slider Parent");
        parent.transform.position = zeroPoint.position;
        parent.transform.rotation = rotation;

        transform.SetParent(parent.transform, true);

        float zDistance = Vector3.Distance(zeroPoint.position, onePoint.position);
        var constraints = new OneGrabTranslateTransformer.OneGrabTranslateConstraints()
        {
            ConstraintsAreRelative = true,
            MinX = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxX = new FloatConstraint() { Constrain = true, Value = 0f },
            MinY = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxY = new FloatConstraint() { Constrain = true, Value = 0f },
            MinZ = new FloatConstraint() { Constrain = true, Value = 0f },
            MaxZ = new FloatConstraint() { Constrain = true, Value = zDistance },
        };
        transformer.Constraints = constraints;
        float t = Mathf.InverseLerp(min, max, initialValue);
        transform.position = Vector3.Lerp(zeroPoint.position, onePoint.position, t);
    }

    void Update()
    {
        float minZ = transformer.Constraints.MinZ.Value;
        float maxZ = transformer.Constraints.MaxZ.Value;
        float norm = transform.localPosition.z;
        float delta = Mathf.Clamp((norm - minZ) / (maxZ - minZ), 0.0f, 1.0f);
        value = Mathf.Lerp(min, max, delta);
        Debug.Log(name + " delta: " + delta.ToString("F2") + " value: " + value.ToString("F2"));

        if (value != lastValue)
        {
            WhenValueChanged?.Invoke(value);
            lastValue = value;
        }
    }
}
