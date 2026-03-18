namespace Loto7Gen;

public class Config
{
    public ScoringConfig Scoring { get; set; } = new();
    public WMAConfig WMA { get; set; } = new();
    public MarkovConfig Markov { get; set; } = new();
    public EnsembleConfig Ensemble { get; set; } = new();
    public FilterConfig Filter { get; set; } = new();
    public CoOccurrenceConfig CoOccurrence { get; set; } = new();
    public GapConfig Gap { get; set; } = new();
    public PortfolioConfig Portfolio { get; set; } = new();
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

public class EnsembleConfig
{
    public double ScoringWeight { get; set; } = 1.0;
    public double WmaWeight { get; set; } = 1.0;
    public double MarkovWeight { get; set; } = 1.0;
    public double LstmWeight { get; set; } = 1.5;
    public double CoOccurWeight { get; set; } = 1.2;
    public double GapWeight { get; set; } = 1.0;
    public double Temperature { get; set; } = 0.5;
    public int AdaptiveWindow { get; set; } = 20;
    public double AdaptiveBlend { get; set; } = 0.5;
}

public class FilterConfig
{
    public int SumMin { get; set; } = 100;
    public int SumMax { get; set; } = 160;
    public int ConsecutiveMin { get; set; } = 1;
    public int ConsecutiveMax { get; set; } = 2;
}

public class CoOccurrenceConfig
{
    public int WindowSize { get; set; } = 100;
    public double LiftWeight { get; set; } = 0.3;
}

public class GapConfig
{
    public double FrequencyWeight { get; set; } = 1.0;
    public double OverdueWeight { get; set; } = 2.0;
}

public class PortfolioConfig
{
    public int TicketCount { get; set; } = 3;
    public double OverlapRatio { get; set; } = 0.3;
}
