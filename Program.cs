using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;

namespace Loto7Gen
{
    public class Config
    {
        public ScoringConfig Scoring { get; set; } = new ScoringConfig();
        public WMAConfig WMA { get; set; } = new WMAConfig();
        public MarkovConfig Markov { get; set; } = new MarkovConfig();
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

    public class Program
    {
        const string HistoryFile = "history.json";
        const string PredictionFile = "predictions.json";
        const string ConfigFile = "config.json";

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("사용법: Loto7Gen generate [scoring|wma|markov|lstm|random] | backtest [scoring|wma|markov|lstm|random]");
                return;
            }

            var config = LoadOrGenerateConfig();
            var history = LoadOrGenerateHistory();

            string cmd = args[0].ToLower();
            string opt = args.Length > 1 ? args[1].ToLower() : "all";

            if (cmd == "generate")
            {
                GenerateDeterministic(history, opt, config);
            }
            else if (cmd == "backtest")
            {
                RunBacktestCmd(history, opt, config);
            }
            else
            {
                Console.WriteLine("지원하지 않는 명령어입니다.");
            }
        }

        static Config LoadOrGenerateConfig()
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

        static void GenerateDeterministic(List<int[]> history, string opt, Config config)
        {
            Console.WriteLine($"=== Loto7Gen V6.0 번호 추출 ({opt}) ===");
            var results = new Dictionary<string, List<int>>();
            var model = new DeterministicModels(history, config);

            if (opt == "scoring" || opt == "all")
                results["Scoring"] = model.GetScoring();
            if (opt == "wma" || opt == "all")
                results["WMA"] = model.GetWMA();
            if (opt == "markov" || opt == "all")
                results["Markov"] = model.GetMarkov();
            if (opt == "lstm" || opt == "all")
                results["LSTM"] = model.GetLSTM();
            if (opt == "random" || opt == "all")
                results["Random"] = model.GetRandom();

            foreach (var kvp in results)
            {
                Console.WriteLine($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
            }

            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PredictionFile, json);
            Console.WriteLine($"\n예측 결과가 '{PredictionFile}'에 저장되었습니다.");
        }

