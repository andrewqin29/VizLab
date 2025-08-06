using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.Linq;
using System.IO;

public class OrbitController : MonoBehaviour
{
    private class RawDataPoint
    {
        public float time; // in Gyr
        public Vector3 position; // in kpc
        public Vector3 velocity; // in km/s
    }

    // --- Public Fields ---

    [Header("Object References")]
    public GameObject milkyWayObject;
    public GameObject lmcObject;
    public GameObject playerObject;
    public GameObject milkyWayTrailAnchor;
    public GameObject lmcTrailAnchor;

    [Header("Startup Settings")]
    public Vector3 playerStartPosition = new Vector3(-107, -46, -120);
    public Vector3 playerStartRotation = new Vector3(16, 40, 30);

    [Header("Trajectory Selection")]
    [Tooltip("The ID for the MW/LMC trajectory pair to load (e.g., 1-8).")]
    [Range(1, 8)]
    public int trajectoryID = 1;

    [Header("Simulation Settings")]
    [Tooltip("The number of steps to create for the animation by interpolating the raw data.")]
    public int numberOfAnimationSteps = 500;
    [Tooltip("The desired visual radius of the Milky Way galaxy in the scene.")]
    public float milkyWayVisualRadius = 20.0f;
    [Tooltip("Should the objects leave a trail behind them?")]
    public bool enableTrail = true;

    // --- Private Fields ---
    private List<Vector3> mw_trajectory_interpolated;
    private List<Vector3> lmc_trajectory_interpolated;

    private int current_index = 0;
    private bool dataLoaded = false;
    private float masterScaleFactor = 1.0f;
    private float timer = 0f;
    public Vector3 scaledCenterOffset;
    private bool hasSetInitialPlayerPosition = false;

    // --- Constants ---
    private const float UPDATE_INTERVAL = 0.02f;
    private const float MILKY_WAY_PHYSICAL_RADIUS_KPC = 15.0f;
    private readonly Color MW_COLOR = new Color(1.0f, 0.3f, 0.3f);
    private readonly Color LMC_COLOR = new Color(0.5f, 1.0f, 0.5f);


    void Awake()
    {
        // 1. Construct the path to the correct trajectory subfolder in Resources.
        string folderPath = Path.Combine("galaxy trajectories", $"trajectory {trajectoryID}");

        // 2. Load all text assets from that specific folder.
        TextAsset[] trajectoryFiles = Resources.LoadAll<TextAsset>(folderPath);

        TextAsset mwFile = null;
        TextAsset lmcFile = null;

        // 3. Identify the MW and LMC files by their suffixes.
        foreach (var file in trajectoryFiles)
        {
            if (file.name.EndsWith("_mw"))
            {
                mwFile = file;
            }
            else if (file.name.EndsWith("_lmc"))
            {
                lmcFile = file;
            }
        }

        if (mwFile != null && lmcFile != null)
        {
            // 4. Load and parse the raw data from the identified files.
            List<RawDataPoint> rawMwData = LoadAndParseRawData(mwFile);
            List<RawDataPoint> rawLmcData = LoadAndParseRawData(lmcFile);

            if (rawMwData.Count > 1 && rawLmcData.Count > 1)
            {
                // 5. Interpolate the position data to create smooth animation paths.
                mw_trajectory_interpolated = InterpolatePositionPath(rawMwData, numberOfAnimationSteps);
                lmc_trajectory_interpolated = InterpolatePositionPath(rawLmcData, numberOfAnimationSteps);

                dataLoaded = true;
                Debug.Log($"Successfully loaded and interpolated '{mwFile.name}' and '{lmcFile.name}' into {numberOfAnimationSteps} steps.");
            }
            else
            {
                Debug.LogError($"Failed to load sufficient data points from trajectory files in '{folderPath}'.");
                dataLoaded = false;
            }
        }
        else
        {
            Debug.LogError($"Could not find '_mw' and/or '_lmc' text files in Resources folder: '{folderPath}'.");
            dataLoaded = false;
        }
    }

