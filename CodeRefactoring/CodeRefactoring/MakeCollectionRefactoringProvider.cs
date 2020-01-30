using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace CodeRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MakeCollectionRefactoringProvider)), Shared]
    public class MakeCollectionRefactoringProvider : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            if (root == null)
                return;
            var node = root.FindNode(context.Span);

            if (node is TypeSyntax type)
                RegisterMakeCollectionActions(context, root, type);

            var generic = node.AncestorsAndSelf().OfType<GenericNameSyntax>().FirstOrDefault();
            if (generic != null)
            {
                var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
                var genericTypeInfo = semanticModel.GetTypeInfo(generic);
                if (genericTypeInfo.Type?.ContainingNamespace.ToString() == "System.Collections.Generic" && (genericTypeInfo.Type.Name == "List" || genericTypeInfo.Type.Name == "IEnumerable"))
                    RegisterUnwrapAction(context, root, generic);
            }
        }


        private void RegisterMakeCollectionActions(CodeRefactoringContext context, SyntaxNode root, TypeSyntax type)
        {
            context.RegisterRefactoring(CodeAction.Create(
                "Make collection",
                ImmutableArray.Create(
                    CodeAction.Create(ienumerableType(type.ToString()), token => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, SyntaxFactory.IdentifierName(ienumerableType(type.ToString())))))),
                    CodeAction.Create(listType(type.ToString()), token => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, SyntaxFactory.IdentifierName(listType(type.ToString()))))))),
                isInlinable: true));
        }
        private void RegisterUnwrapAction(CodeRefactoringContext context, SyntaxNode root, GenericNameSyntax generic)
        {
            TypeSyntax genericArgument = generic.TypeArgumentList.Arguments[0];
            context.RegisterRefactoring(CodeAction.Create($"Make {genericArgument}", _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(generic, SyntaxFactory.IdentifierName(genericArgument.ToString()))))));
        }

        private static string ienumerableType(string t) => $"IEnumerable<{t}>";
        private static string listType(string t) => $"List<{t}>";
    }
}
