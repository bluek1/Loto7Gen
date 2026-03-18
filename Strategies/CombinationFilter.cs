using System.Collections.Generic;
using System.Linq;

namespace Loto7Gen.Strategies;

/// <summary>
/// design.md 기반 조합 필터: 총합, 고저비율, 홀짝비율, 연속수
/// </summary>
public static class CombinationFilter
{
    public static bool PassesAll(List<int> nums, FilterConfig config)
    {
        return CheckSumRange(nums, config.SumMin, config.SumMax)
            && CheckHighLowRatio(nums)
            && CheckOddEvenRatio(nums)
            && CheckConsecutive(nums, config.ConsecutiveMin, config.ConsecutiveMax);
    }

    /// <summary>7개 번호의 합이 지정 범위(기본 100~160) 내인지 확인</summary>
    public static bool CheckSumRange(List<int> nums, int min, int max)
    {
        int sum = nums.Sum();
        return sum >= min && sum <= max;
    }

    /// <summary>고저 비율 3:4 또는 4:3 (1~18=저, 19~37=고)</summary>
    public static bool CheckHighLowRatio(List<int> nums)
    {
        int low = nums.Count(n => n <= 18);
        return low == 3 || low == 4;
    }

    /// <summary>홀짝 비율 3:4 또는 4:3</summary>
    public static bool CheckOddEvenRatio(List<int> nums)
    {
        int odd = nums.Count(n => n % 2 != 0);
        return odd == 3 || odd == 4;
    }

    /// <summary>연속 번호 쌍의 수가 지정 범위 내인지 확인</summary>
    public static bool CheckConsecutive(List<int> nums, int minPairs, int maxPairs)
    {
        var sorted = nums.OrderBy(x => x).ToList();
        int pairs = 0;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i] + 1 == sorted[i + 1])
                pairs++;
        }
        return pairs >= minPairs && pairs <= maxPairs;
    }
}
