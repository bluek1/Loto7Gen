using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

public class WmaPredictionStrategy(List<int[]> history, Config config) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly Config _config = config;

    public string Key => "wma";
    public string DisplayName => "WMA";

    public List<int> Predict()
    {
        double[] scores = new double[38];
        int window = Math.Min(_config.WMA.WindowSize, _history.Count);

        for (int i = _history.Count - window; i < _history.Count; i++)
        {
            double weight = i - (_history.Count - window) + 1;
            foreach (int n in _history[i])
            {
                scores[n] += weight;
            }
        }

        return Enumerable.Range(1, 37)
            .OrderByDescending(i => scores[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }
}
