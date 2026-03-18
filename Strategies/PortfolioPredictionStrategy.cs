using System;
using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

/// <summary>
/// 다중 티켓 포트폴리오 전략.
/// 앙상블 확률 분포에서 보완적인 N장의 티켓을 생성하여
/// 번호 커버리지를 극대화합니다.
/// </summary>
public class PortfolioPredictionStrategy : IPredictionStrategy
{
    private readonly List<IPredictionStrategy> _subStrategies;
    private readonly Config _config;
    private readonly int _ticketIndex;

    public string Key { get; }
    public string DisplayName { get; }

    public PortfolioPredictionStrategy(
        List<IPredictionStrategy> subStrategies,
        Config config,
        int ticketIndex)
    {
        _subStrategies = subStrategies;
        _config = config;
        _ticketIndex = ticketIndex;
        Key = $"portfolio{ticketIndex + 1}";
        DisplayName = $"Portfolio#{ticketIndex + 1}";
    }

    public List<int> Predict()
    {
        // 각 서브 전략에서 투표 기반 점수 수집
        double[] scores = new double[38];
        for (int s = 0; s < _subStrategies.Count; s++)
        {
            var predicted = _subStrategies[s].Predict();
            foreach (int num in predicted)
                scores[num] += 1.0; // 선택되면 동일 가중치
        }

        // 번호를 점수순으로 정렬
        var ranked = Enumerable.Range(1, 37)
            .OrderByDescending(i => scores[i])
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        List<int> result;
        int totalTickets = _config.Portfolio.TicketCount;
        int keepCount = Math.Max(2, (int)(7 * _config.Portfolio.OverlapRatio));

        if (_ticketIndex == 0)
        {
            // 1번 티켓: top-7
            result = ranked.Take(7).ToList();
        }
        else
        {
            // 핵심 번호 (top keepCount개) 유지
            var core = ranked.Take(keepCount).ToList();
            int need = 7 - keepCount;

            // 탐색 구간: ranked에서 ticketIndex에 따라 다른 슬라이스
            int poolStart = keepCount + need * (_ticketIndex - 1);
            int poolEnd = Math.Min(37, poolStart + need * 3);
            var pool = ranked.Skip(poolStart).Take(poolEnd - poolStart)
                .Where(n => !core.Contains(n))
                .OrderByDescending(n => scores[n] + Random.Shared.NextDouble() * 0.5)
                .Take(need)
                .ToList();

            // 부족하면 나머지에서 랜덤 보충
            if (pool.Count < need)
            {
                var filler = ranked.Where(n => !core.Contains(n) && !pool.Contains(n))
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(need - pool.Count);
                pool.AddRange(filler);
            }

            result = core.Concat(pool).Take(7).ToList();
        }

        result = result.OrderBy(x => x).ToList();

        // 조합 필터 시도 (최대 50회)
        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (CombinationFilter.PassesAll(result, _config.Filter))
                return result;

            // 필터 실패 시 최하위 점수 번호를 교체
            var worst = result.OrderBy(n => scores[n]).First();
            var unused = Enumerable.Range(1, 37).Where(n => !result.Contains(n)).ToList();
            if (unused.Count == 0) break;

            result.Remove(worst);
            result.Add(unused[Random.Shared.Next(unused.Count)]);
            result.Sort();
        }

        return result;
    }
}
