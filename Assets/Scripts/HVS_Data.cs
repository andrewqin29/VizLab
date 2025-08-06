using UnityEngine;

[System.Serializable]
public class HVS_Data
{
    public int hvsId;      // The simple ID for selection (e.g., 1, 4, 19)
    public string name;    
    public long sourceId;  // The internal Gaia DR3 source_id

    public Vector3 position;
    public Vector3 velocity;

    public float[,] covarianceMatrix = new float[6, 6];

    public HVS_Data(int simpleId, string hvsName, long gaiaId, Vector3 pos, Vector3 vel, float[,] covMatrix)
    {
        hvsId = simpleId;
        name = hvsName;
        sourceId = gaiaId;
        position = pos;
        velocity = vel;
        covarianceMatrix = covMatrix;
    }
}
