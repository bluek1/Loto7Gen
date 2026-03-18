using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

public class ScoringPredictionStrategy(List<int[]> history, Config config) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly Config _config = config;

    public string Key => "scoring";
    public string DisplayName => "Scoring";

    public List<int> Predict()
    {
        double[] scores = new double[38];
        int[] freq = new int[38];
        int[] lastSeen = new int[38];
        for (int i = 1; i <= 37; i++) lastSeen[i] = -1;

        for (int i = 0; i < _history.Count; i++)
        {
            foreach (int n in _history[i])
            {
                freq[n]++;
                lastSeen[n] = i;
            }
        }

        int currentIndex = _history.Count;
        for (int i = 1; i <= 37; i++)
        {
            int coldPeriod = lastSeen[i] == -1 ? currentIndex : currentIndex - 1 - lastSeen[i];
            double score = freq[i] * _config.Scoring.FrequencyWeight;

            if (coldPeriod > _config.Scoring.ColdBonusThreshold)
                score += _config.Scoring.ColdBonusValue;

            if (coldPeriod <= _config.Scoring.HotPenaltyThreshold)
                score -= _config.Scoring.HotPenaltyValue;

            // 동점 시 항상 낮은 번호만 선택되는 편향 방지용 미세 난수
            scores[i] = score + Random.Shared.NextDouble() * 1e-6;
        }

        return Enumerable.Range(1, 37)
            .OrderByDescending(i => scores[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }
}
