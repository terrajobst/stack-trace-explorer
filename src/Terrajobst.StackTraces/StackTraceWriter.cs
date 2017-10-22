using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Terrajobst.StackTraces
{
    public abstract class StackTraceWriter
    {
        public static void Write(string text, StackTraceWriter writer)
        {
            var lastPosition = 0;

            foreach (var lineExtent in GetLineExtents(text))
            {
                var line = text.Substring(lineExtent.start, lineExtent.length);
                var frameMatch = Regex.Match(line, @"^.*?(?<method>([^. ])+\..*\([^.]*\)).*?((?<path>[a-zA-Z]:.*?):.*?(?<line>[0-9]+))?$");

                if (frameMatch.Success)
                {
                    var methodNameGroup = frameMatch.Groups["method"];
                    var pathGroup = frameMatch.Groups["path"];
                    var lineNumberGroup = frameMatch.Groups["line"];

                    var methodNameStart = lineExtent.start + methodNameGroup.Index;
                    var methodNameLength = methodNameGroup.Length;
                    var methodName = text.Substring(methodNameStart, methodNameLength);

                    var pathStart = lineExtent.start + pathGroup.Index;
                    var pathLength = pathGroup.Length;
                    var path = text.Substring(pathStart, pathLength);

                    var lineNumberStart = lineExtent.start + lineNumberGroup.Index;
                    var lineNumberLength = lineNumberGroup.Length;
                    var hasLineNumber = lineNumberLength > 0;
                    var lineNumberText = text.Substring(lineNumberStart, lineNumberLength);
                    var lineNumber = hasLineNumber ? int.Parse(lineNumberText) : 0;

                    writer.WriteText(text, ref lastPosition, methodNameStart);

                    writer.WriteMethodText(methodName);

                    lastPosition = methodNameStart + methodNameLength;

                    writer.WriteText(text, ref lastPosition, pathStart);

                    if (path.Length > 0)
                        writer.WritePath(path, lineNumber);

                    lastPosition = lineExtent.start + lineExtent.length;
                }
            }

            writer.WriteText(text, ref lastPosition, text.Length);
        }

        private static IEnumerable<(int start, int length)> GetLineExtents(string text)
        {
            var start = 0;
            var position = start;

            while (position < text.Length)
            {
                var c = text[position];
                var l = position == text.Length - 1 ? '\0' : text[position + 1];

                var end = position;
                var length = end - start;
                var returnLine = false;

                if (c == '\r')
                {
                    if (l == '\n')
                        position++;

                    returnLine = true;
                }
                else if (c == '\n')
                {
                    returnLine = true;
                }

                position++;

                if (returnLine)
                {
                    yield return (start, length);
                    start = position;
                }
            }

            if (start < position)
                yield return (start, text.Length - start);
        }

        private void WriteText(string text, ref int lastPosition, int position)
        {
            var start = lastPosition;
            var length = position - lastPosition;

            if (length > 0)
            {
                WriteText(text.Substring(start, length));
                lastPosition = position;
            }
        }

        protected abstract void WriteText(string text);

        protected abstract void WriteMethodText(string methodName);

        protected abstract void WritePath(string path, int lineNumber);
    }
}
