using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

var model = LearnedPolicy.Load(args[0], 6, 4, 3);
using var document = JsonDocument.Parse(File.ReadAllBytes(args[1]));
int decisions = 0, scores = 0;
double maxError = 0;
foreach (var fixture in document.RootElement.EnumerateArray())
{
    var logits = model.Score(ReadRecord(fixture.GetProperty("policy")));
    var expected = fixture.GetProperty("logits").EnumerateArray().Select(value => value.GetDouble()).ToArray();
    if (logits.Length != expected.Length) throw new Exception($"Fixture {decisions}: logit count differs.");
    for (int i = 0; i < logits.Length; i++)
    {
        double error = Math.Abs(logits[i] - expected[i]);
        if (error > 0.00005 + 0.00001 * Math.Abs(expected[i]))
            throw new Exception($"Fixture {decisions}, logit {i}: Python {expected[i]:R}, C# {logits[i]:R}.");
        maxError = Math.Max(maxError, error);
    }
    if (LearnedPolicy.SelectedIndex(logits) != fixture.GetProperty("selected").GetInt32())
        throw new Exception($"Fixture {decisions}: selected legal index differs.");
    decisions++;
    scores += expected.Length;
}
if (decisions == 0 || LearnedPolicy.SelectedIndex(new[] { -1f, -1f }) != 0)
    throw new Exception("Missing fixtures or incorrect first-index tie handling.");
var sharp = new[] { -100f, 100f };
if (LearnedPolicySampling.Select(sharp, 0.01, out double rareLogProbability) != 0 ||
    Math.Abs(Math.Exp(rareLogProbability) - 0.025) > 1e-12 ||
    LearnedPolicySampling.Select(sharp, 0.5, out double commonLogProbability) != 1 ||
    Math.Abs(Math.Exp(commonLogProbability) - 0.975) > 1e-12)
    throw new Exception("Unexpected exploration distribution.");
var near = new[] { 0f, 0.5f };
double lowExpected = 0.98 / (1 + Math.Exp(-0.5 / 0.35)) + 0.01;
if (LearnedPolicySampling.Select(near, 0.25, out _) != 0 ||
    LearnedPolicySampling.Select(near, 0.25, out double lowActual,
        LearnedPolicySampling.LowTemperature, LearnedPolicySampling.LowUniformMix) != 1 ||
    Math.Abs(Math.Exp(lowActual) - lowExpected) > 1e-12)
    throw new Exception("Unexpected low-exploration distribution.");
Console.WriteLine("PASS exploration probability and selection.");
Console.WriteLine($"PASS {decisions} selected indices, {scores} logits, maximum absolute error {maxError:G6}.");

static Dictionary<string, object> ReadRecord(JsonElement record)
{
    var result = new Dictionary<string, object>();
    foreach (var property in record.EnumerateObject())
    {
        var value = property.Value;
        result.Add(property.Name, property.Name switch
        {
            "entities" or "legalActions" or "retaliationMarks" => value.EnumerateArray().Select(ReadRecord).ToArray(),
            "globalFeatures" or "features" => value.EnumerateArray().Select(number => number.GetDouble()).ToArray(),
            "targets" or "tributes" or "legalActionMask" => value.EnumerateArray().Select(number => number.GetInt32()).ToArray(),
            "cardId" => value.GetString(),
            _ => value.GetInt32()
        });
    }
    return result;
}
