using UnityEngine;

// Cholesky decomposition for a covariance matrix.

public static class MatrixMath
{
    public static float[,] CholeskyDecomposition(float[,] matrix)
    {
        int n = 6;
        float[,] L = new float[n, n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                float sum = 0;
                for (int k = 0; k < j; k++)
                {
                    sum += L[i, k] * L[j, k];
                }

                if (i == j)
                {
                    float d = matrix[i, i] - sum;
                    if (d <= 0)
                    {
                        // if the matrix is not positive-definite, decomposition fails.
                        Debug.LogWarning("Cholesky decomposition failed: matrix is not positive-definite. Using a small identity matrix as fallback.");
                        return GetFallbackMatrix(n);
                    }
                    L[i, i] = Mathf.Sqrt(d);
                }
                else
                {
                    if (L[j, j] == 0) return GetFallbackMatrix(n); // avoid division by zero
                    L[i, j] = (matrix[i, j] - sum) / L[j, j];
                }
            }
        }
        return L;
    }

    public static float NextGaussian(System.Random rand)
    {
        float u1 = 1.0f - (float)rand.NextDouble(); //uniform(0,1] random doubles
        float u2 = 1.0f - (float)rand.NextDouble();
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return randStdNormal;
    }
    
    private static float[,] GetFallbackMatrix(int n)
    {
        float[,] fallback = new float[n, n];
        for (int i = 0; i < n; i++)
        {
            fallback[i, i] = 1e-6f; // A very small value
        }
        return fallback;
    }
}
