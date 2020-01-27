using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CodeRefactoring
{
    [ExportCompletionProvider(nameof(ItIsAnyCompletionProvider), LanguageNames.CSharp)]
    public class ItIsAnyCompletionProvider : CompletionProvider
    {

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            if (syntaxRoot == null)
                return;

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            var node = syntaxRoot.FindNode(context.CompletionListSpan);

            var argumentSyntaxNode = node.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
            if (argumentSyntaxNode == null)
                return;

            var lambdaExpression = argumentSyntaxNode.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
            if (lambdaExpression == null)
                return;
            
            var mockMethod = lambdaExpression.Ancestors().FirstOrDefault(x => semanticModel.GetSymbolInfo(x, context.CancellationToken).CandidateSymbols.Any(s => s.ContainingType.Name == "Mock" && s.ContainingAssembly.Name == "Moq"));
            if (mockMethod == null)
                return;

            var typingMethod = argumentSyntaxNode.Ancestors().OfType<InvocationExpressionSyntax>().First();
            var calingMethod = semanticModel.GetSymbolInfo(typingMethod, context.CancellationToken);
            if (!(calingMethod.CandidateSymbols[0] is IMethodSymbol callingMethodSymbol))
                return;

            var index = argumentSyntaxNode.Parent.ChildNodes().ToList().FindIndex(x => x.Equals(argumentSyntaxNode));


            context.AddItem(CompletionItem.Create(GetCompletionDisplayText(callingMethodSymbol.Parameters[index].Type),
                null, null,
                ImmutableDictionary.Create<string, string>(),
                ImmutableArray.Create(WellKnownTags.Method),
                CompletionItemRules.Create(formatOnCommit: true, matchPriority: MatchPriority.Preselect)));

            if (index == 0)
            {
                context.AddItem(CompletionItem.Create(String.Join(", ", callingMethodSymbol.Parameters.Select(x => GetCompletionDisplayText(x.Type))),
                    null, null,
                    ImmutableDictionary.Create<string, string>(),
                    ImmutableArray.Create(WellKnownTags.Method),
                    CompletionItemRules.Create(formatOnCommit: true)));
            }
        }

        private string GetCompletionDisplayText(ITypeSymbol typeSymbol) => $"It.IsAny<{TypeToString(typeSymbol)}>()";

        private string TypeToString(ITypeSymbol typeSymbol) => typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        public override bool ShouldTriggerCompletion(SourceText text, int caretPosition, CompletionTrigger trigger, OptionSet options)
        {
            return trigger.Kind == CompletionTriggerKind.Insertion || trigger.Kind == CompletionTriggerKind.Invoke;
        }
    }
}
