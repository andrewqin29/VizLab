using System.Collections.Generic;
using UnityEngine;
using System.Globalization;
using System.Linq;


public class TrajectoryInterpolator
{

    private struct DataPoint
    {
        public float time;       // (Myr)
        public Vector3 position; // (kpc)
        public Vector3 velocity; // kpc/Myr
    }

    private readonly List<DataPoint> _trajectoryData;
    private readonly float _minTime;
    private readonly float _maxTime;

    public TrajectoryInterpolator(TextAsset trajectoryFile)
    {
        _trajectoryData = LoadAndProcessData(trajectoryFile);
        if (_trajectoryData.Count > 0)
        {
            _minTime = _trajectoryData[0].time;
            _maxTime = _trajectoryData[_trajectoryData.Count - 1].time;
        }
    }

    public Vector3 GetPosition(float time)
    {
        return Interpolate(time, (dp => dp.position));
    }

    public Vector3 GetVelocity(float time)
    {
        return Interpolate(time, (dp => dp.velocity));
    }

    private Vector3 Interpolate(float time, System.Func<DataPoint, Vector3> valueSelector)
    {
        if (_trajectoryData.Count == 0) return Vector3.zero;
        if (time <= _minTime) return valueSelector(_trajectoryData[0]);
        if (time >= _maxTime) return valueSelector(_trajectoryData[_trajectoryData.Count - 1]);

        // binary search
        int i = _trajectoryData.FindIndex(dp => dp.time >= time) - 1;
        if (i < 0) i = 0; // should not happen with guards above, but safe.

        DataPoint p1 = _trajectoryData[i];
        DataPoint p2 = _trajectoryData[i + 1];

        // linear interpolation
        float t = (time - p1.time) / (p2.time - p1.time);
        return Vector3.Lerp(valueSelector(p1), valueSelector(p2), t);
    }

    private List<DataPoint> LoadAndProcessData(TextAsset file)
    {
        var dataList = new List<DataPoint>();
        if (file == null)
        {
            Debug.LogError("TrajectoryInterpolator received a null file.");
            return dataList;
        }

        string[] lines = file.text.Split('\n');
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] values = line.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 7)
            {
                try
                {
                    // parse raw values
                    float timeGyr = float.Parse(values[0], CultureInfo.InvariantCulture);
                    Vector3 posKpc = new Vector3(
                        float.Parse(values[1], CultureInfo.InvariantCulture),
                        float.Parse(values[2], CultureInfo.InvariantCulture),
                        float.Parse(values[3], CultureInfo.InvariantCulture)
                    );
                    Vector3 velKmS = new Vector3(
                        float.Parse(values[4], CultureInfo.InvariantCulture),
                        float.Parse(values[5], CultureInfo.InvariantCulture),
                        float.Parse(values[6], CultureInfo.InvariantCulture)
                    );

                    // add to list with units converted for simulation use
                    dataList.Add(new DataPoint
                    {
                        time = timeGyr * PhysicsConstants.GYR_TO_MYR,
                        position = posKpc,
                        velocity = velKmS * PhysicsConstants.KM_S_TO_KPC_MYR
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not parse line in {file.name}. Content: '{line}'. Error: {e.Message}");
                }
            }
        }
        // sorted for linear interpolation
        return dataList.OrderBy(p => p.time).ToList();
    }
}
