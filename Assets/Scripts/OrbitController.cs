using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization; // Required for robust float parsing

public class OrbitController : MonoBehaviour
{
    // --- Public Fields (Visible in Unity Inspector) ---

    [Header("Object References")]
    [Tooltip("The GameObject representing the Milky Way.")]
    public GameObject milkyWayObject;
    [Tooltip("The GameObject representing the Large Magellanic Cloud.")]
    public GameObject lmcObject;
    [Tooltip("The VR player object in the scene (e.g., GenericPlayer).")]
    public GameObject playerObject; 

    [Header("Startup Settings")]
    [Tooltip("The world-space position where the player will start.")]
    public Vector3 playerStartPosition = new Vector3(-54, 10, -21);
    [Tooltip("The world-space rotation (in Euler angles) for the player at start.")]
    public Vector3 playerStartRotation = new Vector3(-2, 52, 30);

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
    private Vector3 scaledCenterOffset; // The offset needed to center the simulation at the origin.
    private bool hasSetInitialPlayerPosition = false; // Flag to ensure we only set the start position once.
    
    // --- Constants ---
    private const float UPDATE_INTERVAL = 0.02f; // Fixed update time (50 updates per second)
    private const float SIMULATION_SIZE = 20.0f; // The fixed size of the simulation in meters.
    private const float SPHERE_SIZE_RATIO = 0.02f; // Sphere diameter as a percentage of total simulation size.
    private readonly Color MW_COLOR = Color.cyan;
    private readonly Color LMC_COLOR = Color.magenta;


    // This method is called once when the script instance is being loaded.
    void Awake()
    {
        // Initialize the lists to store the trajectory points.
        mw_trajectory = new List<Vector3>();
        lmc_trajectory = new List<Vector3>();

        // Load the data from the text files in the Resources folder.
        LoadTrajectoryData("interp_mw_orbit", mw_trajectory);
        LoadTrajectoryData("interp_lmc_orbit", lmc_trajectory);

        // Verify that the data was loaded and the trajectories have the same length.
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

    // This method is called before the first frame update.
    void Start()
    {
        if (!dataLoaded) return; // Do not proceed if data isn't ready.

        // If the player object isn't assigned in the Inspector, try to find it automatically.
        if (playerObject == null)
        {
            playerObject = GameObject.Find("GenericPlayer");
            if (playerObject == null) {
                Debug.LogError("Could not find 'GenericPlayer' in the scene. Please assign it in the Inspector.", this);
                return;
            }
        }

        // Calculate the scale and center offset for the simulation, which will be fixed at the world origin.
        PositionAndScaleSimulation();

        // --- Scale and color the spheres ---
        float sphereScale = SIMULATION_SIZE * SPHERE_SIZE_RATIO;
        milkyWayObject.transform.localScale = Vector3.one * sphereScale;
        lmcObject.transform.localScale = Vector3.one * sphereScale;
        milkyWayObject.GetComponent<Renderer>().material.color = MW_COLOR;
        lmcObject.GetComponent<Renderer>().material.color = LMC_COLOR;

        // Set the initial LOCAL position of the spheres, applying the centering offset.
        milkyWayObject.transform.localPosition = (mw_trajectory[0] * scaleFactor) - scaledCenterOffset;
        lmcObject.transform.localPosition = (lmc_trajectory[0] * scaleFactor) - scaledCenterOffset;

        // Configure the trail renderers if enabled.
        if (enableTrail)
        {
            SetupTrail(milkyWayObject, MW_COLOR);
            SetupTrail(lmcObject, LMC_COLOR);
        }
    }

    // This method is called once per frame.
    void Update()
    {
        if (!dataLoaded || current_index >= mw_trajectory.Count - 1) return;

        timer += Time.deltaTime;

        if (timer >= UPDATE_INTERVAL)
        {
            timer -= UPDATE_INTERVAL;
            current_index++;
            
            // Update the local position of the spheres, applying the centering offset.
            milkyWayObject.transform.localPosition = (mw_trajectory[current_index] * scaleFactor) - scaledCenterOffset;
            lmcObject.transform.localPosition = (lmc_trajectory[current_index] * scaleFactor) - scaledCenterOffset;
        }
    }

    // LateUpdate is called after all Update functions have been called.
    // This is the best place to override other scripts' control of the transform on the first frame.
    void LateUpdate()
    {
        // Check if we have already set the initial position. If so, do nothing.
        if (hasSetInitialPlayerPosition || playerObject == null)
        {
            return;
        }

        // --- Apply the hardcoded starting position and rotation to the player ---
        // This will override any position set by other scripts in their Start or Update methods.
        playerObject.transform.position = playerStartPosition;
        playerObject.transform.eulerAngles = playerStartRotation;

        // Set the flag to true so this code only ever runs once.
        hasSetInitialPlayerPosition = true;
        
        Debug.Log("Player start position has been set by OrbitController.");
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
                try {
                    float x = float.Parse(values[1], CultureInfo.InvariantCulture);
                    float y = float.Parse(values[2], CultureInfo.InvariantCulture);
                    float z = float.Parse(values[3], CultureInfo.InvariantCulture);
                    trajectoryList.Add(new Vector3(x, y, z));
                }
                catch (System.Exception e) {
                    Debug.LogWarning($"Could not parse line {i + 1} in {fileName}.txt. Content: '{lines[i]}'. Error: {e.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// MODIFIED: Calculates the scale and center offset required to frame the simulation at the world origin.
    /// </summary>
    void PositionAndScaleSimulation()
    {
        if (mw_trajectory.Count == 0) return;

        // Ensure the SimulationManager itself is at the world origin.
        this.transform.position = Vector3.zero;
        this.transform.rotation = Quaternion.identity;

        var bounds = new Bounds(mw_trajectory[0], Vector3.zero);
        foreach (var point in mw_trajectory) { bounds.Encapsulate(point); }
        foreach (var point in lmc_trajectory) { bounds.Encapsulate(point); }

        float maxBoundSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        if (maxBoundSize > 0) {
            scaleFactor = SIMULATION_SIZE / maxBoundSize;
        } else {
            scaleFactor = 1.0f;
        }

        // Calculate and store the offset required to center the simulation within this GameObject.
        scaledCenterOffset = bounds.center * scaleFactor;
        
        Debug.Log($"Simulation is now fixed at the world origin. Scale factor: {scaleFactor}.");
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
