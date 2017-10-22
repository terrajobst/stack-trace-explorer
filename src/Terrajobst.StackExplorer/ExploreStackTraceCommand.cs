using System;
using System.ComponentModel.Design;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Terrajobst.StackExplorer
{
    internal sealed class ExploreStackTraceCommand
    {
        public static readonly Guid CommandSet = new Guid("af9da84c-2742-4bc4-a2c1-2370f8bff5ee");
        public const int CommandId = 0x0100;

        public static ExploreStackTraceCommand Instance { get; private set; }

        public static void Initialize(Package package)
        {
            Instance = new ExploreStackTraceCommand(package);
        }

        private readonly Package _package;

        private ExploreStackTraceCommand(Package package)
        {
            _package = package ?? throw new ArgumentNullException("package");

            var serviceProvider = (IServiceProvider)_package;

            if (serviceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(ShowToolWindow, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        private void ShowToolWindow(object sender, EventArgs e)
        {
            var window = _package.FindToolWindow(typeof(StackTraceExplorerPane), 0, true) as StackTraceExplorerPane;
            if (window == null || window.Frame == null)
                throw new NotSupportedException("Cannot create tool window");

            var stackTrace = Clipboard.GetText();
            if (!string.IsNullOrEmpty(stackTrace))
                window.StackTrace = stackTrace;

            var windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}
