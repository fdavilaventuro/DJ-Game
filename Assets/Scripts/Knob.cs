using UnityEngine;
using UnityEngine.Events;

public class Knob : MonoBehaviour
{
    public enum KnobType
    {
        HI,
        MID,
        LOW,
        FX
    }

    [Header("Tipo de knob")]
    [SerializeField] private KnobType knobType = KnobType.HI;

    [Header("Value range")]
    [SerializeField] private float min = 0f;
    [SerializeField] private float max = 1f;
    [SerializeField] private float initialValue = 0.5f;

    [Header("Rotation range (degrees, local)")]
    [Tooltip("Ángulo mínimo (en grados) del knob, en el eje local definido abajo.")]
    [SerializeField] private float minAngle = -135f;
    [Tooltip("Ángulo máximo (en grados) del knob, en el eje local definido abajo.")]
    [SerializeField] private float maxAngle = 135f;

    [Header("Rotation axis (local)")]
    [Tooltip("Eje local alrededor del cual rota el knob (ej. (0,1,0) = eje Y local).")]
    [SerializeField] private Vector3 localAxis = Vector3.up;

    [Header("Events")]
    public UnityEvent<float> WhenValueChanged;

    float value;
    float lastValue;

    [Header("Optional DJ Link")]
    public DJTable djTable;

    void Start()
    {
        // Inicializar rotación según initialValue
        float t = Mathf.InverseLerp(min, max, initialValue);
        float angle = Mathf.Lerp(minAngle, maxAngle, t);
        ApplyLocalRotation(angle);

        value = initialValue;
        lastValue = -999f;
        RaiseIfChanged();
    }

    void Update()
    {
        // Leer el ángulo actual en el eje local
        float currentAngle = GetCurrentLocalAngle();

        // Normalizar ángulo -> [0..1]
        float t = Mathf.InverseLerp(minAngle, maxAngle, currentAngle);

        // Mapear a [min..max]
        value = Mathf.Lerp(min, max, t);

        RaiseIfChanged();
    }

    void RaiseIfChanged()
    {
        if (Mathf.Abs(value - lastValue) <= 0.0001f)
            return;

        WhenValueChanged?.Invoke(value);

        if (djTable != null)
        {
            // Aquí despachamos según el tipo de knob.
            switch (knobType)
            {
                case KnobType.HI:
                    djTable.SetEQHi(value);
                    break;
                case KnobType.MID:
                    djTable.SetEQMid(value);
                    break;
                case KnobType.LOW:
                    djTable.SetEQLow(value);
                    break;
                case KnobType.FX:
                    djTable.SetFXAmount(value);
                    break;
            }
        }

        lastValue = value;
    }

    void ApplyLocalRotation(float angle)
    {
        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        transform.localRotation = Quaternion.AngleAxis(angle, axis);
    }

    float GetCurrentLocalAngle()
    {
        // Determine which local axis is being used
        Vector3 axis = localAxis.normalized;

        // Pick which Euler component corresponds to that axis
        float rawAngle;
        if (axis == Vector3.right || axis == Vector3.left)
            rawAngle = transform.localEulerAngles.x;
        else if (axis == Vector3.up || axis == Vector3.down)
            rawAngle = transform.localEulerAngles.y;
        else
            rawAngle = transform.localEulerAngles.z;

        // Unwrap 0..360 into -180..180
        if (rawAngle > 180f)
            rawAngle -= 360f;

        // Clamp to knob rotation limits
        return Mathf.Clamp(rawAngle, minAngle, maxAngle);
    }

}