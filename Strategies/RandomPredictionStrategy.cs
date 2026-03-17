using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

public class RandomPredictionStrategy : IPredictionStrategy
{
    public string Key => "random";
    public string DisplayName => "Random";

    public List<int> Predict()
    {
        return Enumerable.Range(1, 37)
            .OrderBy(_ => Random.Shared.Next())
            .Take(7)
            .OrderBy(x => x)
            .ToList();
    }
}
