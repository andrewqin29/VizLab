using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class HVS_Integrator : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Reference to the OrbitController in the scene.")]
    public OrbitController orbitController;
    [Tooltip("Reference to the HVS_Catalogue in the scene.")]
    public HVS_Catalogue hvsCatalogue;
    [Tooltip("The Particle System prefab used to visualize the HVS samples.")]
    public ParticleSystem hvsParticlePrefab;

    [Header("HVS Selection")]
    [Tooltip("The simple HVS ID from the first column of the data file (e.g., 1, 4, 19).")]
    public int hvsIdToIntegrate = 1;

    [Header("Integration Settings")]
    [Tooltip("The number of Monte Carlo samples to generate and integrate for the HVS.")]
    public int numberOfSamples = 100;
    [Tooltip("The time step for the integration in Megayears (Myr). Should be negative for backward integration.")]
    public float timeStepMyr = -0.1f;

    [Header("DEBUGGING")]
    [Tooltip("Makes the HVS particles huge to ensure they are visible.")]
    public bool useHugeParticles = true;
    [Tooltip("If true, all samples will start at the star's mean position, ignoring uncertainties.")]
    public bool disableCovarianceSampling = false;

    // --- Private Fields ---
    private ParticleSystem _particleSystemInstance;
    private ParticleSystem.Particle[] _particles;
    private List<PhaseSpacePoint> _hvsSamples;
    private GalaxyPotentialModel _potentialModel;
    private float _currentTimeMyr;
    private System.Random _random;

    private struct PhaseSpacePoint { public Vector3 position; public Vector3 velocity; }

    void Start()
    {
        if (!Initialize()) { this.enabled = false; return; }
        HVS_Data selectedStar = hvsCatalogue.hvsCatalogue.Find(hvs => hvs.hvsId == hvsIdToIntegrate);
        if (selectedStar == null) { Debug.LogError($"HVS with ID '{hvsIdToIntegrate}' not found.", this); this.enabled = false; return; }
        
        Debug.Log($"DEBUG: Found HVS '{selectedStar.name}' (ID: {selectedStar.hvsId}) at mean position {selectedStar.position}.");
        
        _hvsSamples = GenerateInitialSamples(selectedStar, numberOfSamples);
        SetupParticleSystem();
        Debug.Log($"Starting integration for HVS {selectedStar.name} with {numberOfSamples} samples.");
    }

    void FixedUpdate()
    {
        for (int i = 0; i < _hvsSamples.Count; i++)
        {
            PhaseSpacePoint currentSample = _hvsSamples[i];
            Vector3 accel = _potentialModel.GetTotalAcceleration(currentSample.position, _currentTimeMyr);
            Vector3 vel_half = currentSample.velocity + accel * (timeStepMyr / 2.0f);
            Vector3 pos_new = currentSample.position + vel_half * timeStepMyr;
            Vector3 accel_new = _potentialModel.GetTotalAcceleration(pos_new, _currentTimeMyr + timeStepMyr);
            Vector3 vel_new = vel_half + accel_new * (timeStepMyr / 2.0f);
            _hvsSamples[i] = new PhaseSpacePoint { position = pos_new, velocity = vel_new };
        }
        _currentTimeMyr += timeStepMyr;
        UpdateParticlePositions();

        // DEBUG: Log the position of the first particle every 100 frames
        if (Time.frameCount % 100 == 0 && _hvsSamples.Count > 0)
        {
            Debug.Log($"DEBUG: Frame {Time.frameCount}, HVS sample 0 position (kpc): {_hvsSamples[0].position}");
        }
    }

    private bool Initialize()
    {
        _random = new System.Random();
        if (orbitController == null) orbitController = FindObjectOfType<OrbitController>();
        if (hvsCatalogue == null) hvsCatalogue = FindObjectOfType<HVS_Catalogue>();
        if (orbitController == null || hvsCatalogue == null) { Debug.LogError("Missing OrbitController or HVS_Catalogue.", this); return false; }
        string folderPath = Path.Combine("galaxy trajectories", $"trajectory {orbitController.trajectoryID}");
        TextAsset[] trajectoryFiles = Resources.LoadAll<TextAsset>(folderPath);
        TextAsset mwFile = System.Array.Find(trajectoryFiles, f => f.name.EndsWith("_mw"));
        TextAsset lmcFile = System.Array.Find(trajectoryFiles, f => f.name.EndsWith("_lmc"));
        if (mwFile == null || lmcFile == null) { Debug.LogError($"Could not find trajectory files in '{folderPath}'.", this); return false; }
        var mwInterpolator = new TrajectoryInterpolator(mwFile);
        var lmcInterpolator = new TrajectoryInterpolator(lmcFile);
        _potentialModel = new GalaxyPotentialModel(mwInterpolator, lmcInterpolator, orbitController.trajectoryID);
        _currentTimeMyr = 0f;
        return true;
    }

    private List<PhaseSpacePoint> GenerateInitialSamples(HVS_Data star, int count)
    {
        var samples = new List<PhaseSpacePoint>();
        Vector3 meanPosition = star.position; // in kpc
        Vector3 meanVelocity = star.velocity * PhysicsConstants.KM_S_TO_KPC_MYR; // in kpc/Myr

        if (disableCovarianceSampling)
        {
            Debug.LogWarning("DEBUG: Covariance sampling is DISABLED. All particles will start at the mean position.");
            for (int i = 0; i < count; i++)
            {
                samples.Add(new PhaseSpacePoint { position = meanPosition, velocity = meanVelocity });
            }
        }
        else
        {
            float[,] L = MatrixMath.CholeskyDecomposition(star.covarianceMatrix);
            for (int i = 0; i < count; i++)
            {
                float[] z = new float[6];
                for (int j = 0; j < 6; j++) { z[j] = MatrixMath.NextGaussian(_random); }
                
                float[] deviation = new float[6];
                for (int row = 0; row < 6; row++)
                {
                    float sum = 0;
                    for (int col = 0; col < 6; col++) { sum += L[row, col] * z[col]; }
                    deviation[row] = sum;
                }

                Vector3 samplePosition = meanPosition + new Vector3(deviation[0], deviation[1], deviation[2]);
                
                Vector3 velocityDeviationKpcMyr = new Vector3(
                    deviation[3] * PhysicsConstants.KM_S_TO_KPC_MYR,
                    deviation[4] * PhysicsConstants.KM_S_TO_KPC_MYR,
                    deviation[5] * PhysicsConstants.KM_S_TO_KPC_MYR
                );
                Vector3 sampleVelocity = meanVelocity + velocityDeviationKpcMyr;
                
                samples.Add(new PhaseSpacePoint { position = samplePosition, velocity = sampleVelocity });
            }
        }

        // --- NEW DEBUGGING LOG ---
        // Log the initial state of every generated sample.
        Debug.Log("--- Generating Initial HVS Samples ---");
        for(int i = 0; i < samples.Count; i++)
        {
            // Convert velocity back to km/s for logging to match the input data format
            Vector3 velocityKmS = samples[i].velocity / PhysicsConstants.KM_S_TO_KPC_MYR;
            Debug.Log($"Sample {i}: Pos(kpc)={samples[i].position}, Vel(km/s)={velocityKmS}");
        }
        Debug.Log("------------------------------------");

        return samples;
    }

    private void SetupParticleSystem()
    {
        _particleSystemInstance = Instantiate(hvsParticlePrefab, this.transform);
        _particles = new ParticleSystem.Particle[numberOfSamples];
        _particleSystemInstance.GetParticles(_particles);
        
        float sizeMultiplier = useHugeParticles ? 100f : 1f;
        float masterScale = orbitController.milkyWayVisualRadius / 15.0f;
        
        for (int i = 0; i < numberOfSamples; i++)
        {
            Vector3 scaledPos = _hvsSamples[i].position * masterScale;
            _particles[i].position = scaledPos - orbitController.scaledCenterOffset;
            _particles[i].startColor = Color.white;
            _particles[i].startSize = orbitController.milkyWayVisualRadius * 0.005f * sizeMultiplier;
        }
        _particleSystemInstance.SetParticles(_particles, numberOfSamples);

        if (_particles.Length > 0)
        {
            Debug.Log($"DEBUG: SetupParticleSystem complete. First particle placed at world position: {_particles[0].position}. Size: {_particles[0].startSize}");
        }
    }

    private void UpdateParticlePositions()
    {
        float masterScale = orbitController.milkyWayVisualRadius / 15.0f;
        for (int i = 0; i < numberOfSamples; i++)
        {
            Vector3 scaledPos = _hvsSamples[i].position * masterScale;
            _particles[i].position = scaledPos - orbitController.scaledCenterOffset;
        }
        _particleSystemInstance.SetParticles(_particles, numberOfSamples);
    }
}
