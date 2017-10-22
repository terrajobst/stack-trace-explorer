using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Terrajobst.StackExplorer
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(StackTraceExplorerPane))]
    [Guid("cdc68824-5e11-4207-8caa-41f91a2716f4")]
    internal sealed class VSPackage : Package
    {
        protected override void Initialize()
        {
            ExploreStackTraceCommand.Initialize(this);
            base.Initialize();
        }
    }
}
