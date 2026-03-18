using Loto7Gen.Strategies;

namespace Loto7Gen;

public static class StrategyFactory
{
    public static IReadOnlyList<IPredictionStrategy> CreateAll(List<int[]> history, Config config)
    {
        var random = new RandomPredictionStrategy();
        var scoring = new ScoringPredictionStrategy(history, config);
        var wma = new WmaPredictionStrategy(history, config);
        var markov = new MarkovPredictionStrategy(history, config);
        var lstm = new LstmPredictionStrategy(history, random);
        var cooccur = new CoOccurrencePredictionStrategy(history, config);
        var gap = new GapPredictionStrategy(history, config);

        var subStrategies = new List<IPredictionStrategy> { scoring, wma, markov, lstm, cooccur, gap };
        var ensemble = new EnsemblePredictionStrategy(subStrategies, config, history);

        var strategies = new List<IPredictionStrategy>
            { scoring, wma, markov, lstm, cooccur, gap, ensemble };

        // 포트폴리오 티켓 추가
        for (int t = 0; t < config.Portfolio.TicketCount; t++)
            strategies.Add(new PortfolioPredictionStrategy(subStrategies, config, t));

        strategies.Add(random);
        return strategies;
    }
}
