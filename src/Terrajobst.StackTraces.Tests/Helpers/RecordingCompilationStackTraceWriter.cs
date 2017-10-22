using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Terrajobst.StackTraces.Tests.Helpers
{
    internal sealed class RecordingCompilationStackTraceWriter : CompilationStackTraceWriter
    {
        private List<IMethodSymbol> _methods = new List<IMethodSymbol>();
        private StringBuilder _stringBuilder = new StringBuilder();

        public RecordingCompilationStackTraceWriter(Compilation compilation)
            : base(ImmutableArray.Create(compilation))
        {
        }

        public string GetText()
        {
            return _stringBuilder.ToString();
        }

        public ImmutableArray<IMethodSymbol> GetMethods()
        {
            return _methods.ToImmutableArray();
        }

        protected override void WritePath(string path, int lineNumber)
        {
            _stringBuilder.Append(path);
            _stringBuilder.Append(':');
            _stringBuilder.Append(lineNumber);
        }

        protected override void WriteSymbol(string text, ISymbol symbol)
        {
            if (symbol is IMethodSymbol m)
                _methods.Add(m);

            WriteText(text);
        }

        protected override void WriteText(string text)
        {
            _stringBuilder.Append(text);
        }
    }
}
