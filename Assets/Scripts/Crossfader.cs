using UnityEngine;

public class Crossfader : MonoBehaviour
{
    [Header("FMOD DJ Tables")]
    [SerializeField] private DJTable leftTable;
    [SerializeField] private DJTable rightTable;

    /// <summary>
    /// v debe ir en [0..1]: 0 = sólo izquierda, 1 = sólo derecha.
    /// Conecta esto al slider del crossfader (OnValueChanged).
    /// </summary>
    public void whenValueChanged(float v)
    {
        v = Mathf.Clamp01(v);

        float leftFactor;
        float rightFactor;

        if (v < 0.5f)
        {
            // De 0 a 0.5: izquierda siempre a tope, derecha sube de 0 a 1
            float t = v / 0.5f;   // 0..1
            rightFactor = t;
            leftFactor = 1f;
        }
        else
        {
            // De 0.5 a 1: derecha siempre a tope, izquierda baja de 1 a 0
            float t = (1f - v) / 0.5f; // 0..1
            leftFactor = t;
            rightFactor = 1f;
        }

        if (leftTable != null)
            leftTable.SetCrossfadeFactor(leftFactor);

        if (rightTable != null)
            rightTable.SetCrossfadeFactor(rightFactor);
    }
}
