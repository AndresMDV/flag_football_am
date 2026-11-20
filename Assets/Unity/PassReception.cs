using System;
using FlagFootball.Core;
using UnityEngine;
using CoreVector = System.Numerics.Vector3;

namespace FlagFootball.Unity
{
    public sealed class PassReception : MonoBehaviour
    {
        private float reach;
        private PassFlight observed;
        private ReceptionTrial trial;
        private readonly System.Random random = new System.Random();
        public ReceptionResult Result => trial?.Result ?? ReceptionResult.Pending;

        internal static PassReception Create(Transform owner, float reach)
        {
            if (!float.IsFinite(reach) || reach <= 0f) throw new ArgumentOutOfRangeException(nameof(reach));
            var root = new GameObject("PassReception");
            root.transform.SetParent(owner, false);
            var reception = root.AddComponent<PassReception>();
            reception.reach = reach;
            return reception;
        }

        internal double? FindContact(PassFlight flight, OneCutRoute route, float height, float seconds, float probability)
        {
            if (!isActiveAndEnabled) return null;
            var duration = Math.Min(seconds, flight.EndTime - flight.Elapsed);
            if (duration <= 0d) return null;
            if (observed != flight)
            {
                observed = flight;
                trial = new ReceptionTrial(probability, reach, random.NextDouble);
            }
            var times = new[] { 0d, Math.Min(duration, route.TimeToCut), Math.Min(duration, route.TimeToStop), duration };
            Array.Sort(times);
            CoreVector ReceiverAt(double time)
            {
                var point = route.SampleAfter(time);
                return new CoreVector(point.X, height, point.Y);
            }
            for (var i = 1; i < times.Length; i++)
            {
                var start = times[i - 1];
                var end = times[i];
                if (end <= start) continue;
                var absolute = flight.Elapsed;
                if (trial.TryCatch(flight.Sample(absolute + start), flight.Sample(absolute + (start + end) * 0.5d),
                    flight.Sample(absolute + end), ReceiverAt(start), ReceiverAt(end), end < flight.EndTime - absolute))
                    return start + trial.ContactFraction.Value * (end - start);
                if (trial.Result != ReceptionResult.Pending) return null;
            }
            return null;
        }

        internal void Clear() { observed = null; trial = null; }
    }
}
