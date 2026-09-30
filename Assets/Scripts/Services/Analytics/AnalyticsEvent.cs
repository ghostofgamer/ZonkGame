using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Base.Platform;

namespace Base.Services.Analytics
{
    /// <summary>
    /// Событие по папкам (см. IAnalytics). Path задаёт вложенные папки от корня события, Param добавляет строку
    /// со значением в последнюю заданную папку. Path можно вызывать несколько раз — ветки собираются в одно дерево.
    /// Если после Path не добавлено ни одного Param, последняя папка становится значением предпоследней:
    /// Path("step", "ChooseDice") → {"step": "ChooseDice"} (так удобнее считать, кто на каком шаге).
    /// Выделяет память (дерево, строка JSON): вызывать на редких событиях, не каждый кадр.
    /// </summary>
    public sealed class AnalyticsEvent
    {
        /// <summary>Наибольшая вложенность JSON в AppMetrica.</summary>
        public const int MaxDepth = 5;

        private sealed class Node
        {
            public readonly List<KeyValuePair<string, object>> Children = new List<KeyValuePair<string, object>>();
        }

        private readonly IAnalyticsService _service;
        private readonly string _name;
        private readonly Node _root = new Node();
        private readonly List<string> _path = new List<string>();
        private bool _pathUsed = true;

        internal AnalyticsEvent(IAnalyticsService service, string name)
        {
            _service = service;
            _name = name;
        }

        /// <summary>Вложенные папки от корня события: Path("win", "chapter_1", "semenych").</summary>
        public AnalyticsEvent Path(params string[] levels)
        {
            FlushPath();
            _path.Clear();
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level) && _path.Count < MaxDepth)
                        _path.Add(level);
                }
            }

            _pathUsed = _path.Count == 0;
            return this;
        }

        public AnalyticsEvent Param(string key, string value) => Add(key, value ?? string.Empty);
        public AnalyticsEvent Param(string key, int value) => Add(key, value);
        public AnalyticsEvent Param(string key, long value) => Add(key, value);
        public AnalyticsEvent Param(string key, double value) => Add(key, value);
        public AnalyticsEvent Param(string key, bool value) => Add(key, value);

        /// <summary>Отправить событие.</summary>
        public void Send()
        {
            FlushPath();
            _service?.ReportEvent(_name, _root.Children.Count > 0 ? ToJson() : null);
        }

        private AnalyticsEvent Add(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                return this;

            // Строка со значением — тоже уровень: папок перед ней не больше MaxDepth - 1.
            var node = _root;
            for (var i = 0; i < _path.Count && i < MaxDepth - 1; i++)
                node = Child(node, _path[i]);

            node.Children.Add(new KeyValuePair<string, object>(key, value));
            _pathUsed = true;
            return this;
        }

        /// <summary>Путь без строк: последняя папка становится значением предпоследней.</summary>
        private void FlushPath()
        {
            if (_pathUsed || _path.Count == 0)
                return;

            _pathUsed = true;
            var last = _path.Count - 1;
            if (last == 0)
            {
                _root.Children.Add(new KeyValuePair<string, object>(_path[0], string.Empty));
                return;
            }

            var node = _root;
            for (var i = 0; i < last - 1; i++)
                node = Child(node, _path[i]);

            // Если предпоследняя уже папка (в другой ветке есть строки) — значение кладётся внутрь неё.
            var existing = Find(node, _path[last - 1]);
            if (existing != null)
                existing.Children.Add(new KeyValuePair<string, object>(_path[last], string.Empty));
            else
                node.Children.Add(new KeyValuePair<string, object>(_path[last - 1], _path[last]));
        }

        private static Node Find(Node parent, string key)
        {
            foreach (var pair in parent.Children)
            {
                if (pair.Key == key && pair.Value is Node node)
                    return node;
            }

            return null;
        }

        private static Node Child(Node parent, string key)
        {
            var found = Find(parent, key);
            if (found != null)
                return found;

            var child = new Node();
            parent.Children.Add(new KeyValuePair<string, object>(key, child));
            return child;
        }

        private string ToJson()
        {
            var builder = new StringBuilder(128);
            Write(builder, _root);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, Node node)
        {
            builder.Append('{');
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');

                var pair = node.Children[i];
                WriteString(builder, pair.Key);
                builder.Append(':');
                switch (pair.Value)
                {
                    case Node child:
                        Write(builder, child);
                        break;
                    case string text:
                        WriteString(builder, text);
                        break;
                    case bool flag:
                        builder.Append(flag ? "true" : "false");
                        break;
                    case int number:
                        builder.Append(number.ToString(CultureInfo.InvariantCulture));
                        break;
                    case long number:
                        builder.Append(number.ToString(CultureInfo.InvariantCulture));
                        break;
                    case double number:
                        builder.Append(double.IsNaN(number) || double.IsInfinity(number)
                            ? "0"
                            : number.ToString("R", CultureInfo.InvariantCulture));
                        break;
                    default:
                        builder.Append("null");
                        break;
                }
            }

            builder.Append('}');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
