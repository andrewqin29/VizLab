using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;

public class OrbitController : MonoBehaviour
{
    // --- Public Fields ---

    [Header("Object References")]
    [Tooltip("The parent GameObject representing the Milky Way.")]
    public GameObject milkyWayObject;
    [Tooltip("The parent GameObject representing the Large Magellanic Cloud.")]
    public GameObject lmcObject;
    [Tooltip("The VR player object in the scene (e.g., GenericPlayer).")]
    public GameObject playerObject; 
    [Tooltip("The central, visible object to which the Milky Way's trail will be attached (e.g., the SMBH sphere).")]
    public GameObject milkyWayTrailAnchor;
    [Tooltip("The central, visible object to which the LMC's trail will be attached (e.g., the LMC Bar).")]
    public GameObject lmcTrailAnchor;

    [Header("Startup Settings")]
    [Tooltip("The world-space position where the player will start.")]
    public Vector3 playerStartPosition = new Vector3(-107, -46, -120);
    [Tooltip("The world-space rotation (in Euler angles) for the player at start.")]
    public Vector3 playerStartRotation = new Vector3(16, 40, 30);

    [Header("Simulation Settings")]
    [Tooltip("The desired visual radius of the Milky Way galaxy in the scene. All other scales are derived from this.")]
    public float milkyWayVisualRadius = 20.0f; 
    [Tooltip("Should the objects leave a trail behind them?")]
    public bool enableTrail = true;


    // --- Private Fields ---
    private List<Vector3> mw_trajectory;
    private List<Vector3> lmc_trajectory;
    private int current_index = 0;
    private bool dataLoaded = false; 
    private float masterScaleFactor = 1.0f;
    private float timer = 0f;
    private Vector3 scaledCenterOffset; 
    private bool hasSetInitialPlayerPosition = false;
    
    // --- Constants ---
    private const float UPDATE_INTERVAL = 0.02f; 
    private const float MILKY_WAY_PHYSICAL_RADIUS_KPC = 15.0f; // Approx. radius of the stellar disk
    // Updated Colors
    private readonly Color MW_COLOR = new Color(1.0f, 0.3f, 0.3f); 
    private readonly Color LMC_COLOR = new Color(0.5f, 1.0f, 0.5f);


    void Awake()
    {
        mw_trajectory = new List<Vector3>();
        lmc_trajectory = new List<Vector3>();
        LoadTrajectoryData("interp_mw_orbit", mw_trajectory);
        LoadTrajectoryData("interp_lmc_orbit", lmc_trajectory);

        if (mw_trajectory.Count > 0 && mw_trajectory.Count == lmc_trajectory.Count) {
            dataLoaded = true;
            Debug.Log($"Successfully loaded {mw_trajectory.Count} trajectory points for both galaxies.");
        } else {
            Debug.LogError("Failed to load trajectory data or trajectory lengths do not match. Simulation cannot start.");
            dataLoaded = false;
        }
    }

    void Start()
    {
        if (!dataLoaded) return; 

        if (playerObject == null) {
            playerObject = GameObject.Find("GenericPlayer");
            if (playerObject == null) {
                Debug.LogError("Could not find 'GenericPlayer' in the scene. Please assign it in the Inspector.", this);
                return;
            }
        }

        // Calculate the master scale factor based on the desired visual size of the Milky Way.
        masterScaleFactor = milkyWayVisualRadius / MILKY_WAY_PHYSICAL_RADIUS_KPC;

        // Scale and position the simulation based on the full trajectory bounds.
        PositionAndScaleSimulation();

        milkyWayObject.transform.localScale = Vector3.one; 
        lmcObject.transform.localScale = Vector3.one;
        
        milkyWayObject.transform.localPosition = (mw_trajectory[0] * masterScaleFactor) - scaledCenterOffset;
        lmcObject.transform.localPosition = (lmc_trajectory[0] * masterScaleFactor) - scaledCenterOffset;

        if (enableTrail) {
            // Attach trails to the specified anchor objects
            if (milkyWayTrailAnchor != null) SetupTrail(milkyWayTrailAnchor, MW_COLOR);
            if (lmcTrailAnchor != null) SetupTrail(lmcTrailAnchor, LMC_COLOR);
        }
    }

    void Update()
    {
        if (!dataLoaded || current_index >= mw_trajectory.Count - 1) return;

        timer += Time.deltaTime;

        if (timer >= UPDATE_INTERVAL) {
            timer -= UPDATE_INTERVAL;
            current_index++;
            
            milkyWayObject.transform.localPosition = (mw_trajectory[current_index] * masterScaleFactor) - scaledCenterOffset;
            lmcObject.transform.localPosition = (lmc_trajectory[current_index] * masterScaleFactor) - scaledCenterOffset;
        }
    }

    void LateUpdate()
    {
        if (hasSetInitialPlayerPosition || playerObject == null) return;

        playerObject.transform.position = playerStartPosition;
        playerObject.transform.eulerAngles = playerStartRotation;
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
    
    void PositionAndScaleSimulation()
    {
        if (mw_trajectory.Count == 0) return;

        this.transform.position = Vector3.zero;
        this.transform.rotation = Quaternion.identity;

        // Create bounds based on the original, uncompressed trajectory data.
        var bounds = new Bounds(mw_trajectory[0], Vector3.zero);
        foreach (var point in mw_trajectory) { bounds.Encapsulate(point); }
        foreach (var point in lmc_trajectory) { bounds.Encapsulate(point); }
        
        scaledCenterOffset = bounds.center * masterScaleFactor;
        
        Debug.Log($"Simulation is now fixed at the world origin. Master Scale Factor: {masterScaleFactor}.");
    }

    void SetupTrail(GameObject obj, Color trailColor)
    {
        TrailRenderer tr = obj.AddComponent<TrailRenderer>();
        tr.time = 20.0f;
        tr.startWidth = milkyWayVisualRadius * 0.01f;
        tr.endWidth = 0.0f;
        tr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        tr.startColor = trailColor;
        tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0);
    }
}
