using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

public class CrowdController : MonoBehaviour
{
    [Header("DJ Tables")]
    public DJTable cdjLeft;
    public DJTable cdjRight;

    [Header("Crowd")]
    public GameObject[] clubberPrefabs;
    public int crowdSize = 30;
    public Transform areaPointA;
    public Transform areaPointB;
    public Transform areaPointC;
    public Transform areaPointD;
    public float personalSpace = 1.0f;   // minimum distance between clubbers

    [Header("Animations")]
    public AnimationClip[] danceAnimations;
    public AnimationClip[] idleAnimations;

    private List<Animator> clubbers = new List<Animator>();
    private Dictionary<Animator, PlayableGraph> graphs = new Dictionary<Animator, PlayableGraph>();
    private List<Vector3> occupiedPositions = new List<Vector3>();
    private bool crowdIsDancing = false;

    void Start()
    {
        GenerateCrowd();
    }

    void OnDestroy()
    {
        foreach (var g in graphs.Values)
            g.Destroy();
        graphs.Clear();
    }

    void Update()
    {
        bool anyPlaying = cdjLeft.IsPlaying() || cdjRight.IsPlaying();

        if (anyPlaying && !crowdIsDancing)
            StartDancing();
        else if (!anyPlaying && crowdIsDancing)
            StopDancing();
    }

    // -------------------------------------------------------------------------
    // CROWD GENERATION
    // -------------------------------------------------------------------------

    void GenerateCrowd()
    {
        clubbers.Clear();
        occupiedPositions.Clear();

        for (int i = 0; i < crowdSize; i++)
        {
            Vector3 pos = GetRandomPositionInArea();
            pos = ApplyPersonalSpace(pos);
            SpawnClubber(pos);
        }

        StopDancing(); // start idle
    }

    Vector3 GetRandomPositionInArea()
    {
        // Bilinear interpolation inside quadrilateral
        float u = Random.value;
        float v = Random.value;

        Vector3 top = Vector3.Lerp(areaPointA.position, areaPointB.position, u);
        Vector3 bottom = Vector3.Lerp(areaPointD.position, areaPointC.position, u);
        Vector3 pos = Vector3.Lerp(top, bottom, v);

        return pos;
    }

    Vector3 ApplyPersonalSpace(Vector3 pos)
    {
        int attempts = 0;
        while (attempts < 20)
        {
            bool tooClose = false;
            foreach (var other in occupiedPositions)
            {
                if (Vector3.Distance(pos, other) < personalSpace)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) break;

            // jitter slightly to avoid overlap
            pos += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
            attempts++;
        }

        occupiedPositions.Add(pos);
        return pos;
    }

    void SpawnClubber(Vector3 position)
    {
        if (clubberPrefabs.Length == 0) return;

        int index = Random.Range(0, clubberPrefabs.Length);
        GameObject c = Instantiate(clubberPrefabs[index], position, Quaternion.identity, transform);

        // Base direction towards DJ/player
        Vector3 targetDir = (transform.position - position);
        targetDir.y = 0;
        Quaternion baseRot = Quaternion.identity;
        if (targetDir != Vector3.zero)
            baseRot = Quaternion.LookRotation(targetDir);

        // Add random slight rotation for natural variation
        float yawOffset = Random.Range(-30f, 30f);   // rotate ±30 degrees randomly
        Quaternion randomYaw = Quaternion.Euler(0, yawOffset, 0);

        // Combine
        c.transform.rotation = baseRot * randomYaw;

        // Optional: small random tilt for extra life
        float tiltX = Random.Range(-5f, 5f);
        float tiltZ = Random.Range(-5f, 5f);
        c.transform.rotation *= Quaternion.Euler(tiltX, 0, tiltZ);

        Animator anim = c.GetComponent<Animator>();
        if (anim != null)
            clubbers.Add(anim);
    }


    // -------------------------------------------------------------------------
    // PLAYABLE ANIMATION
    // -------------------------------------------------------------------------

    void PlayClip(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null) return;

        if (graphs.TryGetValue(animator, out var oldGraph))
        {
            oldGraph.Destroy();
            graphs.Remove(animator);
        }

        animator.StopPlayback();
        animator.StartPlayback();
        animator.StopPlayback();
        animator.Rebind();
        animator.Update(0f);

        var graph = PlayableGraph.Create(animator.name + "_Graph");
        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        var playable = AnimationClipPlayable.Create(graph, clip);

        playable.SetApplyFootIK(true);
        playable.SetTime(0);
        playable.SetSpeed(1f);

        output.SetSourcePlayable(playable);
        graph.Play();

        graphs.Add(animator, graph);
    }

    // -------------------------------------------------------------------------
    // ANIMATION CONTROL
    // -------------------------------------------------------------------------

    void StartDancing()
    {
        crowdIsDancing = true;

        foreach (Animator a in clubbers)
        {
            if (danceAnimations.Length == 0) continue;
            AnimationClip clip = danceAnimations[Random.Range(0, danceAnimations.Length)];
            PlayClip(a, clip);
        }
    }

    void StopDancing()
    {
        crowdIsDancing = false;

        foreach (Animator a in clubbers)
        {
            if (idleAnimations.Length == 0) continue;
            AnimationClip clip = idleAnimations[Random.Range(0, idleAnimations.Length)];
            PlayClip(a, clip);
        }
    }
}
