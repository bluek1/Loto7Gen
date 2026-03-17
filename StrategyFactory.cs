using Loto7Gen.Strategies;

namespace Loto7Gen;

public static class StrategyFactory
{
    public static IReadOnlyList<IPredictionStrategy> CreateAll(List<int[]> history, Config config)
    {
        var random = new RandomPredictionStrategy();
        return
        [
            new ScoringPredictionStrategy(history, config),
            new WmaPredictionStrategy(history, config),
            new MarkovPredictionStrategy(history, config),
            new LstmPredictionStrategy(history, random),
            random
        ];
    }
}
