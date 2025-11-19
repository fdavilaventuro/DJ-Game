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
                    //djTable.SetFXAmount(value);
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
        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;

        Vector3 forward = transform.localRotation * Vector3.forward;

        Vector3 proj = Vector3.ProjectOnPlane(forward, axis);
        if (proj.sqrMagnitude < 0.0001f)
            return 0f;

        proj.Normalize();

        Vector3 refDir = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
        if (refDir.sqrMagnitude < 0.0001f)
            refDir = proj;

        float angle = Vector3.SignedAngle(refDir, proj, axis);
        return angle;
    }
}