namespace Loto7Gen;

public class Config
{
    public ScoringConfig Scoring { get; set; } = new();
    public WMAConfig WMA { get; set; } = new();
    public MarkovConfig Markov { get; set; } = new();
}

public class ScoringConfig
{
    public double FrequencyWeight { get; set; } = 1.0;
    public int ColdBonusThreshold { get; set; } = 10;
    public double ColdBonusValue { get; set; } = 5.0;
    public int HotPenaltyThreshold { get; set; } = 0;
    public double HotPenaltyValue { get; set; } = 3.0;
}

public class WMAConfig
{
    public int WindowSize { get; set; } = 50;
}

public class MarkovConfig
{
    public int WindowSize { get; set; } = 100;
}
