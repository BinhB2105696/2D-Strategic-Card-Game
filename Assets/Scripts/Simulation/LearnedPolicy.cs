using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public sealed class LearnedPolicy
{
    private readonly LearnedPolicyEncoder encoder;
    private readonly Layer[] layers;
    public string Sha256 { get; }

    private LearnedPolicy(LearnedPolicyEncoder encoder, Layer[] layers, string sha256)
    {
        this.encoder = encoder;
        this.layers = layers;
        Sha256 = sha256;
    }

    public static LearnedPolicy Load(string path, int rulesVersion, int catalogVersion, int aiSchema)
    {
        using (var stream = File.OpenRead(path))
        using (var reader = new BinaryReader(stream, Encoding.UTF8))
        using (var sha = SHA256.Create())
        {
            if (stream.Length > 16 * 1024 * 1024) throw new InvalidDataException("Learned policy is too large.");
            string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            stream.Position = 0;
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "LCBP" || reader.ReadInt32() != 1 ||
                reader.ReadInt32() != rulesVersion || reader.ReadInt32() != catalogVersion ||
                reader.ReadInt32() != aiSchema)
                throw new InvalidDataException("Incompatible learned policy artifact.");

            int encoderVersion = reader.ReadInt32();
            if (encoderVersion != LearnedPolicyEncoder.Version &&
                (encoderVersion != LearnedPolicyEncoder.FundamentalsVersion || aiSchema != 3))
                throw new InvalidDataException("Incompatible learned policy encoder.");
            int globals = ReadSize(reader, 128);
            var cardIds = new string[ReadSize(reader, 512)];
            for (int i = 0; i < cardIds.Length; i++)
            {
                int size = ReadSize(reader, 256);
                byte[] bytes = reader.ReadBytes(size);
                if (bytes.Length != size) throw new EndOfStreamException();
                cardIds[i] = Encoding.UTF8.GetString(bytes);
            }
            var encoder = new LearnedPolicyEncoder(aiSchema, globals, cardIds, ReadCategories(reader), ReadCategories(reader), encoderVersion);
            var layers = new Layer[ReadSize(reader, 8)];
            int input = encoder.ContextSize + encoder.ActionSize;
            for (int i = 0; i < layers.Length; i++)
            {
                if (reader.ReadInt32() != input) throw new InvalidDataException("Invalid learned policy layer input width.");
                int output = ReadSize(reader, 512);
                layers[i] = new Layer(input, ReadWeights(reader, input * output), ReadWeights(reader, output));
                input = output;
            }
            if (input != 1 || stream.Position != stream.Length)
                throw new InvalidDataException("Invalid learned policy output or trailing data.");
            return new LearnedPolicy(encoder, layers, hash);
        }
    }

    public float[] Score(Dictionary<string, object> policy)
    {
        encoder.Encode(policy, out double[] context, out double[][] actions);
        var logits = new float[actions.Length];
        var shared = new double[layers[0].Output];
        var contextValues = Array.ConvertAll(context, value => (float)value);
        for (int row = 0; row < shared.Length; row++)
            shared[row] = layers[0].Bias[row] + layers[0].Dot(row, 0, contextValues);

        for (int i = 0; i < actions.Length; i++)
        {
            var actionValues = Array.ConvertAll(actions[i], value => (float)value);
            var values = new float[shared.Length];
            for (int row = 0; row < shared.Length; row++)
            {
                float value = (float)(shared[row] + layers[0].Dot(row, context.Length, actionValues));
                values[row] = layers.Length == 1 ? value : Math.Max(0, value);
            }
            for (int layer = 1; layer < layers.Length; layer++)
                values = layers[layer].Apply(values, layer < layers.Length - 1);
            logits[i] = values[0];
            if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i]))
                throw new InvalidOperationException("Non-finite learned policy score.");
        }
        return logits;
    }

    public static int SelectedIndex(IReadOnlyList<float> logits)
    {
        if (logits.Count == 0) throw new ArgumentException("No legal actions to score.", nameof(logits));
        int best = 0;
        for (int i = 1; i < logits.Count; i++)
            if (logits[i] > logits[best]) best = i;
        return best;
    }

    private static int ReadSize(BinaryReader reader, int max)
    {
        int value = reader.ReadInt32();
        if (value < 1 || value > max) throw new InvalidDataException("Invalid learned policy dimension.");
        return value;
    }

    private static int[] ReadCategories(BinaryReader reader)
    {
        var values = new int[ReadSize(reader, 128)];
        for (int i = 0; i < values.Length; i++) values[i] = reader.ReadInt32();
        if (values.Distinct().Count() != values.Length) throw new InvalidDataException("Duplicate learned policy categories.");
        return values;
    }

    private static float[] ReadWeights(BinaryReader reader, int count)
    {
        if (count * sizeof(float) > reader.BaseStream.Length - reader.BaseStream.Position) throw new EndOfStreamException();
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = reader.ReadSingle();
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                throw new InvalidDataException("Non-finite learned policy weights.");
        }
        return values;
    }

    private sealed class Layer
    {
        private readonly int input;
        private readonly float[] weights;
        internal readonly float[] Bias;
        internal int Output => Bias.Length;

        internal Layer(int input, float[] weights, float[] bias)
        {
            this.input = input;
            this.weights = weights;
            Bias = bias;
        }

        internal double Dot(int row, int start, float[] values)
        {
            double sum = 0;
            int index = row * input + start;
            for (int i = 0; i < values.Length; i++) sum += (double)weights[index + i] * values[i];
            return sum;
        }

        internal float[] Apply(float[] inputValues, bool relu)
        {
            var output = new float[Output];
            for (int row = 0; row < output.Length; row++)
            {
                float value = (float)(Bias[row] + Dot(row, 0, inputValues));
                output[row] = relu ? Math.Max(0, value) : value;
            }
            return output;
        }
    }
}
