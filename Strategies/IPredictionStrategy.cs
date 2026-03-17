using System.Collections.Generic;

namespace Loto7Gen.Strategies;

public interface IPredictionStrategy
{
    string Key { get; }
    string DisplayName { get; }
    List<int> Predict();
}
