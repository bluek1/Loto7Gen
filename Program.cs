using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Loto7Gen
{
    public class Program
    {
        const string HistoryFile = "history.json";
        const string PredictionFile = "predictions.json";

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("사용법: Loto7Gen generate | verify 1 2 3 4 5 6 7 | backtest | optimize");
                return;
            }

            var history = LoadOrGenerateHistory();

            if (args[0] == "generate")
            {
                var stats = new Stats(history);
                GenerateNumbers(stats);
            }
            else if (args[0] == "verify" && args.Length == 8)
            {
                var winning = args.Skip(1).Select(int.Parse).OrderBy(x => x).ToList();
                VerifyNumbers(winning);
            }
            else if (args[0] == "backtest")
            {
                RunBacktestCmd(history);
            }
            else if (args[0] == "optimize")
            {
                RunOptimize(history);
            }
            else
            {
                Console.WriteLine("잘못된 명령입니다.");
            }
        }

        static void RunBacktestCmd(List<int[]> fullHistory)
        {
            var result = RunBacktest(fullHistory, true);
        }

        static (long totalPrize, double totalMatches) RunBacktest(List<int[]> fullHistory, bool printOutput = false)
        {
            int m = 100; // 과거 100회차 데이터 사용
            if (fullHistory.Count <= m)
            {
                if (printOutput) Console.WriteLine($"히스토리 데이터가 부족합니다 (최소 {m + 1}회차 필요).");
                return (0, 0);
            }

            if (printOutput) Console.WriteLine($"=== 백테스팅 시작 (과거 {m}회차 기반 다음 1회 예측) ===");
            
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

                var stats = new Stats(trainData);
                
                var predictions = new List<List<int>>();
                predictions.Add(GenerateUntil(stats.GenerateAssociationWeighted, nums => Filters.CheckSum(nums) && Filters.CheckHighLow(nums) && Filters.CheckOddEven(nums), printOutput));
                predictions.Add(GenerateUntil(stats.GenerateWeighted, nums => Filters.CheckOddEven(nums), printOutput));
                predictions.Add(GenerateUntil(stats.GenerateMarkovWeightedFirst, nums => Filters.CheckConsecutive(nums), printOutput));
                predictions.Add(GenerateUntil(stats.GenerateMarkov, nums => Filters.CheckSum(nums) && Filters.CheckConsecutive(nums), printOutput));
                predictions.Add(GenerateUntil(stats.GenerateMarkov, nums => Filters.CheckHighLow(nums), printOutput));

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

            if (printOutput)
            {
                Console.WriteLine($"테스트 횟수(회차): {testCount}회");
                Console.WriteLine($"총 구매 게임 수: {testCount * 5}게임");
                Console.WriteLine($"총 투자 비용: {totalCost:N0}엔");
                Console.WriteLine($"총 당첨 금액(추정): {totalPrize:N0}엔");
                Console.WriteLine($"순수익: {(totalPrize - totalCost):N0}엔 (수익률: {(double)totalPrize/totalCost * 100:F2}%)");
                Console.WriteLine($"평균 일치 개수(1게임당): {totalMatches / (testCount * 5):F2}개");
                Console.WriteLine($"--- 당첨 분포 ---");
                for(int i=7; i>=3; i--) Console.WriteLine($"{i}개 일치: {matchDist[i]}번");
            }
            
            return (totalPrize, totalMatches);
        }

        static void RunOptimize(List<int[]> fullHistory)
        {
            Console.WriteLine("=== 유전 알고리즘 / 파라미터 최적화 시작 ===");
            var rand = new Random();
            int generations = 50; // 수백 세대는 너무 오래 걸릴 수 있으므로 50세대로 제한
            
            double bestMatches = 0;
            long bestPrize = 0;
            string bestParams = "";

            for (int i = 0; i < generations; i++)
            {
                Filters.MinSum = rand.Next(80, 110);
                Filters.MaxSum = rand.Next(150, 190);
                Stats.ColdBonusMultiplier = 1.0 + (rand.NextDouble() * 4.0); // 1.0 ~ 5.0
                Stats.HotPenaltyMultiplier = rand.NextDouble(); // 0.0 ~ 1.0
                
                var (prize, matches) = RunBacktest(fullHistory, false);
                
                if (matches > bestMatches || (matches == bestMatches && prize > bestPrize))
                {
                    bestMatches = matches;
                    bestPrize = prize;
                    bestParams = $"MinSum: {Filters.MinSum}, MaxSum: {Filters.MaxSum}, ColdBonus: {Stats.ColdBonusMultiplier:F2}, HotPenalty: {Stats.HotPenaltyMultiplier:F2}";
                    Console.WriteLine($"[새로운 최고 기록 세대 {i+1}] 일치수: {bestMatches}, 수익: {bestPrize:N0}엔 => {bestParams}");
                }
            }
            
            Console.WriteLine("\n=== 최적화 결과 ===");
            Console.WriteLine($"최적 파라미터: {bestParams}");
            Console.WriteLine($"최고 일치 횟수: {bestMatches}");
            Console.WriteLine($"예상 수익: {bestPrize:N0}엔");
        }

        static void GenerateNumbers(Stats stats)
        {
            Console.WriteLine("=== 로또 7 번호 생성 시작 (V3.0) ===");
            var results = new Dictionary<string, List<int>>();

            results["Combo_1_2_3"] = GenerateUntil(stats.GenerateAssociationWeighted, 
                nums => Filters.CheckSum(nums) && Filters.CheckHighLow(nums) && Filters.CheckOddEven(nums), true);

            results["Combo_3_4"] = GenerateUntil(stats.GenerateWeighted, 
                nums => Filters.CheckOddEven(nums), true);

            results["Combo_4_5_6"] = GenerateUntil(stats.GenerateMarkovWeightedFirst, 
                nums => Filters.CheckConsecutive(nums), true);

            results["Combo_1_5_6"] = GenerateUntil(stats.GenerateMarkov, 
                nums => Filters.CheckSum(nums) && Filters.CheckConsecutive(nums), true);

            results["Combo_2_6"] = GenerateUntil(stats.GenerateMarkov, 
                nums => Filters.CheckHighLow(nums), true);

            foreach (var kvp in results)
            {
                Console.WriteLine($"[{kvp.Key}] {string.Join(", ", kvp.Value)}");
            }

            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PredictionFile, json);
            Console.WriteLine($"\n생성된 번호가 '{PredictionFile}'에 저장되었습니다.");
        }

        static void VerifyNumbers(List<int> winning)
        {
            Console.WriteLine($"당첨 번호: {string.Join(", ", winning)}");
            if (!File.Exists(PredictionFile))
            {
                Console.WriteLine("저장된 예측 번호 파일이 없습니다. 먼저 generate를 실행하세요.");
                return;
            }

            var json = File.ReadAllText(PredictionFile);
            var results = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json);

            Console.WriteLine("\n=== 당첨 결과 확인 ===");
            foreach (var kvp in results)
            {
                var matchCount = kvp.Value.Intersect(winning).Count();
                Console.WriteLine($"[{kvp.Key}] {string.Join(", ", kvp.Value)} -> 일치: {matchCount}개");
            }
        }

        static List<int[]> LoadOrGenerateHistory()
        {
            if (File.Exists(HistoryFile))
            {
                return JsonSerializer.Deserialize<List<int[]>>(File.ReadAllText(HistoryFile));
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

        static List<int> GenerateUntil(Func<List<int>> generator, Func<List<int>, bool> filter, bool printWarning = false)
        {
            int maxAttempts = 10000; // 최적화 시 너무 오래 걸리는 것을 방지
            for (int i = 0; i < maxAttempts; i++)
            {
                var nums = generator();
                if (filter(nums)) return nums;
            }
            
            if (printWarning) Console.WriteLine("경고: 최대 시도 횟수를 초과하여 기본 난수로 대체합니다.");
            return generator();
        }
    }

    public static class Filters
    {
        public static int MinSum = 100;
        public static int MaxSum = 160;

        public static bool CheckSum(List<int> nums)
        {
            int s = nums.Sum();
            return s >= MinSum && s <= MaxSum;
        }

        public static bool CheckHighLow(List<int> nums)
        {
            int high = nums.Count(x => x >= 19);
            int low = nums.Count(x => x <= 18);
            return (high == 3 && low == 4) || (high == 4 && low == 3);
        }

        public static bool CheckOddEven(List<int> nums)
        {
            int odd = nums.Count(x => x % 2 != 0);
            int even = nums.Count(x => x % 2 == 0);
            return (odd == 3 && even == 4) || (odd == 4 && even == 3);
        }

        public static bool CheckConsecutive(List<int> nums)
        {
            int consecutiveCount = 0;
            for (int i = 0; i < nums.Count - 1; i++)
            {
                if (nums[i + 1] - nums[i] == 1) consecutiveCount++;
            }
            return consecutiveCount >= 1 && consecutiveCount <= 2;
        }
    }

    public class Stats
    {
        public static double ColdBonusMultiplier = 2.0;
        public static double HotPenaltyMultiplier = 0.5;

        static readonly Random _rand = new Random();
        double[] _weights = new double[38];
        int[,] _pairCounts = new int[38, 38];
        Dictionary<int, List<int>> _markov = new Dictionary<int, List<int>>();

        public Stats(List<int[]> history)
        {
            double[] counts = new double[38];
            int[] shortTermCounts = new int[38];
            int shortTermWindow = 25;
            
            for (int i = 0; i < history.Count; i++)
            {
                var set = history[i];
                
                double recencyWeight = 1.0 + ((double)i / history.Count);

                foreach (var n in set) 
                {
                    counts[n] += recencyWeight;
                    if (i >= history.Count - shortTermWindow)
                    {
                        shortTermCounts[n]++;
                    }
                }
                
                for (int j = 0; j < set.Length; j++)
                {
                    for (int k = j + 1; k < set.Length; k++)
                    {
                        _pairCounts[set[j], set[k]]++;
                        _pairCounts[set[k], set[j]]++;
                    }

                    if (j < set.Length - 1)
                    {
                        if (!_markov.ContainsKey(set[j])) _markov[set[j]] = new List<int>();
                        _markov[set[j]].Add(set[j + 1]);
                    }
                }
            }

            // 장단기 편향 보정 로직
            int nTotal = history.Count;
            int nShort = Math.Min(nTotal, shortTermWindow);
            
            double expectedTotal = nTotal * 7.0 / 37.0;
            double expectedShort = nShort * 7.0 / 37.0;
            
            double total = counts.Sum();
            if (total == 0) total = 1;

            for (int i = 1; i <= 37; i++)
            {
                double baseW = counts[i] > 0 ? counts[i] / total : 0.01;
                
                // 장기 Cold 보너스 (너무 안나온 번호)
                double rawCount = counts[i] / (1.5); // 대략적인 raw count (가중치 제거)
                if (rawCount < expectedTotal * 0.7)
                {
                    baseW *= ColdBonusMultiplier;
                }
                
                // 단기 Hot 패널티 (최근 너무 많이 나온 번호)
                if (shortTermCounts[i] > expectedShort * 1.5)
                {
                    baseW *= HotPenaltyMultiplier;
                }
                
                _weights[i] = Math.Max(baseW, 0.001);
            }
            
            // Re-normalize
            double newTotal = _weights.Sum();
            for (int i = 1; i <= 37; i++)
            {
                _weights[i] /= newTotal;
            }
        }

        public List<int> GenerateRandom()
        {
            return Enumerable.Range(1, 37).OrderBy(x => _rand.Next()).Take(7).OrderBy(x => x).ToList();
        }

        public List<int> GenerateWeighted()
        {
            var nums = new HashSet<int>();
            while (nums.Count < 7)
            {
                double r = _rand.NextDouble();
                double cumulative = 0.0;
                for (int i = 1; i <= 37; i++)
                {
                    cumulative += _weights[i];
                    if (r <= cumulative)
                    {
                        nums.Add(i);
                        break;
                    }
                }
            }
            return nums.OrderBy(x => x).ToList();
        }

        public List<int> GenerateAssociationWeighted()
        {
            var nums = new HashSet<int>();
            while (nums.Count < 7)
            {
                double[] currentWeights = new double[38];
                double totalWeight = 0;

                for (int i = 1; i <= 37; i++)
                {
                    if (nums.Contains(i)) continue;

                    double baseW = _weights[i];
                    double assocBonus = 1.0;

                    foreach (var picked in nums)
                    {
                        assocBonus += _pairCounts[picked, i] * 0.1; 
                    }

                    double w = baseW * assocBonus;
                    currentWeights[i] = w;
                    totalWeight += w;
                }

                double r = _rand.NextDouble() * totalWeight;
                double cumulative = 0.0;
                for (int i = 1; i <= 37; i++)
                {
                    if (nums.Contains(i)) continue;
                    
                    cumulative += currentWeights[i];
                    if (r <= cumulative)
                    {
                        nums.Add(i);
                        break;
                    }
                }
            }
            return nums.OrderBy(x => x).ToList();
        }

        public List<int> GenerateMarkov()
        {
            return BuildMarkovSet(GenerateRandom()[0]);
        }

        public List<int> GenerateMarkovWeightedFirst()
        {
            return BuildMarkovSet(GenerateWeighted()[0]);
        }

        private List<int> BuildMarkovSet(int first)
        {
            var nums = new HashSet<int> { first };
            int current = first;

            while (nums.Count < 7)
            {
                if (_markov.ContainsKey(current) && _markov[current].Count > 0)
                {
                    var nextList = _markov[current];
                    int next = nextList[_rand.Next(nextList.Count)];
                    
                    if (nums.Contains(next) || next <= current) 
                    {
                        next = Enumerable.Range(current + 1, 37 - current).OrderBy(x => _rand.Next()).FirstOrDefault();
                        if (next == 0) next = GenerateRandom()[0];
                    }
                    
                    nums.Add(next);
                    current = next;
                }
                else
                {
                    int next = Enumerable.Range(current + 1, 37 - current).OrderBy(x => _rand.Next()).FirstOrDefault();
                    if (next == 0) next = GenerateRandom()[0];
                    nums.Add(next);
                    current = next;
                }
            }
            return nums.OrderBy(x => x).ToList();
        }
    }
}
