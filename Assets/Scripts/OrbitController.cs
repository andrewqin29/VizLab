using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization; // Required for robust float parsing

public class OrbitController : MonoBehaviour
{
    // --- Public Fields (Visible in Unity Inspector) ---

    [Header("Object References")]
    public GameObject milkyWayObject;
    public GameObject lmcObject;

    [Header("Simulation Settings")]
    [Tooltip("Should the spheres leave a trail behind them?")]
    public bool enableTrail = true;


    // --- Private Fields ---
    private List<Vector3> mw_trajectory;
    private List<Vector3> lmc_trajectory;
    private int current_index = 0;
    private bool dataLoaded = false; // Safety flag
    private float scaleFactor = 1.0f;
    private float timer = 0f;
    
    // --- Constants ---
    private const float UPDATE_INTERVAL = 0.02f; // Fixed update time (50 updates per second)
    private const float SIMULATION_SIZE = 20.0f; // The fixed size of the simulation in meters.
    private const float SPHERE_SIZE_RATIO = 0.02f; // Sphere diameter as a percentage of total simulation size.
    private readonly Color MW_COLOR = Color.cyan;
    private readonly Color LMC_COLOR = Color.magenta;


    void Awake()
    {
        mw_trajectory = new List<Vector3>();
        lmc_trajectory = new List<Vector3>();

        LoadTrajectoryData("interp_mw_orbit", mw_trajectory);
        LoadTrajectoryData("interp_lmc_orbit", lmc_trajectory);

        if (mw_trajectory.Count > 0 && mw_trajectory.Count == lmc_trajectory.Count)
        {
            dataLoaded = true;
            Debug.Log($"Successfully loaded {mw_trajectory.Count} trajectory points for both galaxies.");
        }
        else
        {
            Debug.LogError("Failed to load trajectory data or trajectory lengths do not match. Simulation cannot start.");
            dataLoaded = false;
        }
    }

    void Start()
    {
        if (!dataLoaded) return;


        PositionAndScaleSimulation();

        float sphereScale = SIMULATION_SIZE * SPHERE_SIZE_RATIO;
        milkyWayObject.transform.localScale = Vector3.one * sphereScale;
        lmcObject.transform.localScale = Vector3.one * sphereScale;

        milkyWayObject.GetComponent<Renderer>().material.color = MW_COLOR;
        lmcObject.GetComponent<Renderer>().material.color = LMC_COLOR;

        milkyWayObject.transform.localPosition = mw_trajectory[0] * scaleFactor;
        lmcObject.transform.localPosition = lmc_trajectory[0] * scaleFactor;

        if (enableTrail)
        {
            SetupTrail(milkyWayObject, MW_COLOR);
            SetupTrail(lmcObject, LMC_COLOR);
        }
    }

    void Update()
    {
        if (!dataLoaded || current_index >= mw_trajectory.Count - 1)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= UPDATE_INTERVAL)
        {
            timer -= UPDATE_INTERVAL;

            current_index++;

            milkyWayObject.transform.localPosition = mw_trajectory[current_index] * scaleFactor;
            lmcObject.transform.localPosition = lmc_trajectory[current_index] * scaleFactor;
        }
    }

    void LoadTrajectoryData(string fileName, List<Vector3> trajectoryList)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(fileName);
        if (textAsset == null) {
            Debug.LogError($"Could not find file: {fileName}.txt in the Resources folder.");
            return;
        }
        string[] lines = textAsset.text.Split('\n');
        for (int i = 0; i < lines.Length; i++) {
            if (i == 0 || string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Trim().Split(' ');
            if (values.Length >= 4) {
                float x = float.Parse(values[1], CultureInfo.InvariantCulture);
                float y = float.Parse(values[2], CultureInfo.InvariantCulture);
                float z = float.Parse(values[3], CultureInfo.InvariantCulture);
                trajectoryList.Add(new Vector3(x, y, z));
            }
        }
    }
    
    void PositionAndScaleSimulation()
    {
        if (mw_trajectory.Count == 0) return;

        // create a bounding box that encapsulates all points from both trajectories.
        var bounds = new Bounds(mw_trajectory[0], Vector3.zero);
        foreach (var point in mw_trajectory) {
            bounds.Encapsulate(point);
        }
        foreach (var point in lmc_trajectory) {
            bounds.Encapsulate(point);
        }

        // find the largest dimension of the raw data.
        float maxBoundSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        // calculate the scale factor needed to shrink the simulation to our target size.
        if (maxBoundSize > 0) {
            scaleFactor = SIMULATION_SIZE / maxBoundSize;
        } else {
            scaleFactor = 1.0f;
        }

        // calculate the scaled center of the data.
        Vector3 scaledCenter = bounds.center * scaleFactor;

        // centered in the user's view at startup.
        this.transform.position = -scaledCenter;
        
        Debug.Log($"Simulation centered at {bounds.center}. Scaled by factor {scaleFactor} to fit a target size of {SIMULATION_SIZE}.");
    }

    void SetupTrail(GameObject obj, Color trailColor)
    {
        TrailRenderer tr = obj.AddComponent<TrailRenderer>();
        tr.time = 20.0f;
        tr.startWidth = 0.05f * SIMULATION_SIZE;
        tr.endWidth = 0.0f;
        tr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        tr.startColor = trailColor;
        tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0);
    }
}
