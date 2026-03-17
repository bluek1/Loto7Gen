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
            var mlContext = new MLContext();
            const int windowSize = 10;
            if (_history.Count < windowSize)
                return _fallbackStrategy.Predict();

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

            var pipeline = mlContext.Transforms.ApplyOnnxModel(
                outputColumnNames: ["output"],
                inputColumnNames: ["input"],
                modelFile: modelPath);

            var transformedData = pipeline.Fit(dataView).Transform(dataView);
            var probabilities = mlContext.Data
                .CreateEnumerable<OnnxOutput>(transformedData, reuseRowObject: false)
                .First().output;

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
