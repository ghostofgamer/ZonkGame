using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Tests
{
    /// <summary>Хват стакана: пальцы ложатся на стенку и не входят в стакан, большой палец из стакана выходит.</summary>
    public sealed class HandGripTests
    {
        // Палец модели при SCALE = 4: костяшка над ладонью и впереди, три фаланги вперёд.
        private static readonly Vector2 Knuckle = new Vector2(0.025f, 0.245f);
        private static readonly float[] Bases = { 0f, 0f, 0f };
        private static readonly float[] Lengths = { 0.2f, 0.16f, 0.16f };

        private HandConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<HandConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [TestCase(0.33f)]
        [TestCase(0.38f)]
        [TestCase(0.45f)]
        public void FingerWrapsCupWithoutEnteringIt(float cupRadius)
        {
            var centre = HandGrip.CupCentre(cupRadius, _config);
            var wall = cupRadius + _config.FingerPad;
            var angles = new float[3];
            HandGrip.BendFinger(Knuckle, Bases, Lengths, 0, 3, centre, wall, _config.MaxBend * Mathf.Deg2Rad,
                _config.MaxOpen * Mathf.Deg2Rad, angles);

            var p = Knuckle;
            var total = 0f;
            var bent = 0f;
            for (var i = 0; i < 3; i++)
            {
                Assert.That(angles[i], Is.InRange(-_config.MaxOpen * Mathf.Deg2Rad - 1e-4f, _config.MaxBend * Mathf.Deg2Rad + 1e-4f));
                total += angles[i];
                bent += Mathf.Max(0f, angles[i]);
                var a = Bases[i] - total;
                p += new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Lengths[i];
                // Сустав не глубже толщины пальца в стенке: палец касается, а не проходит насквозь.
                Assert.That((p - centre).magnitude, Is.GreaterThan(wall - _config.FingerPad), "joint " + i);
            }

            // Кончик у стенки, и палец действительно согнулся вокруг стакана.
            Assert.That((p - centre).magnitude, Is.EqualTo(wall).Within(0.03f));
            Assert.That(bent * Mathf.Rad2Deg, Is.GreaterThan(20f));
        }

        [Test]
        public void ThumbInsideCupIsLiftedOut()
        {
            var centre = HandGrip.CupCentre(0.38f, _config);
            var wall = 0.38f + _config.FingerPad;
            // Большой палец свисает ниже ладони — в стакан.
            var start = new Vector2(-0.08f, 0.02f);
            var bases = new[] { -0.6f, -0.6f, -0.5f };
            var lengths = new[] { 0.06f, 0.09f, 0.1f };
            var angles = new float[3];
            HandGrip.BendThumb(start, bases, lengths, 0, 3, centre, wall, _config.ThumbBend * Mathf.Deg2Rad,
                _config.ThumbOpen * Mathf.Deg2Rad, angles);

            Assert.That(angles[0], Is.LessThan(0f));
            Assert.That(angles[1], Is.EqualTo(angles[0]));
        }
    }
}
