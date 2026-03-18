using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Onnx;

namespace Loto7Gen.Strategies;

public class LstmPredictionStrategy(List<int[]> history, IPredictionStrategy fallbackStrategy) : IPredictionStrategy
{
    private readonly List<int[]> _history = history;
    private readonly IPredictionStrategy _fallbackStrategy = fallbackStrategy;

    // 백테스트 등 반복 호출 시 매번 모델을 로딩하지 않도록 캐싱
    private static ITransformer? _cachedTransformer;
    private static MLContext? _cachedMlContext;
    private static string? _cachedModelPath;
    private static readonly object _modelLock = new();

    public string Key => "lstm";
    public string DisplayName => "LSTM";

    public List<int> Predict()
    {
        const string modelPath = "loto7_lstm.onnx";
        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Warning: LSTM 모델 파일이 없습니다. 랜덤으로 대체합니다.");
            return _fallbackStrategy.Predict();
        }

        try
        {
            const int windowSize = 10;
            if (_history.Count < windowSize)
                return _fallbackStrategy.Predict();

            var (mlContext, transformer) = GetOrCreateTransformer(modelPath);

            var lastWindow = _history.Skip(_history.Count - windowSize).ToList();
            var inputFeatures = new float[1 * 10 * 40];

            for (int i = 0; i < windowSize; i++)
            {
                var draw = lastWindow[i];
                var features = PredictionFeatureHelper.ExtractFeatures(draw);
                Array.Copy(features, 0, inputFeatures, i * 40, 40);
            }

            var dataView = mlContext.Data.LoadFromEnumerable(new List<OnnxInput>
            {
                new() { input = inputFeatures }
            });

            var transformedData = transformer.Transform(dataView);
            var probabilities = mlContext.Data
                .CreateEnumerable<OnnxOutput>(transformedData, reuseRowObject: false)
                .First().output;

            // 모델 출력 배열 길이 검증
            if (probabilities == null || probabilities.Length < 37)
            {
                Console.WriteLine("Warning: LSTM 모델 출력이 유효하지 않습니다. 랜덤으로 대체합니다.");
                return _fallbackStrategy.Predict();
            }

            return Enumerable.Range(1, 37)
                .OrderByDescending(i => probabilities[i - 1])
                .Take(7)
                .OrderBy(x => x)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LSTM Error: {ex.Message}");
            return _fallbackStrategy.Predict();
        }
    }

    private static (MLContext mlContext, ITransformer transformer) GetOrCreateTransformer(string modelPath)
    {
        if (_cachedTransformer != null && _cachedMlContext != null && _cachedModelPath == modelPath)
            return (_cachedMlContext, _cachedTransformer);

        lock (_modelLock)
        {
            if (_cachedTransformer == null || _cachedModelPath != modelPath)
            {
                var mlContext = new MLContext();
                var dummyData = mlContext.Data.LoadFromEnumerable(new List<OnnxInput>
                {
                    new() { input = new float[1 * 10 * 40] }
                });
                var pipeline = mlContext.Transforms.ApplyOnnxModel(
                    outputColumnNames: ["output"],
                    inputColumnNames: ["input"],
                    modelFile: modelPath);
                _cachedMlContext = mlContext;
                _cachedTransformer = pipeline.Fit(dummyData);
                _cachedModelPath = modelPath;
            }
            return (_cachedMlContext!, _cachedTransformer!);
        }
    }

    public class OnnxInput
    {
        [VectorType(1, 10, 40)]
        public float[] input { get; set; } = [];
    }

    public class OnnxOutput
    {
        [ColumnName("output")]
        public float[] output { get; set; } = [];
    }
}
