using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

/// <summary>
/// 앙상블 전략: 4개 서브 전략(Scoring, WMA, Markov, LSTM)의 예측을
/// 가중 투표로 결합하고, softmax 확률 샘플링 + 조합 필터를 적용.
/// </summary>
public class EnsemblePredictionStrategy : IPredictionStrategy
{
    private readonly List<IPredictionStrategy> _subStrategies;
    private readonly double[] _weights;
    private readonly Config _config;

    public string Key => "ensemble";
    public string DisplayName => "Ensemble";

    public EnsemblePredictionStrategy(List<IPredictionStrategy> subStrategies, Config config)
    {
        _subStrategies = subStrategies;
        _config = config;
        _weights =
        [
            config.Ensemble.ScoringWeight,
            config.Ensemble.WmaWeight,
            config.Ensemble.MarkovWeight,
            config.Ensemble.LstmWeight
        ];
    }

    public List<int> Predict()
    {
        double[] scores = ComputeEnsembleScores();

        if (_config.Ensemble.Temperature > 0)
            return ProbabilisticPredict(scores);

        // Temperature == 0 → 결정론적 top-7
        for (int i = 1; i <= 37; i++)
            scores[i] += Random.Shared.NextDouble() * 1e-6;

        return Enumerable.Range(1, 37)
            .OrderByDescending(i => scores[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }

    private double[] ComputeEnsembleScores()
    {
        double[] scores = new double[38];
        for (int s = 0; s < _subStrategies.Count; s++)
        {
            double w = s < _weights.Length ? _weights[s] : 1.0;
            var predicted = _subStrategies[s].Predict();
            foreach (var num in predicted)
                scores[num] += w;
        }
        return scores;
    }

    /// <summary>
    /// softmax 온도 기반 확률 샘플링 + 조합 필터 적용.
    /// 최대 200회 시도 후 가장 많은 필터를 통과한 조합을 반환.
    /// </summary>
    private List<int> ProbabilisticPredict(double[] scores)
    {
        double temperature = _config.Ensemble.Temperature;
        double[] probs = SoftmaxWithTemperature(scores, temperature);
        const int maxAttempts = 200;
        List<int>? bestCandidate = null;
        int bestFilterScore = -1;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var sampled = WeightedSampleWithoutReplacement(probs, 7);
            var result = sampled.Select(idx => idx + 1).OrderBy(x => x).ToList();

            if (CombinationFilter.PassesAll(result, _config.Filter))
                return result;

            int filterScore = CountPassedFilters(result);
            if (filterScore > bestFilterScore)
            {
                bestFilterScore = filterScore;
                bestCandidate = result;
            }
        }

        return bestCandidate ?? Enumerable.Range(1, 37)
            .OrderByDescending(i => scores[i])
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }

    private int CountPassedFilters(List<int> nums)
    {
        int count = 0;
        if (CombinationFilter.CheckSumRange(nums, _config.Filter.SumMin, _config.Filter.SumMax)) count++;
        if (CombinationFilter.CheckHighLowRatio(nums)) count++;
        if (CombinationFilter.CheckOddEvenRatio(nums)) count++;
        if (CombinationFilter.CheckConsecutive(nums, _config.Filter.ConsecutiveMin, _config.Filter.ConsecutiveMax)) count++;
        return count;
    }

    private static double[] SoftmaxWithTemperature(double[] scores, double temperature)
    {
        double[] logits = new double[37];
        for (int i = 0; i < 37; i++)
            logits[i] = scores[i + 1] / temperature;

        double maxLogit = logits.Max();
        double[] exps = new double[37];
        double sumExp = 0;
        for (int i = 0; i < 37; i++)
        {
            exps[i] = Math.Exp(logits[i] - maxLogit);
            sumExp += exps[i];
        }

        double[] probs = new double[37];
        for (int i = 0; i < 37; i++)
            probs[i] = exps[i] / sumExp;

        return probs;
    }

    private static List<int> WeightedSampleWithoutReplacement(double[] probs, int count)
    {
        var result = new List<int>(count);
        var available = new double[probs.Length];
        Array.Copy(probs, available, probs.Length);

        for (int i = 0; i < count; i++)
        {
            double total = 0;
            for (int j = 0; j < available.Length; j++) total += available[j];
            if (total <= 0) break;

            double r = Random.Shared.NextDouble() * total;
            double cumulative = 0;

            for (int j = 0; j < available.Length; j++)
            {
                cumulative += available[j];
                if (r < cumulative)
                {
                    result.Add(j);
                    available[j] = 0;
                    break;
                }
            }
        }

        // 부족하면 미선택 번호에서 랜덤으로 채움
        if (result.Count < count)
        {
            var unused = Enumerable.Range(0, probs.Length)
                .Where(j => !result.Contains(j))
                .OrderBy(_ => Random.Shared.Next())
                .Take(count - result.Count);
            result.AddRange(unused);
        }

        return result;
    }
}
