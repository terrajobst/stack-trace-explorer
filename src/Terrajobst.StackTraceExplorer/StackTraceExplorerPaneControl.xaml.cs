using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Terrajobst.StackTraces;

namespace Terrajobst.StackTraceExplorer
{
    public partial class StackTraceExplorerPaneControl : UserControl
    {
        public StackTraceExplorerPaneControl(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public async void SetStackTrace(string text)
        {
            var componentModel = (IComponentModel)ServiceProvider.GetService(typeof(SComponentModel));
            var workspace = componentModel.GetService<VisualStudioWorkspace>();

            var solution = workspace.CurrentSolution;
            var compilations = await GetCompilationsAsync(solution);

            var writer = new VisualStudioStackTraceWriter(compilations, ServiceProvider);
            StackTraceWriter.Write(text, writer);

            var textBlock = new TextBlock
            {
                Padding = new Thickness(16),
                FontFamily = new FontFamily("Consolas")
            };
            textBlock.Inlines.AddRange(writer.GetInlines());

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = textBlock
            };
        }

        public async Task<ImmutableArray<Compilation>> GetCompilationsAsync(Solution solution)
        {
            var compilationTasks = new Task<Compilation>[solution.ProjectIds.Count];
            for (var i = 0; i < compilationTasks.Length; i++)
            {
                var project = solution.GetProject(solution.ProjectIds[i]);
                compilationTasks[i] = project.GetCompilationAsync();
            }

            await Task.WhenAll(compilationTasks);

            return compilationTasks.Select(t => t.Result).ToImmutableArray();
        }
    }
}