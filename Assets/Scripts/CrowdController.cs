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
    public int crowdSize = 40;
    public float crowdFrontRadiusMin = 1.0f;  // closer to DJ
    public float crowdFrontRadiusMax = 5.0f;  // closer front area
    public float clusterSpacing = 1.0f;       // personal space between clubbers

    [Header("Distribution Tweaks")]
    public float crowdArcAngle = 70f;        // ± degrees for arch
    public int clusterFrequency = 25;        // % chance to form clusters
    public int maxClusterSize = 4;
    public int stragglersMin = 3;
    public int stragglersMax = 7;

    [Header("Animations")]
    public AnimationClip[] danceAnimations;
    public AnimationClip[] idleAnimations;


    private List<Animator> clubbers = new List<Animator>();
    private Dictionary<Animator, PlayableGraph> graphs = new Dictionary<Animator, PlayableGraph>();
    private bool crowdIsDancing = false;
    private List<Vector3> occupiedPositions = new List<Vector3>();

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

        Vector3 djPos = transform.position;
        Vector3 forward = transform.forward;

        int spawned = 0;
        int frontCount = Mathf.RoundToInt(crowdSize * 0.75f);

        // 1. Front arch with clusters
        while (spawned < frontCount)
        {
            Vector3 pos = GetNaturalArchPosition(djPos, forward);
            pos = ApplyPersonalSpace(pos);

            SpawnClubber(pos, djPos);
            spawned++;

            // Cluster
            if (Random.Range(0, 100) < clusterFrequency)
            {
                int clusterSize = Random.Range(1, maxClusterSize + 1);
                for (int i = 0; i < clusterSize && spawned < frontCount; i++)
                {
                    Vector3 offset = new Vector3(
                        Random.Range(-clusterSpacing, clusterSpacing),
                        0,
                        Random.Range(-clusterSpacing, clusterSpacing)
                    );
                    Vector3 clusterPos = ApplyPersonalSpace(pos + offset);
                    SpawnClubber(clusterPos, djPos);
                    spawned++;
                }
            }
        }

        // 2. Stragglers
        int stragglers = Random.Range(stragglersMin, stragglersMax);
        for (int i = 0; i < stragglers; i++)
        {
            Vector3 pos = GetStragglerPosition(djPos, forward);
            pos = ApplyPersonalSpace(pos);
            SpawnClubber(pos, djPos);
        }

        StopDancing(); // start idle
    }

    // -------------------------------------------------------------------------
    // SPAWN HELPERS
    // -------------------------------------------------------------------------

    Vector3 GetNaturalArchPosition(Vector3 center, Vector3 forward)
    {
        float angle = Random.Range(-crowdArcAngle, crowdArcAngle);
        Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);

        float radius = Random.Range(crowdFrontRadiusMin, crowdFrontRadiusMax);
        Vector3 basePos = center + (rot * forward * radius);

        // Noise for organic spread
        float noise = Mathf.PerlinNoise(basePos.x * 0.25f, basePos.z * 0.25f);
        basePos += new Vector3((noise - 0.5f) * clusterSpacing, 0, (noise - 0.5f) * clusterSpacing);

        return basePos;
    }

    Vector3 GetStragglerPosition(Vector3 center, Vector3 forward)
    {
        Vector3 back = center - forward * Random.Range(6f, 10f);
        back += new Vector3(
            Random.Range(-5f, 5f),
            0,
            Random.Range(-2f, 2f)
        );
        return back;
    }

    Vector3 ApplyPersonalSpace(Vector3 pos)
    {
        // Prevent overlapping positions
        int attempts = 0;
        while (attempts < 10)
        {
            bool tooClose = false;
            foreach (var other in occupiedPositions)
            {
                if (Vector3.Distance(pos, other) < clusterSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose)
                break;

            // jitter if too close
            pos += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
            attempts++;
        }

        occupiedPositions.Add(pos);
        return pos;
    }

    void SpawnClubber(Vector3 position, Vector3 djPos)
    {
        if (clubberPrefabs.Length == 0) return;

        int index = Random.Range(0, clubberPrefabs.Length);
        GameObject c = Instantiate(clubberPrefabs[index], position, Quaternion.identity, transform);

        // Orientation
        float r = Random.value;
        if (r < 0.70f)
        {
            Vector3 dir = (djPos - position);
            dir.y = 0;
            c.transform.rotation = Quaternion.LookRotation(dir);
        }
        else if (r < 0.90f)
        {
            c.transform.rotation = Quaternion.Euler(0, Random.Range(-90f, 90f), 0);
        }
        else
        {
            c.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        }

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
    // ANIMATION STATE CONTROL
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
