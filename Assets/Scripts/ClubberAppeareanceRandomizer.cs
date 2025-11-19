using System.Collections.Generic;
using UnityEngine;

public class ClubberAppearanceRandomizer : MonoBehaviour
{
    public List<GameObject> bodyVariants;

    void Start()
    {
        Debug.Log($"[Randomizer] Starting on {gameObject.name}. Found {bodyVariants.Count} variants.");

        RandomizeBody();
    }

    public void RandomizeBody()
    {
        if (bodyVariants == null || bodyVariants.Count == 0)
        {
            Debug.LogError($"[Randomizer] No body variants assigned on {gameObject.name}!");
            return;
        }

        // Disable all
        foreach (var body in bodyVariants)
        {
            if (body != null)
            {
                body.SetActive(false);
                Debug.Log($"[Randomizer] Disabled: {body.name}");
            }
        }

        // Pick random
        int index = Random.Range(0, bodyVariants.Count);
        GameObject selected = bodyVariants[index];

        selected.SetActive(true);
        Debug.Log($"[Randomizer] ENABLED: {selected.name}");
    }
}
