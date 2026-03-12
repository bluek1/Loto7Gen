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
                Console.WriteLine("사용법: Loto7Gen generate | verify 1 2 3 4 5 6 7");
                return;
            }

            var history = LoadOrGenerateHistory();
            var stats = new Stats(history);

            if (args[0] == "generate")
            {
                GenerateNumbers(stats);
            }
            else if (args[0] == "verify" && args.Length == 8)
            {
                var winning = args.Skip(1).Select(int.Parse).OrderBy(x => x).ToList();
                VerifyNumbers(winning);
            }
            else
            {
                Console.WriteLine("잘못된 명령입니다.");
            }
        }

        static void GenerateNumbers(Stats stats)
        {
            Console.WriteLine("=== 로또 7 번호 생성 시작 ===");
            var results = new Dictionary<string, List<int>>();

            // 1. 1, 2, 3 조합 (Sum, HighLow, OddEven)
            results["Combo_1_2_3"] = GenerateUntil(stats.GenerateRandom, 
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

            Console.WriteLine("\n[향후 보정 제안]");
            Console.WriteLine("1. 당첨 번호의 총합이 100~160 범위를 벗어났는지 확인 (필터 1 조정 필요 여부)");
            Console.WriteLine("2. 당첨 번호에 연속수가 너무 많거나 없는지 확인 (필터 5 조정 필요 여부)");
            Console.WriteLine("3. 이번 당첨번호를 history.json에 추가하여 가중치(4)와 마르코프 전이행렬(6)을 업데이트해야 합니다.");
        }

        static List<int[]> LoadOrGenerateHistory()
        {
            if (File.Exists(HistoryFile))
            {
                return JsonSerializer.Deserialize<List<int[]>>(File.ReadAllText(HistoryFile));
            }
            
            Console.WriteLine("과거 100주치 가상 데이터를 생성합니다...");
            var rand = new Random();
            var history = new List<int[]>();
            for (int i = 0; i < 100; i++)
            {
                var set = Enumerable.Range(1, 37).OrderBy(x => rand.Next()).Take(7).OrderBy(x => x).ToArray();
                history.Add(set);
            }
            File.WriteAllText(HistoryFile, JsonSerializer.Serialize(history));
            return history;
        }

        static List<int> GenerateUntil(Func<List<int>> generator, Func<List<int>, bool> filter)
        {
            while (true)
            {
                var nums = generator();
                if (filter(nums)) return nums;
            }
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
        Random _rand = new Random();
        double[] _weights = new double[38];
        Dictionary<int, List<int>> _markov = new Dictionary<int, List<int>>();

        public Stats(List<int[]> history)
        {
            // Calculate frequencies
            int[] counts = new int[38];
            foreach (var set in history)
            {
                foreach (var n in set) counts[n]++;
                
                // Build Markov transitions
                for (int i = 0; i < set.Length - 1; i++)
                {
                    if (!_markov.ContainsKey(set[i])) _markov[set[i]] = new List<int>();
                    _markov[set[i]].Add(set[i + 1]);
                }
            }

            int total = counts.Sum();
            for (int i = 1; i <= 37; i++)
            {
                _weights[i] = counts[i] > 0 ? (double)counts[i] / total : 0.01;
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
                    
                    // Prevent duplicates or going backwards (since lotto is sorted)
                    if (nums.Contains(next) || next <= current) 
                    {
                        next = Enumerable.Range(current + 1, 37 - current).OrderBy(x => _rand.Next()).FirstOrDefault();
                        if (next == 0) next = GenerateRandom()[0]; // reset if stuck
                    }
                    
                    nums.Add(next);
                    current = next;
                }
                else
                {
                    // Fallback
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