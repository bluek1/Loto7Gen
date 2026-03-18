using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

/// <summary>
/// 번호 쌍 동시출현(Co-occurrence) 분석 전략.
/// 단순 개별 빈도가 아닌, 함께 출현한 번호 쌍의 상관관계를 활용.
/// </summary>
public class CoOccurrencePredictionStrategy(List<int[]> history, Config config) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly Config _config = config;

    public string Key => "cooccur";
    public string DisplayName => "CoOccur";

    public List<int> Predict()
    {
        int window = Math.Min(_config.CoOccurrence.WindowSize, _history.Count);
        var trainData = _history.Skip(_history.Count - window).ToList();

        // 개별 출현 횟수
        int[] freq = new int[38];
        // 동시 출현 횟수 (대칭 행렬)
        int[,] coFreq = new int[38, 38];

        foreach (var draw in trainData)
        {
            foreach (int n in draw)
                freq[n]++;

            for (int a = 0; a < draw.Length; a++)
            {
                for (int b = a + 1; b < draw.Length; b++)
                {
                    coFreq[draw[a], draw[b]]++;
                    coFreq[draw[b], draw[a]]++;
                }
            }
        }

        // Lift 점수: 관측 동시출현 / 기대 동시출현
        // Lift(i,j) = P(i∩j) / (P(i) × P(j))
        double total = trainData.Count;
        double[,] lift = new double[38, 38];
        for (int i = 1; i <= 37; i++)
        {
            for (int j = i + 1; j <= 37; j++)
            {
                double pI = freq[i] / total;
                double pJ = freq[j] / total;
                double pIJ = coFreq[i, j] / total;
                if (pI > 0 && pJ > 0)
                {
                    lift[i, j] = pIJ / (pI * pJ);
                    lift[j, i] = lift[i, j];
                }
            }
        }

        // 개별 번호 점수: 빈도 기반
        double[] baseScore = new double[38];
        for (int i = 1; i <= 37; i++)
            baseScore[i] = freq[i] / total;

        // 탐욕적 선택: 첫 번호는 최고 빈도, 이후 동시출현 Lift가 높은 번호 추가
        var selected = new List<int>();

        // 최근 회차에서 출현한 번호에 약간의 보너스
        double[] recencyBonus = new double[38];
        int recentWindow = Math.Min(5, _history.Count);
        for (int r = 0; r < recentWindow; r++)
        {
            double w = (recentWindow - r) * 0.02;
            foreach (int n in _history[_history.Count - 1 - r])
                recencyBonus[n] += w;
        }

        // 초기 점수 = 빈도 + 최근 보너스
        double[] score = new double[38];
        for (int i = 1; i <= 37; i++)
            score[i] = baseScore[i] + recencyBonus[i] + Random.Shared.NextDouble() * 1e-6;

        // 첫 번호 선택
        int first = Enumerable.Range(1, 37).OrderByDescending(i => score[i]).First();
        selected.Add(first);

        // 나머지 6개: 기존 선택 번호와의 Lift 합산 + 개별 점수
        while (selected.Count < 7)
        {
            double bestCombinedScore = double.MinValue;
            int bestNum = -1;

            for (int candidate = 1; candidate <= 37; candidate++)
            {
                if (selected.Contains(candidate)) continue;

                double combinedScore = score[candidate];
                foreach (int s in selected)
                    combinedScore += lift[candidate, s] * _config.CoOccurrence.LiftWeight;

                if (combinedScore > bestCombinedScore)
                {
                    bestCombinedScore = combinedScore;
                    bestNum = candidate;
                }
            }

            if (bestNum > 0) selected.Add(bestNum);
            else break;
        }

        return selected.OrderBy(x => x).ToList();
    }
}
