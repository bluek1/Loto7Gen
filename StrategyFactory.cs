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

        var subStrategies = new List<IPredictionStrategy> { scoring, wma, markov, lstm };
        var ensemble = new EnsemblePredictionStrategy(subStrategies, config);

        return [scoring, wma, markov, lstm, ensemble, random];
    }
}
