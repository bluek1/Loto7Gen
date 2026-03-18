using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

/// <summary>
/// 갭 분포 회귀 전략.
/// 각 번호의 미출현 기간(갭)을 기하분포로 모델링하여
/// "오버듀(overdue)" 확률을 계산하고, 빈도 점수와 결합.
/// </summary>
public class GapPredictionStrategy(List<int[]> history, Config config) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly Config _config = config;

    public string Key => "gap";
    public string DisplayName => "Gap";

    public List<int> Predict()
    {
        // 1) 각 번호별 갭(연속 미출현 횟수) 목록 수집
        double[] avgGap = new double[38];
        int[] currentGap = new int[38];
        int[] gapCount = new int[38];
        double[] gapSum = new double[38];

        bool[] appeared = new bool[38];

        for (int i = 0; i < _history.Count; i++)
        {
            var draw = _history[i];
            var drawSet = new HashSet<int>(draw);

            for (int n = 1; n <= 37; n++)
            {
                if (drawSet.Contains(n))
                {
                    if (appeared[n])
                    {
                        gapSum[n] += currentGap[n];
                        gapCount[n]++;
                    }
                    appeared[n] = true;
                    currentGap[n] = 0;
                }
                else
                {
                    currentGap[n]++;
                }
            }
        }

        // 2) 평균 갭 및 오버듀 확률 계산
        double[] overdueProb = new double[38];
        for (int n = 1; n <= 37; n++)
        {
            avgGap[n] = gapCount[n] > 0 ? gapSum[n] / gapCount[n] : _history.Count;

            // 기하분포 CDF: P(X <= currentGap) = 1 - (1 - p)^(currentGap+1)
            // p = 1 / avgGap (각 라운드에서 출현할 확률)
            double p = avgGap[n] > 0 ? 1.0 / avgGap[n] : 0.01;
            p = Math.Clamp(p, 0.01, 0.99);

            // CDF 값이 높을수록 = "충분히 기다렸으므로 출현할 차례"
            overdueProb[n] = 1.0 - Math.Pow(1.0 - p, currentGap[n] + 1);
        }

        // 3) 빈도 점수 + 오버듀 점수 결합
        int[] freq = new int[38];
        foreach (var draw in _history)
            foreach (int n in draw)
                freq[n]++;

        double totalDraws = _history.Count;
        double[] finalScore = new double[38];
        double freqWeight = _config.Gap.FrequencyWeight;
        double overdueWeight = _config.Gap.OverdueWeight;

        for (int n = 1; n <= 37; n++)
        {
            double freqScore = freq[n] / totalDraws;
            finalScore[n] = freqScore * freqWeight + overdueProb[n] * overdueWeight
                + Random.Shared.NextDouble() * 1e-6;
        }

        return Enumerable.Range(1, 37)
            .OrderByDescending(i => finalScore[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }
}