        static void RunBacktestCmd(List<int[]> fullHistory, string opt, Config config)
        {
            int m = 100; // 과거 100회차 데이터 사용
            if (fullHistory.Count <= m)
            {
                Console.WriteLine($"히스토리 데이터가 부족합니다 (최소 {m + 1}회차 필요).");
                return;
            }

            Console.WriteLine($"=== 백테스팅 시작 (모델: {opt}, 과거 {m}회차 기반 다음 1회 예측) ===");
            
            double totalMatches = 0;
            int testCount = fullHistory.Count - m;
            int costPerGame = 300;
            long totalCost = 0;
            long totalPrize = 0;
            int[] matchDist = new int[8];

            for (int i = m; i < fullHistory.Count; i++)
            {
                var trainData = fullHistory.Skip(i - m).Take(m).ToList();
                var actualWinning = fullHistory[i];
                var model = new DeterministicModels(trainData, config);
                
                var predictions = new List<List<int>>();

                if (opt == "scoring" || opt == "all") predictions.Add(model.GetScoring());
                if (opt == "wma" || opt == "all") predictions.Add(model.GetWMA());
                if (opt == "markov" || opt == "all") predictions.Add(model.GetMarkov());
                if (opt == "lstm" || opt == "all") predictions.Add(model.GetLSTM());
                if (opt == "random" || opt == "all") predictions.Add(model.GetRandom());

                foreach (var predicted in predictions)
                {
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
            int gamesPerTest = (opt == "all") ? 5 : 1;
            Console.WriteLine($"총 구매 게임 수: {testCount * gamesPerTest}게임");
            Console.WriteLine($"총 투자 비용: {totalCost:N0}엔");
            Console.WriteLine($"총 당첨 금액(추정): {totalPrize:N0}엔");
            Console.WriteLine($"순수익: {(totalPrize - totalCost):N0}엔 (수익률: {(double)totalPrize/totalCost * 100:F2}%)");
            Console.WriteLine($"평균 일치 개수(1게임당): {totalMatches / (testCount * gamesPerTest):F2}개");
            Console.WriteLine($"--- 당첨 분포 ---");
            for(int i = 7; i >= 3; i--) Console.WriteLine($"{i}개 일치: {matchDist[i]}번");
        }

        static List<int[]> LoadOrGenerateHistory()
        {
            if (File.Exists(HistoryFile))
            {
                return JsonSerializer.Deserialize<List<int[]>>(File.ReadAllText(HistoryFile)) ?? new List<int[]>();
            }
            
            Console.WriteLine("과거 150주치 가상 데이터를 생성합니다...");
            var rand = new Random();
            var history = new List<int[]>();
            for (int i = 0; i < 150; i++)
            {
                var set = Enumerable.Range(1, 37).OrderBy(x => rand.Next()).Take(7).OrderBy(x => x).ToArray();
                history.Add(set);
            }
            File.WriteAllText(HistoryFile, JsonSerializer.Serialize(history));
            return history;
        }
    }

    public class DeterministicModels
    {
        private List<int[]> _history;
        private Config _config;
        public DeterministicModels(List<int[]> history, Config config)
        {
            _history = history;
            _config = config;
        }

        public List<int> GetScoring()
        {
            double[] scores = new double[38];
            int[] freq = new int[38];
            int[] lastSeen = new int[38];
            for (int i = 1; i <= 37; i++) lastSeen[i] = -1;

            for (int i = 0; i < _history.Count; i++)
            {
                foreach (int n in _history[i])
                {
                    freq[n]++;
                    lastSeen[n] = i;
                }
            }

            int currentIndex = _history.Count;
            for (int i = 1; i <= 37; i++)
            {
                int coldPeriod = lastSeen[i] == -1 ? currentIndex : currentIndex - 1 - lastSeen[i];
                double score = freq[i] * _config.Scoring.FrequencyWeight; 
                
                // Cold bonus
                if (coldPeriod > _config.Scoring.ColdBonusThreshold) 
                    score += _config.Scoring.ColdBonusValue;
                
                // Hot penalty
                if (coldPeriod <= _config.Scoring.HotPenaltyThreshold) 
                    score -= _config.Scoring.HotPenaltyValue;

                scores[i] = score;
            }

            return Enumerable.Range(1, 37)
                .OrderByDescending(i => scores[i])
                .Take(7)
                .OrderBy(x => x)
                .ToList();
        }

        public List<int> GetWMA()
        {
            double[] scores = new double[38];
            int window = Math.Min(_config.WMA.WindowSize, _history.Count);
            
            for (int i = _history.Count - window; i < _history.Count; i++)
            {
                double weight = (i - (_history.Count - window) + 1);
                foreach (int n in _history[i])
                {
                    scores[n] += weight;
                }
            }

            return Enumerable.Range(1, 37)
                .OrderByDescending(i => scores[i])
                .Take(7)
                .OrderBy(x => x)
                .ToList();
        }

        public List<int> GetMarkov()
        {
            double[,] trans = new double[38, 38];
            int window = Math.Min(_config.Markov.WindowSize, _history.Count);
            var trainData = _history.Skip(_history.Count - window).ToList();
            
            for (int i = 0; i < trainData.Count - 1; i++)
            {
                var cur = trainData[i];
                var nxt = trainData[i + 1];
                foreach (var c in cur)
                {
                    foreach (var n in nxt)
                    {
                        trans[c, n] += 1.0;
                    }
                }
            }

            double[] prob = new double[38];
            var lastDraw = _history.Last();
            
            foreach (var n in lastDraw)
            {
                for (int nextN = 1; nextN <= 37; nextN++)
                {
                    prob[nextN] += trans[n, nextN];
                }
            }

            return Enumerable.Range(1, 37)
                .OrderByDescending(i => prob[i])
                .Take(7)
                .OrderBy(x => x)
                .ToList();
        }

        public List<int> GetLSTM()
        {
            const string modelPath = "loto7_lstm.onnx";
            if (!File.Exists(modelPath))
            {
                Console.WriteLine("Warning: LSTM 모델 파일이 없습니다. 랜덤으로 대체합니다.");
                return GetRandom();
            }

            try
            {
                var mlContext = new MLContext();
                
                // Feature Extraction (동일한 전처리 로직)
                var windowSize = 10;
                if (_history.Count < windowSize) return GetRandom();
                
                var lastWindow = _history.Skip(_history.Count - windowSize).ToList();
                var inputFeatures = new float[1 * 10 * 40]; // Batch * Seq * Dim

                for (int i = 0; i < windowSize; i++)
                {
                    var draw = lastWindow[i];
                    var features = ExtractFeatures(draw);
                    Array.Copy(features, 0, inputFeatures, i * 40, 40);
                }

                var dataView = mlContext.Data.LoadFromEnumerable(new List<OnnxInput> { new OnnxInput { input = inputFeatures } });
                
                var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    outputColumnNames: new[] { "output" },
                    inputColumnNames: new[] { "input" },
                    modelFile: modelPath);

                var transformedData = pipeline.Fit(dataView).Transform(dataView);
                var probabilities = mlContext.Data.CreateEnumerable<OnnxOutput>(transformedData, reuseRowObject: false).First().output;

                return Enumerable.Range(1, 37)
                    .OrderByDescending(i => probabilities[i - 1])
                    .Take(7)
                    .OrderBy(x => x)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LSTM Error: {ex.Message}");
                return GetRandom();
            }
        }

        private float[] ExtractFeatures(int[] draws)
        {
            var features = new float[40];
            foreach (var n in draws) features[n - 1] = 1.0f;
            
            var sum = draws.Sum();
            var ac = CalculateACValue(draws);
            var oddRatio = draws.Count(n => n % 2 != 0) / 7.0f;
            
            features[37] = (sum - 28) / (238.0f - 28.0f);
            features[38] = ac / 15.0f;
            features[39] = oddRatio;
            
            return features;
        }

        private int CalculateACValue(int[] numbers)
        {
            var sorted = numbers.OrderBy(x => x).ToArray();
            var diffs = new HashSet<int>();
            for (int i = 0; i < sorted.Length; i++)
            {
                for (int j = i + 1; j < sorted.Length; j++)
                {
                    diffs.Add(sorted[j] - sorted[i]);
                }
            }
            return diffs.Count - (sorted.Length - 1);
        }

        public List<int> GetRandom()
        {
            var rand = new Random();
            return Enumerable.Range(1, 37).OrderBy(x => rand.Next()).Take(7).OrderBy(x => x).ToList();
        }

        public class OnnxInput
        {
            [VectorType(1, 10, 40)]
            public float[] input { get; set; }
        }

        public class OnnxOutput
        {
            [ColumnName("output")]
            public float[] output { get; set; }
        }
    }
}