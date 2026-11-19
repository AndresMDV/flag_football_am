using System;
using System.Collections.Generic;
using System.Numerics;

namespace FlagFootball.Core
{
    public enum ReceptionResult { Pending, Missed, Caught }

    public sealed class ReceptionTrial
    {
        private readonly double probability, radiusSquared;
        private readonly Func<double> draw;
        private const double TimeTolerance = 1e-10;
        public ReceptionResult Result { get; private set; }
        public double? ContactFraction { get; private set; }

        public ReceptionTrial(float probability, float reach, Func<double> draw)
        {
            if (!float.IsFinite(probability) || probability < 0f || probability > 1f)
                throw new ArgumentOutOfRangeException(nameof(probability));
            if (!float.IsFinite(reach) || reach <= 0f)
                throw new ArgumentOutOfRangeException(nameof(reach));
            this.probability = probability;
            radiusSquared = (double)reach * reach;
            this.draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        // One quadratic ball segment and one linear receiver leg. Split cuts/stops in the caller.
        // Excluding the end gives a terminal flight event precedence within TimeTolerance.
        public bool TryCatch(Vector3 ballStart, Vector3 ballMiddle, Vector3 ballEnd,
            Vector3 receiverStart, Vector3 receiverEnd, bool includeEnd = true)
        {
            if (Result != ReceptionResult.Pending) return false;
            foreach (var point in new[] { ballStart, ballMiddle, ballEnd, receiverStart, receiverEnd })
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                    throw new ArgumentOutOfRangeException(nameof(ballStart), "Use finite segment positions.");
            // Squared relative distance minus reach² is quartic in normalized interval time.
            var polynomial = new[] { -radiusSquared, 0d, 0d, 0d, 0d };
            void Axis(double start, double middle, double end, double receiver0, double receiver1)
            {
                var a = 2d * (end + start - 2d * middle);
                var b = end - start - a - (receiver1 - receiver0);
                var c = start - receiver0;
                polynomial[0] += c * c;
                polynomial[1] += 2d * c * b;
                polynomial[2] += b * b + 2d * c * a;
                polynomial[3] += 2d * a * b;
                polynomial[4] += a * a;
            }
            Axis(ballStart.X, ballMiddle.X, ballEnd.X, receiverStart.X, receiverEnd.X);
            Axis(ballStart.Y, ballMiddle.Y, ballEnd.Y, receiverStart.Y, receiverEnd.Y);
            Axis(ballStart.Z, ballMiddle.Z, ballEnd.Z, receiverStart.Z, receiverEnd.Z);
            double? contact = polynomial[0] <= 0d ? 0d : null;
            if (!contact.HasValue)
                foreach (var time in Roots(polynomial))
                    if (includeEnd || time < 1d - TimeTolerance) { contact = time; break; }
            if (!contact.HasValue) return false;
            ContactFraction = contact;
            Result = ReceptionResult.Missed; // Fail closed even if the draw provider throws.
            if (probability == 1d) Result = ReceptionResult.Caught;
            else if (probability > 0d)
            {
                var value = draw();
                if (!double.IsFinite(value) || value < 0d || value >= 1d)
                    throw new InvalidOperationException("Reception draws must be uniform values in [0, 1).");
                if (value < probability) Result = ReceptionResult.Caught;
            }
            return Result == ReceptionResult.Caught;
        }

        // Derivative roots partition a polynomial into monotone intervals, including tangencies.
        private static List<double> Roots(double[] polynomial)
        {
            var degree = polynomial.Length - 1;
            while (degree > 0 && polynomial[degree] == 0d) degree--;
            var roots = new List<double>();
            if (degree == 0) return roots;
            var derivative = new double[degree];
            var scale = Math.Abs(polynomial[0]);
            for (var i = 1; i <= degree; i++)
            {
                derivative[i - 1] = i * polynomial[i];
                scale += Math.Abs(polynomial[i]);
            }
            var points = Roots(derivative);
            points.Insert(0, 0d);
            points.Add(1d);
            var tolerance = scale * 1e-12;
            var previous = 0d;
            var previousValue = Evaluate(polynomial, degree, previous);
            foreach (var point in points)
            {
                var value = Evaluate(polynomial, degree, point);
                if (previousValue * value < 0d)
                {
                    var low = previous;
                    var high = point;
                    var lowValue = previousValue;
                    for (var iteration = 0; iteration < 64; iteration++)
                    {
                        var middle = (low + high) * 0.5d;
                        var middleValue = Evaluate(polynomial, degree, middle);
                        if (middleValue == 0d) { low = high = middle; break; }
                        if (lowValue * middleValue > 0d) { low = middle; lowValue = middleValue; }
                        else high = middle;
                    }
                    roots.Add((low + high) * 0.5d);
                }
                if (Math.Abs(value) <= tolerance) roots.Add(point);
                previous = point;
                previousValue = value;
            }
            return roots;
        }

        private static double Evaluate(double[] polynomial, int degree, double time)
        {
            var value = polynomial[degree];
            for (var i = degree - 1; i >= 0; i--) value = value * time + polynomial[i];
            return value;
        }
    }
}
