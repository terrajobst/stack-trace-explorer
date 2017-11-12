using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Terrajobst.StackTraceExplorer
{
    [Guid("0100a14e-3984-4732-a2b8-766393de4859")]
    public class StackTraceExplorerPane : ToolWindowPane
    {
        private string _stackTrace;

        public StackTraceExplorerPane()
            : base(null)
        {
            Caption = "Stack Trace Explorer";
        }

        private StackTraceExplorerPaneControl Control => (StackTraceExplorerPaneControl)Content;

        public string StackTrace
        {
            get => _stackTrace;
            set
            {
                _stackTrace = value;
                Control.SetStackTrace(value);
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            Content = new StackTraceExplorerPaneControl(this);
        }
    }
}
