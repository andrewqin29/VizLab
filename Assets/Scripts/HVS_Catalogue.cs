using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.IO; 

public class HVS_Catalogue : MonoBehaviour
{
    [Header("HVS Data")]
    public List<HVS_Data> hvsCatalogue = new List<HVS_Data>();

    void Awake()
    {
        LoadCatalogue();
    }

    private void LoadCatalogue()
    {
        string dataFilePath = Path.Combine("processed_hvs", "6d_cartesian_data");
        string covFilePath = Path.Combine("processed_hvs", "6d_cartesian_covariance");

        TextAsset dataFile = Resources.Load<TextAsset>(dataFilePath);
        TextAsset covFile = Resources.Load<TextAsset>(covFilePath);

        if (dataFile == null || covFile == null)
        {
            Debug.LogError($"Could not find HVS data files. Searched for '{dataFilePath}.csv' and '{covFilePath}.csv' in the Resources folder.");
            return;
        }

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
                    starKinematics.Value.hvsId,
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

    private Dictionary<long, (int hvsId, string name, Vector3 position, Vector3 velocity)> ParseKinematicsData(TextAsset csvFile)
    {
        var data = new Dictionary<long, (int hvsId, string name, Vector3 position, Vector3 velocity)>();
        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = lines[i].Trim().Split(',');

            if (values.Length >= 14)
            {
                try
                {
                    int hvsId = int.Parse(values[0]);
                    long sourceId = long.Parse(values[1]);
                    string hvsName = "HVS " + values[0];
                    
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
                    
                    data[sourceId] = (hvsId, hvsName, position, velocity);
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

        for (int i = 1; i < lines.Length; i++) // Skip header
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

                    covMatrix[0, 0] = flatCov[0]; covMatrix[1, 1] = flatCov[6]; covMatrix[2, 2] = flatCov[11];
                    covMatrix[3, 3] = flatCov[15]; covMatrix[4, 4] = flatCov[18]; covMatrix[5, 5] = flatCov[20];
                    covMatrix[0, 1] = covMatrix[1, 0] = flatCov[1]; covMatrix[0, 2] = covMatrix[2, 0] = flatCov[2];
                    covMatrix[0, 3] = covMatrix[3, 0] = flatCov[3]; covMatrix[0, 4] = covMatrix[4, 0] = flatCov[4];
                    covMatrix[0, 5] = covMatrix[5, 0] = flatCov[5]; covMatrix[1, 2] = covMatrix[2, 1] = flatCov[7];
                    covMatrix[1, 3] = covMatrix[3, 1] = flatCov[8]; covMatrix[1, 4] = covMatrix[4, 1] = flatCov[9];
                    covMatrix[1, 5] = covMatrix[5, 1] = flatCov[10]; covMatrix[2, 3] = covMatrix[3, 2] = flatCov[12];
                    covMatrix[2, 4] = covMatrix[4, 2] = flatCov[13]; covMatrix[2, 5] = covMatrix[5, 2] = flatCov[14];
                    covMatrix[3, 4] = covMatrix[4, 3] = flatCov[16]; covMatrix[3, 5] = covMatrix[5, 3] = flatCov[17];
                    covMatrix[4, 5] = covMatrix[5, 4] = flatCov[19];

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
