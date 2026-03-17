using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

public static class PredictionFeatureHelper
{
    public static float[] ExtractFeatures(int[] draws)
    {
        var features = new float[40];
        foreach (var n in draws) features[n - 1] = 1.0f;

        var sum = draws.Sum();
        var ac = CalculateACValue(draws);
        var oddRatio = draws.Count(n => n % 2 != 0) / 7.0f;

        features[37] = (sum - 28) / (238.0f - 28.0f);
        features[38] = ac / 15.0f;
        features[39] = oddRatio;

        return features;
    }

    private static int CalculateACValue(int[] numbers)
    {
        var sorted = numbers.OrderBy(x => x).ToArray();
        var diffs = new HashSet<int>();
        for (int i = 0; i < sorted.Length; i++)
        {
            for (int j = i + 1; j < sorted.Length; j++)
            {
                diffs.Add(sorted[j] - sorted[i]);
            }
        }

        return diffs.Count - (sorted.Length - 1);
    }
}
