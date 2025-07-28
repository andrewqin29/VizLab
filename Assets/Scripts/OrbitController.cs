using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization; // Required for robust float parsing

public class OrbitController : MonoBehaviour
{
    // --- Public Fields (Visible in Unity Inspector) ---

    [Header("Object References")]
    // ** CORRECTED: Added milkyWayObject and lmcObject back **
    [Tooltip("The GameObject representing the Milky Way.")]
    public GameObject milkyWayObject;
    [Tooltip("The GameObject representing the Large Magellanic Cloud.")]
    public GameObject lmcObject;
    [Tooltip("The VR player object in the scene (e.g., GenericPlayer).")]
    public GameObject playerObject; 
    [Tooltip("The main camera used by getReal3D (often a child of the player).")]
    public Camera mainCamera;

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
        // If the camera isn't assigned, try to find it automatically.
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
             if (mainCamera == null) {
                Debug.LogError("Could not find a Main Camera in the scene. Please assign it in the Inspector.", this);
                return;
            }
        }

        // Position and scale the entire simulation relative to the player.
        PositionAndScaleSimulation();

        // --- Scale and color the spheres ---
        float sphereScale = SIMULATION_SIZE * SPHERE_SIZE_RATIO;
        milkyWayObject.transform.localScale = Vector3.one * sphereScale;
        lmcObject.transform.localScale = Vector3.one * sphereScale;
        milkyWayObject.GetComponent<Renderer>().material.color = MW_COLOR;
        lmcObject.GetComponent<Renderer>().material.color = LMC_COLOR;

        // Set the initial LOCAL position of the spheres within the scaled SimulationManager.
        milkyWayObject.transform.localPosition = mw_trajectory[0] * scaleFactor;
        lmcObject.transform.localPosition = lmc_trajectory[0] * scaleFactor;

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
    /// MODIFIED: Calculates the bounds of the simulation and positions it relative to the player's CAMERA.
    /// </summary>
    void PositionAndScaleSimulation()
    {
        if (mw_trajectory.Count == 0 || playerObject == null || mainCamera == null) return;

        var bounds = new Bounds(mw_trajectory[0], Vector3.zero);
        foreach (var point in mw_trajectory) { bounds.Encapsulate(point); }
        foreach (var point in lmc_trajectory) { bounds.Encapsulate(point); }

        float maxBoundSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        if (maxBoundSize > 0) {
            scaleFactor = SIMULATION_SIZE / maxBoundSize;
        } else {
            scaleFactor = 1.0f;
        }

        Vector3 scaledCenter = bounds.center * scaleFactor;

        // This is the key change.
        // We use the player's position as the anchor, but the CAMERA's forward
        // vector as the direction. This ensures the content is placed in front
        // of what the user is actually seeing.
        Vector3 playerPosition = playerObject.transform.position;
        Vector3 viewForward = mainCamera.transform.forward;
        
        // Position the simulation's center a certain distance in front of the player's view.
        float viewDistance = (SIMULATION_SIZE / 2.0f) + 2.0f; // 2 meters buffer
        
        this.transform.position = playerPosition + (viewForward * viewDistance) - scaledCenter;
        
        Debug.Log($"Player found at {playerPosition}. Simulation positioned in front of player's view.");
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
