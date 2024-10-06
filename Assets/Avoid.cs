using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AvoiderDLL;
using UnityEngine.AI;
using UnityEngine.Experimental.GlobalIllumination;
[RequireComponent(typeof(NavMeshAgent))]
[System.Serializable]
public class Avoider : MonoBehaviour
{
    NavMeshAgent agent;

    [SerializeField] private GameObject avoidee;
    [SerializeField] private float radius;

    [SerializeField] bool visualize = true;

    [SerializeField] float size_x;
    [SerializeField] float size_y;
    [SerializeField] float cellSize;

    void FixedUpdate()
    {
        UnityEngine.Color lineColor = UnityEngine.Color.white;
        RaycastHit hit;
        bool visible = IsVisible(transform.position, avoidee, out hit);
        if (true)
        {
            if (hit.distance < radius)
            {
                agent.SetDestination(RunAwaySearch(hit));
                lineColor = UnityEngine.Color.black;
                Debug.Log("Visible");
            }
        } else
        {
            Debug.Log("Not Visible");
        }
        if (visualize)
        {
            //UnityEngine.Debug.DrawLine(transform.position, avoidee.transform.position, lineColor, .02f);
        }
    }

    private bool IsVisible(Vector3 point, GameObject tracked)
    {
        RaycastHit hit;
        Ray toPlayer = new Ray(point, tracked.transform.position);
        if (Physics.Raycast(toPlayer, out hit))
        {
            Debug.Log(tracked.name);
            Debug.Log(hit.collider.gameObject.name);
            if (hit.collider == tracked.GetComponent<Collider>())
            {
                return true;
            }
        }
        return false;
    }
    private bool IsVisible(Vector3 point, GameObject tracked, out RaycastHit hit)
    {
        Ray toPlayer = new Ray(point, tracked.transform.position);
        if (Physics.Raycast(toPlayer, out hit))
        {
            Debug.Log(tracked.name);
            Debug.Log(hit.collider.gameObject.name);
            if (hit.collider == tracked.GetComponent<Collider>())
            {
                return true;
            }
        }
        return false;
    }

    private Vector3 RunAwaySearch(RaycastHit rayHit)
    {
        var sampler = new PoissonDiscSampler(transform.position, size_x, size_y, cellSize);
        List<Vector3> potentialSpots = new List<Vector3>();
        foreach (var point in sampler.Samples())
        {
            bool visible = IsVisible(point, avoidee);
            UnityEngine.Color lineColor = UnityEngine.Color.red;
            if (visible)
            {
                potentialSpots.Add(point);
                lineColor = UnityEngine.Color.green;
            }
            if (visualize)
            {
                UnityEngine.Debug.DrawLine(point, transform.position, lineColor, .02f);
            }
        }
        if (potentialSpots.Count > 0)
        {
            return potentialSpots[UnityEngine.Random.Range(0, (potentialSpots.Count - 1))];
        }

        return ((avoidee.transform.position - transform.position).normalized) * 10;
    }
}

public class PoissonDiscSampler
{
    private const int k = 30;  // Maximum number of attempts before marking a sample as inactive.

    private readonly Rect rect;
    private readonly float radius2;  // radius squared
    private readonly float cellSize;
    private Vector2[,] grid;
    private List<Vector2> activeSamples = new List<Vector2>();

    /// Create a sampler with the following parameters:
    ///
    /// width:  each sample's x coordinate will be between [0, width]
    /// height: each sample's y coordinate will be between [0, height]
    /// radius: each sample will be at least `radius` units away from any other sample, and at most 2 * `radius`.
    public PoissonDiscSampler(Vector3 location, float width, float height, float radius)
    {
        rect = new Rect(location.x, location.y, width, height);
        radius2 = radius * radius;
        cellSize = radius / Mathf.Sqrt(2);
        grid = new Vector2[Mathf.CeilToInt(width / cellSize),
                           Mathf.CeilToInt(height / cellSize)];
    }

    /// Return a lazy sequence of samples. You typically want to call this in a foreach loop, like so:
    ///   foreach (Vector2 sample in sampler.Samples()) { ... }
    public IEnumerable<Vector2> Samples()
    {
        // First sample is choosen randomly
        yield return AddSample(new Vector2(UnityEngine.Random.value * rect.width, UnityEngine.Random.value * rect.height));

        while (activeSamples.Count > 0)
        {

            // Pick a random active sample
            int i = (int)UnityEngine.Random.value * activeSamples.Count;
            Vector2 sample = activeSamples[i];

            // Try `k` random candidates between [radius, 2 * radius] from that sample.
            bool found = false;
            for (int j = 0; j < k; ++j)
            {

                float angle = 2 * Mathf.PI * UnityEngine.Random.value;
                float r = Mathf.Sqrt(UnityEngine.Random.value * 3 * radius2 + radius2); // See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
                Vector2 candidate = sample + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
                if (rect.Contains(candidate) && IsFarEnough(candidate))
                {
                    found = true;
                    yield return AddSample(candidate);
                    break;
                }
            }

            // If we couldn't find a valid candidate after k attempts, remove this sample from the active samples queue
            if (!found)
            {
                activeSamples[i] = activeSamples[activeSamples.Count - 1];
                activeSamples.RemoveAt(activeSamples.Count - 1);
            }
        }
    }

    private bool IsFarEnough(Vector2 sample)
    {
        GridPos pos = new GridPos(sample, cellSize);

        int xmin = Mathf.Max(pos.x - 2, 0);
        int ymin = Mathf.Max(pos.y - 2, 0);
        int xmax = Mathf.Min(pos.x + 2, grid.GetLength(0) - 1);
        int ymax = Mathf.Min(pos.y + 2, grid.GetLength(1) - 1);

        for (int y = ymin; y <= ymax; y++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                Vector2 s = grid[x, y];
                if (s != Vector2.zero)
                {
                    Vector2 d = s - sample;
                    if (d.x * d.x + d.y * d.y < radius2) return false;
                }
            }
        }

        return true;

        // Note: we use the zero vector to denote an unfilled cell in the grid. This means that if we were
        // to randomly pick (0, 0) as a sample, it would be ignored for the purposes of proximity-testing
        // and we might end up with another sample too close from (0, 0). This is a very minor issue.
    }

    /// Adds the sample to the active samples queue and the grid before returning it
    private Vector2 AddSample(Vector2 sample)
    {
        activeSamples.Add(sample);
        GridPos pos = new GridPos(sample, cellSize);
        grid[pos.x, pos.y] = sample;
        return sample;
    }

    /// Helper struct to calculate the x and y indices of a sample in the grid
    private struct GridPos
    {
        public int x;
        public int y;

        public GridPos(Vector2 sample, float cellSize)
        {
            x = (int)(sample.x / cellSize);
            y = (int)(sample.y / cellSize);
        }
    }
}
