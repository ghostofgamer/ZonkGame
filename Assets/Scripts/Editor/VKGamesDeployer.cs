using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using Base.Platform;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Base.Editor
{
    /// <summary>
    /// Выкладка сборки Игр ВКонтакте на хостинг VK прямо из Unity, без PowerShell.
    ///
    /// Загрузки архива через сайт, как у Яндекса, у VK нет: хостинг принимает файлы только
    /// через npm-пакет @vkontakte/vk-miniapps-deploy. Здесь он запускается в фоне без вопросов
    /// (noprompt) и только для режима разработки: такая выкладка не требует подтверждения на телефоне,
    /// а обновлённую игру сразу видят администраторы, если в «Размещении» стоит галочка «Режим разработки».
    ///
    /// Вход в VK нужен один раз: запустить npx @vkontakte/vk-miniapps-deploy в PowerShell из корня проекта.
    /// Выкладка в прод (перед модерацией) тоже делается так, вручную: она требует кода с телефона.
    /// </summary>
    public static class VKGamesDeployer
    {
        private const string ToolPackage = "@vkontakte/vk-miniapps-deploy@1.0.2";
        private const string RootConfigPath = "vk-hosting-config.json";
        private const string BuildDir = "Builds/VKGames";

        /// <summary>Папка запуска внутри Temp: там же инструмент оставляет build.zip.</summary>
        private const string WorkDir = "Temp/VKGamesDeploy";

        /// <summary>Путь к сборке относительно WorkDir: инструмент читает static_path от своей папки.</summary>
        private const string StaticPathFromWorkDir = "../../" + BuildDir;

        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// После строки с адресами инструмент может продолжать опрашивать очередь VK в ожидании прода,
        /// которого при dev-выкладке не будет. Даём ему немного времени и завершаем сами.
        /// </summary>
        private static readonly TimeSpan GraceAfterUrls = TimeSpan.FromSeconds(5);

        private static readonly ConcurrentQueue<string> Output = new ConcurrentQueue<string>();
        private static readonly StringBuilder DevUrls = new StringBuilder();

        private static Process _process;
        private static DateTime _startedAt;
        private static DateTime? _urlsReceivedAt;
        private static bool _needsLogin;
        private static int _appId;

        [Serializable]
        private sealed class HostingConfig
        {
            public int app_id;
        }

        [MenuItem("Base/Deploy/VK Games (dev)", priority = 0)]
        public static void DeployDev()
        {
            if (_process != null)
            {
                Debug.LogWarning("[VKDeploy] Deploy is already running");
                return;
            }

            if (!File.Exists(Path.Combine(BuildDir, "index.html")))
            {
                EditorUtility.DisplayDialog("VK deploy", $"Нет сборки в {BuildDir}. Сначала Base/Build/VK Games (WebGL).", "OK");
                return;
            }

            _appId = ReadAppId();
            if (_appId == 0)
            {
                EditorUtility.DisplayDialog("VK deploy", $"В {RootConfigPath} не указан app_id.", "OK");
                return;
            }

            Directory.CreateDirectory(WorkDir);
            File.WriteAllText(Path.Combine(WorkDir, "vk-hosting-config.json"),
                "{\n" +
                $"  \"static_path\": \"{StaticPathFromWorkDir}\",\n" +
                $"  \"app_id\": {_appId},\n" +
                "  \"endpoints\": { \"mobile\": \"index.html\", \"web\": \"index.html\", \"mvk\": \"index.html\" },\n" +
                "  \"noprompt\": 1,\n" +
                "  \"update_dev\": 1,\n" +
                "  \"update_prod\": 0\n" +
                "}\n");

            var startInfo = new ProcessStartInfo("cmd.exe", $"/c npx --yes {ToolPackage}")
            {
                WorkingDirectory = Path.GetFullPath(WorkDir),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // MINI_APPS_ENVIRONMENT=dev не ставить: с ним VK отвечает "15: Access denied: invalid file"
            // (проверено 11.09.2026). Только dev обновляется через update_dev/update_prod в конфиге.
            startInfo.EnvironmentVariables["NO_COLOR"] = "1";
            startInfo.EnvironmentVariables["FORCE_COLOR"] = "0";

            DevUrls.Clear();
            _needsLogin = false;
            _urlsReceivedAt = null;
            while (Output.TryDequeue(out _)) { }

            try
            {
                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) => { if (e.Data != null) Output.Enqueue(e.Data); };
                _process.ErrorDataReceived += (_, e) => { if (e.Data != null) Output.Enqueue(e.Data); };
                _process.Start();

                // Вопросов в noprompt быть не должно. Если инструмент всё же спросит (например, вход в VK),
                // закрытый ввод заставит его завершиться, а не висеть в фоне.
                _process.StandardInput.Close();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                _process = null;
                Debug.LogError($"[VKDeploy] Cannot start npx: {e.Message}. Is Node.js installed?");
                return;
            }

            _startedAt = DateTime.Now;
            EditorApplication.update += Poll;
            Debug.Log($"[VKDeploy] Uploading {BuildDir} to VK hosting (dev) for app {_appId}...");
        }

        [MenuItem("Base/Build/VK Games + Deploy (dev)", priority = 20)]
        public static void BuildAndDeployDev()
        {
            if (BuildScript.TryBuild(PlatformId.VKGames))
                DeployDev();
        }

        private static int ReadAppId()
        {
            try
            {
                return File.Exists(RootConfigPath)
                    ? JsonUtility.FromJson<HostingConfig>(File.ReadAllText(RootConfigPath)).app_id
                    : 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VKDeploy] Cannot read {RootConfigPath}: {e.Message}");
                return 0;
            }
        }

        private static void Poll()
        {
            while (Output.TryDequeue(out var line))
                HandleLine(line);

            if (_process == null)
                return;

            if (!_process.HasExited)
            {
                if (_urlsReceivedAt.HasValue && DateTime.Now - _urlsReceivedAt.Value > GraceAfterUrls)
                {
                    Stop();
                    Finish(true);
                }
                else if (DateTime.Now - _startedAt > Timeout)
                {
                    Stop();
                    Debug.LogError("[VKDeploy] Timeout");
                    Finish(false);
                }

                return;
            }

            // Дочитываем вывод, который мог прийти одновременно с завершением.
            _process.WaitForExit();
            while (Output.TryDequeue(out var line))
                HandleLine(line);

            // Адреса печатаются только после успешной загрузки, код выхода при этом бывает и ненулевым.
            Finish(DevUrls.Length > 0);
        }

        private static void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            // Инструмент печатает токен доступа к аккаунту VK "для CI". В лог Unity он попадать не должен.
            if (line.Contains("MINI_APPS_ACCESS_TOKEN="))
                line = "[token hidden]";

            if (line.Contains("Please open this url"))
                _needsLogin = true;

            if (line.Contains("pages.vk-apps.ru"))
            {
                DevUrls.AppendLine(line.Trim());
                if (!_urlsReceivedAt.HasValue)
                    _urlsReceivedAt = DateTime.Now;
            }

            Debug.Log("[VKDeploy] " + line);
        }

        private static void Stop()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VKDeploy] Cannot stop deploy process: {e.Message}");
            }
        }

        private static void Finish(bool success)
        {
            EditorApplication.update -= Poll;
            _process.Dispose();
            _process = null;

            var gameUrl = $"https://vk.com/app{_appId}";

            if (success)
            {
                Debug.Log($"[VKDeploy] Done. Dev URLs:\n{DevUrls}");
                if (EditorUtility.DisplayDialog("VK deploy",
                        "Сборка выложена в режим разработки.\n\n" +
                        "Игра откроется у администраторов, если в «Размещении» включён «Режим разработки».",
                        "Открыть игру", "Закрыть"))
                {
                    Application.OpenURL(gameUrl);
                }

                return;
            }

            if (_needsLogin)
            {
                EditorUtility.DisplayDialog("VK deploy",
                    "Нужно войти в VK. Один раз запустите в PowerShell:\n\n" +
                    "cd " + Path.GetFullPath(".") + "\n" +
                    "npx @vkontakte/vk-miniapps-deploy\n\n" +
                    "и откройте ссылку, которую он напечатает. Потом выкладка из Unity снова заработает.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog("VK deploy", "Выкладка не удалась, подробности в Console (строки [VKDeploy]).", "OK");
        }
    }
}
