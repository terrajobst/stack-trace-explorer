using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Terrajobst.StackTraces;

namespace Terrajobst.StackExplorer
{
    internal sealed class VisualStudioStackTraceWriter : CompilationStackTraceWriter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<Inline> _inlines = new List<Inline>();

        public VisualStudioStackTraceWriter(ImmutableArray<Compilation> compilations, IServiceProvider serviceProvider)
            : base(compilations)
        {
            _serviceProvider = serviceProvider;
        }

        public IReadOnlyList<Inline> GetInlines() => _inlines.ToArray();

        protected override void WriteText(string text)
        {
            _inlines.Add(new Run(text));
        }

        protected override void WriteSymbol(string text, ISymbol symbol)
        {
            var location = symbol.Locations.First();
            var isMethod = symbol is IMethodSymbol;

            var run = new Run(text)
            {
                ToolTip = symbol.ToString()
            };

            if (location.IsInSource)
            {
                var path = location.SourceTree.FilePath;
                var position = location.GetLineSpan().StartLinePosition;
                var line = position.Line;
                var character = position.Character;

                RegisterNavigationCommand(run, path, line, character);
            }

            _inlines.Add(run);
        }

        protected override void WritePath(string path, int lineNumber)
        {
            var text = Path.GetFileName(path) + ":" + lineNumber;

            var run = new Run(text)
            {
                ToolTip = path
            };

            RegisterNavigationCommand(run, path, lineNumber - 1, 0);

            _inlines.Add(run);
        }

        private void RegisterNavigationCommand(Run run, string path, int line, int character)
        {
            run.Cursor = Cursors.Hand;
            run.MouseEnter += (s, e) => run.TextDecorations = TextDecorations.Underline;
            run.MouseLeave += (s, e) => run.TextDecorations = null;
            run.MouseDown += (s, e) => NavigateTo(path, line, character);
        }

        private void NavigateTo(string fileName, int line, int character)
        {
            VsShellUtilities.OpenDocument(_serviceProvider, fileName);

            var docTable = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
            if (docTable.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, fileName, out var hierarchy,
                out var itemid, out var bufferPtr, out var cookie) != VSConstants.S_OK)
                return;

            try
            {
                var lines = Marshal.GetObjectForIUnknown(bufferPtr) as IVsTextLines;
                if (lines == null)
                    return;

                var textManager = (IVsTextManager)_serviceProvider.GetService(typeof(SVsTextManager));
                if (textManager == null)
                    return;

                textManager.NavigateToLineAndColumn(lines, VSConstants.LOGVIEWID.TextView_guid, line, character, line, character);
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                    Marshal.Release(bufferPtr);
            }
        }
    }
}