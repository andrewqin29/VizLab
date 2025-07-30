using System.Collections.Generic;
using UnityEngine;
using System.Globalization;


// This component is responsible for loading all HVS data from the CSV files
// in the Resources folder at the start of the simulation. It creates a complete,
// in-memory catalogue of HVS_Data objects that other scripts can then access.

public class HVS_Catalogue : MonoBehaviour
{
    [Header("HVS Data")]
    [Tooltip("The complete list of HVS data loaded from the CSV files.")]
    public List<HVS_Data> hvsCatalogue = new List<HVS_Data>();

    void Awake()
    {
        LoadCatalogue();
    }

    private void LoadCatalogue()
    {
        TextAsset dataFile = Resources.Load<TextAsset>("6d_cartesian_data");
        TextAsset covFile = Resources.Load<TextAsset>("6d_cartesian_covariance");

        if (dataFile == null || covFile == null)
        {
            Debug.LogError("Could not find '6d_cartesian_data.csv' or '6d_cartesian_covariance.csv' in the Resources folder.");
            return;
        }


        var kinematicsData = ParseKinematicsData(dataFile);
        var covarianceData = ParseCovarianceData(covFile);

        foreach (var starKinematics in kinematicsData)
        {
            long sourceId = starKinematics.Key;
            
            if (covarianceData.ContainsKey(sourceId))
            {
                HVS_Data newStar = new HVS_Data(
                    starKinematics.Value.name,
                    sourceId,
                    starKinematics.Value.position,
                    starKinematics.Value.velocity,
                    covarianceData[sourceId]
                );
                hvsCatalogue.Add(newStar);
            }
            else
            {
                Debug.LogWarning($"Could not find matching covariance data for star with source_id: {sourceId}");
            }
        }
        
        Debug.Log($"Successfully loaded and processed {hvsCatalogue.Count} hypervelocity stars into the catalogue.");
    }

    private Dictionary<long, (string name, Vector3 position, Vector3 velocity)> ParseKinematicsData(TextAsset csvFile)
    {
        var data = new Dictionary<long, (string name, Vector3 position, Vector3 velocity)>();
        string[] lines = csvFile.text.Split('\n');

        // Start at i=1 to skip the header row.
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Trim().Split(',');

            if (values.Length >= 14)
            {
                try
                {
                    string hvsName = "HVS " + values[0];
                    long sourceId = long.Parse(values[1]);
                    Vector3 position = new Vector3(
                        float.Parse(values[2], CultureInfo.InvariantCulture),
                        float.Parse(values[4], CultureInfo.InvariantCulture),
                        float.Parse(values[6], CultureInfo.InvariantCulture)
                    );
                    Vector3 velocity = new Vector3(
                        float.Parse(values[8], CultureInfo.InvariantCulture),
                        float.Parse(values[10], CultureInfo.InvariantCulture),
                        float.Parse(values[12], CultureInfo.InvariantCulture)
                    );
                    data[sourceId] = (hvsName, position, velocity);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Error parsing kinematics data on line {i + 1}: {e.Message}");
                }
            }
        }
        return data;
    }
    
    private Dictionary<long, float[,]> ParseCovarianceData(TextAsset csvFile)
    {
        var data = new Dictionary<long, float[,]>();
        string[] lines = csvFile.text.Split('\n');

        // Start at i=1 to skip the header row.
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Trim().Split(',');

            if (values.Length >= 23)
            {
                try
                {
                    long sourceId = long.Parse(values[1]);
                    float[,] covMatrix = new float[6, 6];

                    float[] flatCov = new float[21];
                    for (int j = 0; j < 21; j++)
                    {
                        flatCov[j] = float.Parse(values[j + 2], CultureInfo.InvariantCulture);
                    }

                    //diagonal
                    covMatrix[0, 0] = flatCov[0];  // xx
                    covMatrix[1, 1] = flatCov[6];  // yy
                    covMatrix[2, 2] = flatCov[11]; // zz
                    covMatrix[3, 3] = flatCov[15]; // uu
                    covMatrix[4, 4] = flatCov[18]; // vv
                    covMatrix[5, 5] = flatCov[20]; // ww

                    //off-diagonal
                    covMatrix[0, 1] = covMatrix[1, 0] = flatCov[1];  // xy
                    covMatrix[0, 2] = covMatrix[2, 0] = flatCov[2];  // xz
                    covMatrix[0, 3] = covMatrix[3, 0] = flatCov[3];  // xu
                    covMatrix[0, 4] = covMatrix[4, 0] = flatCov[4];  // xv
                    covMatrix[0, 5] = covMatrix[5, 0] = flatCov[5];  // xw
                    covMatrix[1, 2] = covMatrix[2, 1] = flatCov[7];  // yz
                    covMatrix[1, 3] = covMatrix[3, 1] = flatCov[8];  // yu
                    covMatrix[1, 4] = covMatrix[4, 1] = flatCov[9];  // yv
                    covMatrix[1, 5] = covMatrix[5, 1] = flatCov[10]; // yw
                    covMatrix[2, 3] = covMatrix[3, 2] = flatCov[12]; // zu
                    covMatrix[2, 4] = covMatrix[4, 2] = flatCov[13]; // zv
                    covMatrix[2, 5] = covMatrix[5, 2] = flatCov[14]; // zw
                    covMatrix[3, 4] = covMatrix[4, 3] = flatCov[16]; // uv
                    covMatrix[3, 5] = covMatrix[5, 3] = flatCov[17]; // uw
                    covMatrix[4, 5] = covMatrix[5, 4] = flatCov[19]; // vw

                    data[sourceId] = covMatrix;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Error parsing covariance data on line {i + 1}: {e.Message}");
                }
            }
        }
        return data;
    }
}
