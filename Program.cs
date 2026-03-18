using System.Text.Json;
using Loto7Gen.Strategies;

namespace Loto7Gen;

public class Program
{
    private const string HistoryFile = "history.json";
    private const string PredictionFile = "predictions.json";
    private const string ConfigFile = "config.json";

    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("사용법: Loto7Gen generate [scoring|wma|markov|lstm|ensemble|random] | backtest [전략] [횟수]");
            return;
        }

        var config = LoadOrGenerateConfig();
        var history = LoadOrGenerateHistory();

        string cmd = args[0].ToLower();
        string opt = args.Length > 1 ? args[1].ToLower() : "all";
        int? testRounds = args.Length > 2 && int.TryParse(args[2], out int tr) ? tr : null;

        if (cmd == "generate")
        {
            GeneratePredictions(history, opt, config);
        }
        else if (cmd == "backtest")
        {
            RunBacktestCmd(history, opt, config, testRounds);
        }
        else
        {
            Console.WriteLine("지원하지 않는 명령어입니다.");
        }
    }

    private static Config LoadOrGenerateConfig()
    {
        if (File.Exists(ConfigFile))
        {
            try
            {
                string json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
            catch
            {
                Console.WriteLine("config.json 형식이 잘못되었습니다. 기본값을 사용합니다.");
                return new Config();
            }
        }

        var defaultConfig = new Config();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(defaultConfig, options));
        Console.WriteLine("기본 설정 파일을 생성했습니다 (config.json).");
        return defaultConfig;
    }

    private static void GeneratePredictions(List<int[]> history, string opt, Config config)
    {
        Console.WriteLine($"=== Loto7Gen V8.0 번호 추출 ({opt}) ===");

        var strategies = SelectStrategies(history, config, opt);
        var results = new Dictionary<string, List<int>>();

        foreach (var strategy in strategies)
        {
            results[strategy.DisplayName] = strategy.Predict();
        }

        foreach (var kvp in results)
        {
            Console.WriteLine($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
        }

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PredictionFile, json);
        Console.WriteLine($"\n예측 결과가 '{PredictionFile}'에 저장되었습니다.");
    }

    private static void RunBacktestCmd(List<int[]> fullHistory, string opt, Config config, int? testRounds)
    {
        int m = 100;
        if (fullHistory.Count <= m)
        {
            Console.WriteLine($"히스토리 데이터가 부족합니다 (최소 {m + 1}회차 필요).");
            return;
        }

        int maxTests = fullHistory.Count - m;
        int testCount = testRounds.HasValue ? Math.Min(testRounds.Value, maxTests) : maxTests;
        int startIdx = fullHistory.Count - testCount;

        Console.WriteLine($"=== 백테스팅 시작 (모델: {opt}, 과거 {m}회차 기반 다음 1회 예측, {testCount}회 테스트) ===");

        double totalMatches = 0;
        int costPerGame = 300;
        long totalCost = 0;
        long totalPrize = 0;
        int[] matchDist = new int[8];
        int gamesPerTest = 0;

        for (int i = startIdx; i < fullHistory.Count; i++)
        {
            var trainData = fullHistory.Skip(i - m).Take(m).ToList();
            var actualWinning = fullHistory[i];
            var strategies = SelectStrategies(trainData, config, opt);

            if (gamesPerTest == 0)
                gamesPerTest = strategies.Count;

            foreach (var strategy in strategies)
            {
                var predicted = strategy.Predict();
                totalCost += costPerGame;

                int matchCount = predicted.Intersect(actualWinning).Count();
                totalMatches += matchCount;
                matchDist[matchCount]++;

                if (matchCount == 7) totalPrize += 600000000;
                else if (matchCount == 6) totalPrize += 730000;
                else if (matchCount == 5) totalPrize += 9100;
                else if (matchCount == 4) totalPrize += 1400;
                else if (matchCount == 3) totalPrize += 1000;
            }
        }

        Console.WriteLine($"테스트 횟수(회차): {testCount}회");
        Console.WriteLine($"총 구매 게임 수: {testCount * gamesPerTest}게임");
        Console.WriteLine($"총 투자 비용: {totalCost:N0}엔");
        Console.WriteLine($"총 당첨 금액(추정): {totalPrize:N0}엔");
        Console.WriteLine($"순수익: {(totalPrize - totalCost):N0}엔 (수익률: {(double)totalPrize / totalCost * 100:F2}%)");
        Console.WriteLine($"평균 일치 개수(1게임당): {totalMatches / (testCount * gamesPerTest):F2}개");
        Console.WriteLine("--- 당첨 분포 ---");
        for (int i = 7; i >= 3; i--) Console.WriteLine($"{i}개 일치: {matchDist[i]}번");
    }

    private static List<IPredictionStrategy> SelectStrategies(List<int[]> history, Config config, string opt)
    {
        var all = StrategyFactory.CreateAll(history, config);
        if (opt == "all")
            return all.ToList();

        var selected = all.Where(s => s.Key == opt).ToList();
        if (selected.Count == 0)
        {
            Console.WriteLine($"알 수 없는 모델 '{opt}' 입니다. 전체 모델(all)로 진행합니다.");
            return all.ToList();
        }

        return selected;
    }

    private static List<int[]> LoadOrGenerateHistory()
    {
        if (File.Exists(HistoryFile))
        {
            try
            {
                return JsonSerializer.Deserialize<List<int[]>>(File.ReadAllText(HistoryFile)) ?? [];
            }
            catch
            {
                Console.WriteLine("history.json 형식이 잘못되었습니다. 새 가상 데이터를 생성합니다.");
            }
        }

        Console.WriteLine("과거 150주치 가상 데이터를 생성합니다...");
        var history = new List<int[]>();
        for (int i = 0; i < 150; i++)
        {
            var set = Enumerable.Range(1, 37)
                .OrderBy(_ => Random.Shared.Next())
                .Take(7)
                .OrderBy(x => x)
                .ToArray();

            history.Add(set);
        }

        File.WriteAllText(HistoryFile, JsonSerializer.Serialize(history));
        return history;
    }
}
