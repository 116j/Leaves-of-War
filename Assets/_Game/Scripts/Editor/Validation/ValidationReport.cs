using System;
using System.Collections;
using System.Collections.Generic;

namespace Hortensia.Editor.Validation
{
    internal readonly struct ValidationDiagnostic
    {
        public ValidationDiagnostic(int order, string code, string message)
        {
            Order = order;
            Code = string.IsNullOrWhiteSpace(code) ? "validation" : code;
            Message = message ?? string.Empty;
        }

        public int Order { get; }
        public string Code { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Ordered validation diagnostics with one shared formatting path. The
    /// ICollection implementation lets existing validation rules add messages
    /// without coupling them to editor UI or command-line presentation.
    /// </summary>
    internal sealed class ValidationReport : ICollection<string>
    {
        private readonly List<ValidationDiagnostic> diagnostics =
            new List<ValidationDiagnostic>();

        public int Count => diagnostics.Count;
        public bool IsReadOnly => false;
        public IReadOnlyList<ValidationDiagnostic> Diagnostics => diagnostics;

        public void Add(string message)
        {
            Add("validation", message);
        }

        public void Add(string code, string message)
        {
            diagnostics.Add(new ValidationDiagnostic(diagnostics.Count, code, message));
        }

        public ICollection<string> WithPrefix(string prefix)
        {
            return new PrefixedCollection(this, prefix ?? string.Empty);
        }

        public bool TryFormat(string header, bool useBullets, out string error)
        {
            if (diagnostics.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            string separator = useBullets ? "\n- " : "\n";
            var messages = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                messages[i] = diagnostics[i].Message;

            error = header + separator + string.Join(separator, messages);
            return false;
        }

        public void Clear()
        {
            diagnostics.Clear();
        }

        public bool Contains(string item)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Message, item, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public void CopyTo(string[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + diagnostics.Count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            for (int i = 0; i < diagnostics.Count; i++)
                array[arrayIndex + i] = diagnostics[i].Message;
        }

        public bool Remove(string item)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (!string.Equals(diagnostics[i].Message, item, StringComparison.Ordinal))
                    continue;

                diagnostics.RemoveAt(i);
                return true;
            }

            return false;
        }

        public IEnumerator<string> GetEnumerator()
        {
            for (int i = 0; i < diagnostics.Count; i++)
                yield return diagnostics[i].Message;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private sealed class PrefixedCollection : ICollection<string>
        {
            private readonly ValidationReport report;
            private readonly string prefix;

            public PrefixedCollection(ValidationReport owner, string valuePrefix)
            {
                report = owner;
                prefix = valuePrefix;
            }

            public int Count => report.Count;
            public bool IsReadOnly => false;

            public void Add(string item)
            {
                report.Add(prefix + item);
            }

            public void Clear()
            {
                throw new NotSupportedException("A prefixed validation view cannot clear its report.");
            }

            public bool Contains(string item)
            {
                return report.Contains(prefix + item);
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                foreach (string message in this)
                    array[arrayIndex++] = message;
            }

            public bool Remove(string item)
            {
                return report.Remove(prefix + item);
            }

            public IEnumerator<string> GetEnumerator()
            {
                foreach (string message in report)
                {
                    if (message.StartsWith(prefix, StringComparison.Ordinal))
                        yield return message;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