    void Start()
    {
        if (!dataLoaded) return;

        if (playerObject == null)
        {
            playerObject = GameObject.Find("GenericPlayer");
            if (playerObject == null)
            {
                Debug.LogError("Could not find 'GenericPlayer' in the scene. Please assign it in the Inspector.", this);
                return;
            }
        }

        masterScaleFactor = milkyWayVisualRadius / MILKY_WAY_PHYSICAL_RADIUS_KPC;
        PositionAndScaleSimulation();

        current_index = mw_trajectory_interpolated.Count - 1;

        milkyWayObject.transform.localPosition = (mw_trajectory_interpolated[current_index] * masterScaleFactor) - scaledCenterOffset;
        lmcObject.transform.localPosition = (lmc_trajectory_interpolated[current_index] * masterScaleFactor) - scaledCenterOffset;

        if (enableTrail)
        {
            if (milkyWayTrailAnchor != null) SetupTrail(milkyWayTrailAnchor, MW_COLOR);
            if (lmcTrailAnchor != null) SetupTrail(lmcTrailAnchor, LMC_COLOR);
        }
    }

    void Update()
    {
        if (!dataLoaded || current_index <= 0) return;

        timer += Time.deltaTime;
        if (timer >= UPDATE_INTERVAL)
        {
            timer -= UPDATE_INTERVAL;
            current_index--;

            milkyWayObject.transform.localPosition = (mw_trajectory_interpolated[current_index] * masterScaleFactor) - scaledCenterOffset;
            lmcObject.transform.localPosition = (lmc_trajectory_interpolated[current_index] * masterScaleFactor) - scaledCenterOffset;
        }
    }

    void LateUpdate()
    {
        if (hasSetInitialPlayerPosition || playerObject == null) return;
        playerObject.transform.position = playerStartPosition;
        playerObject.transform.eulerAngles = playerStartRotation;
        hasSetInitialPlayerPosition = true;
    }


    // Parses a TextAsset containing the 7-column trajectory data.
     List<RawDataPoint> LoadAndParseRawData(TextAsset file)
    {
        var rawDataList = new List<RawDataPoint>();
        string[] lines = file.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] values = lines[i].Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 7)
            {
                try
                {
                    rawDataList.Add(new RawDataPoint
                    {
                        time = float.Parse(values[0], CultureInfo.InvariantCulture),
                        position = new Vector3(
                            float.Parse(values[1], CultureInfo.InvariantCulture),
                            float.Parse(values[2], CultureInfo.InvariantCulture),
                            float.Parse(values[3], CultureInfo.InvariantCulture)
                        ),
                        velocity = new Vector3(
                            float.Parse(values[4], CultureInfo.InvariantCulture),
                            float.Parse(values[5], CultureInfo.InvariantCulture),
                            float.Parse(values[6], CultureInfo.InvariantCulture)
                        )
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not parse line {i + 1} in {file.name}.txt. Content: '{lines[i]}'. Error: {e.Message}");
                }
            }
        }
        return rawDataList.OrderBy(p => p.time).ToList();
    }

    // Interpolates a raw trajectory's position data into a fixed number of steps for animation.
    List<Vector3> InterpolatePositionPath(List<RawDataPoint> rawPath, int steps)
    {
        var interpolatedPath = new List<Vector3>();
        if (rawPath.Count < 2) return interpolatedPath;

        float minTime = rawPath[0].time;
        float maxTime = rawPath[rawPath.Count - 1].time;
        float timeStep = (maxTime - minTime) / (steps - 1);

        int rawIndex = 0;
        for (int i = 0; i < steps; i++)
        {
            float targetTime = minTime + (i * timeStep);

            while (rawIndex < rawPath.Count - 2 && rawPath[rawIndex + 1].time < targetTime)
            {
                rawIndex++;
            }

            RawDataPoint p1 = rawPath[rawIndex];
            RawDataPoint p2 = rawPath[rawIndex + 1];

            float t = (targetTime - p1.time) / (p2.time - p1.time);
            Vector3 interpolatedPosition = Vector3.Lerp(p1.position, p2.position, t);

            interpolatedPath.Add(interpolatedPosition);
        }
        return interpolatedPath;
    }

    void PositionAndScaleSimulation()
    {
        if (mw_trajectory_interpolated.Count == 0) return;

        this.transform.position = Vector3.zero;
        this.transform.rotation = Quaternion.identity;

        var bounds = new Bounds(mw_trajectory_interpolated[0], Vector3.zero);
        foreach (var point in mw_trajectory_interpolated) { bounds.Encapsulate(point); }
        foreach (var point in lmc_trajectory_interpolated) { bounds.Encapsulate(point); }

        scaledCenterOffset = bounds.center * masterScaleFactor;
    }

    void SetupTrail(GameObject obj, Color trailColor)
    {
        TrailRenderer tr = obj.AddComponent<TrailRenderer>();
        tr.time = 100.0f;
        tr.startWidth = milkyWayVisualRadius * 0.01f;
        tr.endWidth = 0.0f;
        tr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        tr.startColor = trailColor;
        tr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0);
    }
}