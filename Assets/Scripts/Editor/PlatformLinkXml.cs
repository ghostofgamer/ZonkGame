using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Base.Editor
{
    /// <summary>
    /// Дополнительные правила стриппинга только для сборки конкретной платформы.
    /// Общий Assets/link.xml действует на все сборки, а защита SDK одной площадки
    /// (например, Яндекс Рекламы для RuStore) не должна тащить его код в WebGL.
    /// </summary>
    public sealed class PlatformLinkXml : IUnityLinkerProcessor
    {
        private const string AndroidLinkXml = PlatformTargets.PlatformRoot + "/RuStore/RuStoreLinker.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            if (report.summary.platform != BuildTarget.Android)
                return null;

            return Path.GetFullPath(AndroidLinkXml);
        }
    }
}
