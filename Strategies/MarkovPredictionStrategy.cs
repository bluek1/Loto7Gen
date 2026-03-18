using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

public class MarkovPredictionStrategy(List<int[]> history, Config config) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly Config _config = config;

    public string Key => "markov";
    public string DisplayName => "Markov";

    public List<int> Predict()
    {
        double[,] trans = new double[38, 38];
        int window = Math.Min(_config.Markov.WindowSize, _history.Count);
        var trainData = _history.Skip(_history.Count - window).ToList();

        for (int i = 0; i < trainData.Count - 1; i++)
        {
            var cur = trainData[i];
            var nxt = trainData[i + 1];
            foreach (var c in cur)
            {
                foreach (var n in nxt)
                {
                    trans[c, n] += 1.0;
                }
            }
        }

        double[] prob = new double[38];
        var lastDraw = _history.Last();

        foreach (var n in lastDraw)
        {
            // 전이 확률 정규화: 행 합계로 나눠 번호 출현 빈도 편향 제거
            double rowSum = 0;
            for (int nextN = 1; nextN <= 37; nextN++)
                rowSum += trans[n, nextN];

            if (rowSum == 0) continue;
            for (int nextN = 1; nextN <= 37; nextN++)
                prob[nextN] += trans[n, nextN] / rowSum;
        }

        return Enumerable.Range(1, 37)
            .OrderByDescending(i => prob[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }
}
