using UnityEngine;

[System.Serializable]
public class HVS_Data
{

    public string name;    
    public long sourceId;

    public Vector3 position;
    public Vector3 velocity;

    public float[,] covarianceMatrix = new float[6, 6];

    public HVS_Data(string hvsName, long id, Vector3 pos, Vector3 vel, float[,] covMatrix)
    {
        name = hvsName;
        sourceId = id;
        position = pos;
        velocity = vel;
        covarianceMatrix = covMatrix;
    }
}
