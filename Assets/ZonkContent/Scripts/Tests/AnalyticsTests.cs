using Base.Platform;
using Base.Services.Analytics;
using NUnit.Framework;

namespace Zonk.Tests
{
    /// <summary>События по папкам: какой JSON уходит в AppMetrica.</summary>
    public sealed class AnalyticsTests
    {
        private sealed class Recorder : IAnalyticsService
        {
            public string Name;
            public string Json;

            public bool IsAvailable => true;

            public void ReportEvent(string name, string parametersJson)
            {
                Name = name;
                Json = parametersJson;
            }

            public void SetUserProperty(string key, string value)
            {
            }

            public void SetUserProperty(string key, double value)
            {
            }

            public void Flush()
            {
            }
        }

        private Recorder _recorder;
        private Analytics _analytics;

        [SetUp]
        public void SetUp()
        {
            _recorder = new Recorder();
            _analytics = new Analytics(_recorder);
        }

        [Test]
        public void Track_LastLevelBecomesValue()
        {
            _analytics.Track("tutorial", "step", "ChooseDice");
            Assert.AreEqual("tutorial", _recorder.Name);
            Assert.AreEqual("{\"step\":\"ChooseDice\"}", _recorder.Json);
        }

        [Test]
        public void PathWithParams_NestsFolders()
        {
            _analytics.Event("campaign").Path("win", "chapter_1", "semenych").Param("turns", 9).Param("boss", false).Send();
            Assert.AreEqual("{\"win\":{\"chapter_1\":{\"semenych\":{\"turns\":9,\"boss\":false}}}}", _recorder.Json);
        }

        [Test]
        public void SeveralPaths_MergeIntoOneTree()
        {
            _analytics.Event("ads").Path("rewarded", "double_reward").Param("result", "completed")
                .Path("rewarded", "energy").Param("result", "closed").Send();
            Assert.AreEqual(
                "{\"rewarded\":{\"double_reward\":{\"result\":\"completed\"},\"energy\":{\"result\":\"closed\"}}}",
                _recorder.Json);
        }

        [Test]
        public void TooDeep_IsCutToFiveLevels()
        {
            _analytics.Event("deep").Path("a", "b", "c", "d", "e", "f").Param("x", 1).Send();
            Assert.AreEqual("{\"a\":{\"b\":{\"c\":{\"d\":{\"x\":1}}}}}", _recorder.Json);
        }

        [Test]
        public void NoParams_SendsEventWithoutJson()
        {
            _analytics.Event("session").Send();
            Assert.AreEqual("session", _recorder.Name);
            Assert.IsNull(_recorder.Json);
        }

        [Test]
        public void Strings_AreEscaped()
        {
            _analytics.Event("e").Param("name", "a\"b\\c").Param("rate", 0.5).Send();
            Assert.AreEqual("{\"name\":\"a\\\"b\\\\c\",\"rate\":0.5}", _recorder.Json);
        }
    }
}
