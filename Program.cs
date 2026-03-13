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
                Console.WriteLine("사용법: Loto7Gen generate | verify 1 2 3 4 5 6 7 | backtest");
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
                RunBacktest(history);
            }
            else
            {
                Console.WriteLine("잘못된 명령입니다.");
            }
        }

        static void RunBacktest(List<int[]> fullHistory)
        {
            int m = 100; // 과거 100회차 데이터 사용
            if (fullHistory.Count <= m)
            {
                Console.WriteLine($"히스토리 데이터가 부족합니다 (최소 {m + 1}회차 필요).");
                return;
            }

            Console.WriteLine($"=== 백테스팅 시작 (과거 {m}회차 기반 다음 1회 예측) ===");
            
            double totalMatches = 0;
            int testCount = fullHistory.Count - m;

            for (int i = m; i < fullHistory.Count; i++)
            {
                var trainData = fullHistory.Skip(i - m).Take(m).ToList();
                var actualWinning = fullHistory[i];

                var stats = new Stats(trainData);
                
                // 대표적으로 Combo_1_2_3 모델을 사용 (또는 무작위 5게임 중 최고 등등)
                // 속도를 위해 1게임만 생성하여 비교
                var predicted = GenerateUntil(stats.GenerateAssociationWeighted, 
                    nums => Filters.CheckSum(nums) && Filters.CheckHighLow(nums) && Filters.CheckOddEven(nums));

                int matchCount = predicted.Intersect(actualWinning).Count();
                totalMatches += matchCount;
            }

            Console.WriteLine($"테스트 횟수: {testCount}회");
            Console.WriteLine($"평균 일치 개수: {totalMatches / testCount:F2}개");
        }

        static void GenerateNumbers(Stats stats)
        {
            Console.WriteLine("=== 로또 7 번호 생성 시작 (V2.0) ===");
            var results = new Dictionary<string, List<int>>();

            // 1. 1, 2, 3 조합 (Sum, HighLow, OddEven)
            results["Combo_1_2_3"] = GenerateUntil(stats.GenerateAssociationWeighted, 
                nums => Filters.CheckSum(nums) && Filters.CheckHighLow(nums) && Filters.CheckOddEven(nums));

            // 2. 3, 4 조합 (OddEven, Weighted)
            results["Combo_3_4"] = GenerateUntil(stats.GenerateWeighted, 
                nums => Filters.CheckOddEven(nums));

            // 3. 4, 5, 6 조합 (Weighted, Consecutive, Markov)
            results["Combo_4_5_6"] = GenerateUntil(stats.GenerateMarkovWeightedFirst, 
                nums => Filters.CheckConsecutive(nums));

            // 4. 1, 5, 6 조합 (Sum, Consecutive, Markov)
            results["Combo_1_5_6"] = GenerateUntil(stats.GenerateMarkov, 
                nums => Filters.CheckSum(nums) && Filters.CheckConsecutive(nums));

            // 5. 2, 6 조합 (HighLow, Markov)
            results["Combo_2_6"] = GenerateUntil(stats.GenerateMarkov, 
                nums => Filters.CheckHighLow(nums));

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
            for (int i = 0; i < 150; i++) // 백테스트를 위해 좀 더 넉넉하게
            {
                var set = Enumerable.Range(1, 37).OrderBy(x => rand.Next()).Take(7).OrderBy(x => x).ToArray();
                history.Add(set);
            }
            File.WriteAllText(HistoryFile, JsonSerializer.Serialize(history));
            return history;
        }

        static List<int> GenerateUntil(Func<List<int>> generator, Func<List<int>, bool> filter)
        {
            int maxAttempts = 100000;
            for (int i = 0; i < maxAttempts; i++)
            {
                var nums = generator();
                if (filter(nums)) return nums;
            }
            
            Console.WriteLine("경고: 10만 번 시도했으나 필터 조건을 만족하는 조합을 찾지 못해 기본 난수로 대체합니다.");
            return generator();
        }
    }

    public static class Filters
    {
        public static bool CheckSum(List<int> nums)
        {
            int s = nums.Sum();
            return s >= 100 && s <= 160;
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
        static readonly Random _rand = new Random();
        double[] _weights = new double[38];
        int[,] _pairCounts = new int[38, 38];
        Dictionary<int, List<int>> _markov = new Dictionary<int, List<int>>();

        public Stats(List<int[]> history)
        {
            double[] counts = new double[38];
            for (int i = 0; i < history.Count; i++)
            {
                var set = history[i];
                // 1. Recency Bias: 최신 회차일수록 가중치 증가 (선형 감쇠/증가)
                double recencyWeight = 1.0 + ((double)i / history.Count);

                foreach (var n in set) 
                    counts[n] += recencyWeight;
                
                // 2. Association Rules (연관 규칙): 동시 출현 빈도 추적
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

            double total = counts.Sum();
            for (int i = 1; i <= 37; i++)
            {
                _weights[i] = counts[i] > 0 ? counts[i] / total : 0.01;
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
