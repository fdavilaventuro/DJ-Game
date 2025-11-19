using System.Collections.Generic;
using UnityEngine;

public class CrowdController : MonoBehaviour
{
    [Header("DJ Tables")]
    public DJTable cdjLeft;
    public DJTable cdjRight;

    [Header("Crowd Generation")]
    public GameObject[] clubberPrefabs;  // assign all male/female prefabs here
    public int crowdSize = 20;
    public Vector2 spawnAreaSize = new Vector2(10f, 10f);

    [Header("Animations")]
    public AnimationClip[] danceAnimations;
    public AnimationClip[] idleAnimations;

    private List<Animator> clubbers = new List<Animator>();
    private bool crowdIsDancing = false;

    void Start()
    {
        GenerateCrowd();
    }

    void Update()
    {
        bool shouldDance = cdjLeft.IsPlaying() || cdjRight.IsPlaying();

        if (shouldDance && !crowdIsDancing)
            StartDancing();
        else if (!shouldDance && crowdIsDancing)
            StopDancing();
    }

    // -----------------------------
    // CROWD GENERATION
    // -----------------------------
    void GenerateCrowd()
    {
        clubbers.Clear();

        int rows = Mathf.CeilToInt(Mathf.Sqrt(crowdSize));
        int cols = Mathf.CeilToInt((float)crowdSize / rows);
        float spacingX = spawnAreaSize.x / Mathf.Max(1, cols - 1);
        float spacingZ = spawnAreaSize.y / Mathf.Max(1, rows - 1);

        int count = 0;
        for (int r = 0; r < rows && count < crowdSize; r++)
        {
            for (int c = 0; c < cols && count < crowdSize; c++)
            {
                Vector3 offset = new Vector3(
                    -spawnAreaSize.x / 2 + c * spacingX,
                    0,
                    -spawnAreaSize.y / 2 + r * spacingZ
                );

                // Pick a random prefab
                int prefabIndex = Random.Range(0, clubberPrefabs.Length);
                GameObject clubber = Instantiate(clubberPrefabs[prefabIndex], transform.position + offset, Quaternion.identity, transform);

                Animator anim = clubber.GetComponent<Animator>();
                if (anim != null)
                {
                    clubbers.Add(anim);
                    Debug.Log($"[CrowdController] Spawned clubber #{count} at {offset} with Animator: {anim.name}");
                }

                count++;
            }
        }

        StopDancing(); // start idle
    }

    // -----------------------------
    // ANIMATION CONTROL
    // -----------------------------
    void StartDancing()
    {
        crowdIsDancing = true;

        foreach (Animator a in clubbers)
        {
            if (a == null || danceAnimations.Length == 0) continue;

            AnimationClip clip = danceAnimations[Random.Range(0, danceAnimations.Length)];
            PlayClip(a, clip);
            Debug.Log($"[CrowdController] Clubber {a.name} starts dancing: {clip.name}");
        }
    }

    void StopDancing()
    {
        crowdIsDancing = false;

        foreach (Animator a in clubbers)
        {
            if (a == null || idleAnimations.Length == 0) continue;

            AnimationClip clip = idleAnimations[Random.Range(0, idleAnimations.Length)];
            PlayClip(a, clip);
            Debug.Log($"[CrowdController] Clubber {a.name} goes idle: {clip.name}");
        }
    }

    // -----------------------------
    // PLAY ANIMATION VIA OVERRIDE CONTROLLER
    // -----------------------------
    void PlayClip(Animator a, AnimationClip clip)
    {
        if (a == null || clip == null) return;

        AnimatorOverrideController overrideController = new AnimatorOverrideController(a.runtimeAnimatorController);
        overrideController["Default"] = clip; // replace placeholder Default state
        a.runtimeAnimatorController = overrideController;
    }
}
