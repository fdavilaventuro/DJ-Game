using UnityEngine;

public class Crossfader : MonoBehaviour
{
    [Header("FMOD DJ Tables")]
    [SerializeField] private DJTable leftTable;
    [SerializeField] private DJTable rightTable;

    [Header("Curva del crossfader")]
    [Tooltip("0 = lineal, 1 = ligera curva, 2 = más pronunciada, etc.")]
    [SerializeField] private float curvePower = 1f;

    /// <summary>
    /// v debe ir en [0..1]: 0 = sólo izquierda, 1 = sólo derecha.
    /// Conecta esto al slider del crossfader (OnValueChanged).
    /// </summary>
    public void WhenValueChanged(float v)
    {
        v = Mathf.Clamp01(v);

        float t = Mathf.Pow(v, Mathf.Max(curvePower, 0.0001f));
        float leftFactor = 1f - t;
        float rightFactor = t;

        if (leftTable != null)
            leftTable.SetCrossfadeFactor(leftFactor);

        if (rightTable != null)
            rightTable.SetCrossfadeFactor(rightFactor);
    }
}
