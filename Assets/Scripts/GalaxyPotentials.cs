using UnityEngine;
using System.Collections.Generic;

public static class PotentialModels
{
    // --- Individual Acceleration Models (Hernquist, Miyamoto-Nagai, NFW) ---
    // (These functions remain unchanged)
    public static Vector3 HernquistAcceleration(Vector3 pos, float m, float c)
    {
        float r = pos.magnitude;
        if (r == 0) return Vector3.zero;
        float massTerm = PhysicsConstants.G_KPC_MYR * m;
        float denominator = r * (r + c) * (r + c);
        return pos * (-massTerm / denominator);
    }

    public static Vector3 MiyamotoNagaiAcceleration(Vector3 pos, float m, float a, float b)
    {
        float R_sq = pos.x * pos.x + pos.y * pos.y;
        float sqrt_z_sq_b_sq = Mathf.Sqrt(pos.z * pos.z + b * b);
        float denominator = Mathf.Pow(R_sq + (a + sqrt_z_sq_b_sq) * (a + sqrt_z_sq_b_sq), 1.5f);
        if (denominator == 0) return Vector3.zero;
        float commonFactor = -PhysicsConstants.G_KPC_MYR * m / denominator;
        float accel_x = commonFactor * pos.x;
        float accel_y = commonFactor * pos.y;
        float accel_z_numerator = -PhysicsConstants.G_KPC_MYR * m * pos.z * (a + sqrt_z_sq_b_sq);
        float accel_z_denominator = denominator * sqrt_z_sq_b_sq;
        float accel_z = (accel_z_denominator == 0) ? 0f : accel_z_numerator / accel_z_denominator;
        return new Vector3(accel_x, accel_y, accel_z);
    }

    public static Vector3 NFWAcceleration(Vector3 pos, float m, float r_s)
    {
        float r = pos.magnitude;
        if (r == 0) return Vector3.zero;
        float s = r / r_s;
        float massProfileTerm = Mathf.Log(1 + s) - s / (1 + s);
        float accel_mag = (PhysicsConstants.G_KPC_MYR * m * massProfileTerm) / (r * r);
        return pos.normalized * -accel_mag;
    }
}

public class GalaxyPotentialModel
{
    private struct PotentialComponent
    {
        public System.Func<Vector3, float, float, Vector3> AccelFunc3Param;
        public System.Func<Vector3, float, float, float, Vector3> AccelFunc4Param;
        public float p1, p2, p3;
    }
    
    private struct LMCModelParams { public float mass; public float radius; }

    private readonly List<PotentialComponent> _mwComponents = new List<PotentialComponent>();
    private readonly PotentialComponent _lmcComponent;
    private readonly TrajectoryInterpolator _mwInterpolator;
    private readonly TrajectoryInterpolator _lmcInterpolator;

    public GalaxyPotentialModel(TrajectoryInterpolator mwInterpolator, TrajectoryInterpolator lmcInterpolator, int trajectoryID)
    {
        _mwInterpolator = mwInterpolator;
        _lmcInterpolator = lmcInterpolator;
        _mwComponents.Add(new PotentialComponent { AccelFunc3Param = PotentialModels.HernquistAcceleration, p1 = 1.8142e9f, p2 = 0.0688867f });
        _mwComponents.Add(new PotentialComponent { AccelFunc3Param = PotentialModels.HernquistAcceleration, p1 = 5e9f, p2 = 0.7f });
        _mwComponents.Add(new PotentialComponent { AccelFunc3Param = PotentialModels.NFWAcceleration, p1 = 5.5427e11f, p2 = 15.626f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = 9.01e10f, p2 = 4.27f, p3 = 0.242f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = -5.91e10f, p2 = 9.23f, p3 = 0.242f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = 1e10f, p2 = 1.43f, p3 = 0.242f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = 7.88e9f, p2 = 7.30f, p3 = 1.14f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = -4.97e9f, p2 = 15.25f, p3 = 1.14f });
        _mwComponents.Add(new PotentialComponent { AccelFunc4Param = PotentialModels.MiyamotoNagaiAcceleration, p1 = 0.82e9f, p2 = 2.02f, p3 = 1.14f });
        LMCModelParams lmcParams = GetLMCParamsForTrajectory(trajectoryID);
        _lmcComponent = new PotentialComponent { AccelFunc3Param = PotentialModels.HernquistAcceleration, p1 = lmcParams.mass, p2 = lmcParams.radius };
    }

    private LMCModelParams GetLMCParamsForTrajectory(int id)
    {
        switch (id)
        {
            case 1: case 5: return new LMCModelParams { mass = 8.0e10f, radius = 10.4f };
            case 2: case 6: return new LMCModelParams { mass = 10.0e10f, radius = 12.7f };
            case 3: case 7: return new LMCModelParams { mass = 18.0e10f, radius = 20.0f };
            case 4: case 8: return new LMCModelParams { mass = 25.0e10f, radius = 25.2f };
            default: return new LMCModelParams { mass = 18.0e10f, radius = 20.0f };
        }
    }

    public Vector3 GetTotalAcceleration(Vector3 pos, float time)
    {
        Vector3 totalAccel = Vector3.zero;
        Vector3 mwPos = _mwInterpolator.GetPosition(time);
        Vector3 lmcPos = _lmcInterpolator.GetPosition(time);

        // --- MW Acceleration ---
        Vector3 relativePosMw = pos - mwPos;
        foreach (var component in _mwComponents)
        {
            if (component.AccelFunc3Param != null)
                totalAccel += component.AccelFunc3Param(relativePosMw, component.p1, component.p2);
            else if (component.AccelFunc4Param != null)
                totalAccel += component.AccelFunc4Param(relativePosMw, component.p1, component.p2, component.p3);
        }

        // --- LMC Acceleration ---
        Vector3 relativePosLmc = pos - lmcPos;
        totalAccel += _lmcComponent.AccelFunc3Param(relativePosLmc, _lmcComponent.p1, _lmcComponent.p2);

        // --- NEW DEBUGGING LOG ---
        // We only log this on the very first frame to avoid flooding the console.
        if (Time.frameCount < 5) // Log for the first few frames
        {
            Debug.Log($"--- ACCELERATION DEBUG (Frame {Time.frameCount}) ---\n" +
                      $"HVS Sim Pos: {pos}\n" +
                      $"MW Sim Pos: {mwPos}\n" +
                      $"LMC Sim Pos: {lmcPos}\n" +
                      $"HVS relative to MW: {relativePosMw} (Magnitude: {relativePosMw.magnitude})\n" +
                      $"HVS relative to LMC: {relativePosLmc} (Magnitude: {relativePosLmc.magnitude})\n" +
                      $"--> TOTAL ACCELERATION (kpc/Myr^2): {totalAccel} (Magnitude: {totalAccel.magnitude})\n" +
                      $"------------------------------------");
        }

        return totalAccel;
    }
}
