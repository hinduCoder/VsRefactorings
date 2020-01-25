using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Analyzer1.Test.Util
{
    internal static class Extensions
    {
        internal static Document SingleDocument(this Project project) => project.Documents.Single();
        internal static async Task<IEnumerable<T>> NodesOfType<T>(this Document document) => (await document.GetSyntaxRootAsync())?.DescendantNodesAndSelf().OfType<T>() ?? Enumerable.Empty<T>();
        internal static async Task<T> FirstNodeOfType<T>(this Document document) => (await document.NodesOfType<T>()).First();
        internal static Project ApplyOperation(this Project project, CodeActionOperation operation)
        {
            var workspace = project.Solution.Workspace;
            operation.Apply(workspace, CancellationToken.None);
            return workspace.CurrentSolution.GetProject(project.Id)!;
        }
    }
}
