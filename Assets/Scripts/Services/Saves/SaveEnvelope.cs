using System;
using System.Collections.Generic;

namespace Base.Services.Saves
{
    /// <summary>
    /// То, что уходит в облако: формат конверта и список разделов.
    /// JSON раздела хранится строкой, потому что JsonUtility не умеет поля типа object.
    /// </summary>
    [Serializable]
    internal sealed class SaveEnvelope
    {
        public int format;
        public List<SaveSection> sections = new List<SaveSection>();
    }

    [Serializable]
    internal sealed class SaveSection
    {
        public string key;
        public int version;
        public string json;
    }
}
