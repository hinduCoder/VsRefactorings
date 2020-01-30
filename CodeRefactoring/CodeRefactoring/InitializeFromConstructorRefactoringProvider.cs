using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodeRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InitializeFromConstructorRefactoringProvider)), Shared]
    public class InitializeFromConstructorRefactoringProvider : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            if (root == null)
                return;
            var node = root.FindNode(context.Span);

            var fieldNode = node.AncestorsAndSelf().OfType<FieldDeclarationSyntax>().FirstOrDefault();
            if (fieldNode == null)
                return;

            if (fieldNode.Declaration.Variables[0].Initializer != null)
                return;

            if (fieldNode.Modifiers.Any(SyntaxKind.PublicKeyword))
                return;

            var constructor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            var variableIdentifier = fieldNode.Declaration.Variables[0].Identifier;

            context.RegisterRefactoring(CodeAction.Create("Initialize from contstructor", async token =>
            {
                var newParameter = Parameter(
                    new SyntaxList<AttributeListSyntax>(),
                    TokenList(), 
                    fieldNode.Declaration.Type,
                    Identifier(variableIdentifier.Text.TrimStart('_')),
                    null);
                var classDeclaration = (ClassDeclarationSyntax)fieldNode.Parent;
                
                var newRoot = root;
                var constructor1 = constructor;

                if (constructor == null)
                {
                    constructor1 = CreateConstructor(classDeclaration);
                    newRoot = newRoot.ReplaceNode(classDeclaration, classDeclaration.WithMembers(classDeclaration.Members.Add(constructor1)));
                }
                var newConstructor = constructor1.AddParameterListParameters(newParameter);

                newConstructor = newConstructor.AddBodyStatements(
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            (newParameter.Identifier.Text == variableIdentifier.Text
                                ? (ExpressionSyntax)MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName(variableIdentifier))
                                : IdentifierName(variableIdentifier)),
                            IdentifierName(newParameter.Identifier))));

                newRoot = newRoot.ReplaceNode(newRoot.DescendantNodes().OfType<ConstructorDeclarationSyntax>().First(), newConstructor);
                
                return await Formatter.FormatAsync(context.Document.WithSyntaxRoot(newRoot), cancellationToken: context.CancellationToken);
            }));
        }

        private static ConstructorDeclarationSyntax CreateConstructor(ClassDeclarationSyntax classDeclaration)
        {
            ConstructorDeclarationSyntax newConstructor =
                ConstructorDeclaration(classDeclaration.Identifier)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithBody(Block())
                    .NormalizeWhitespace()
                    .WithLeadingTrivia(SyntaxTrivia(SyntaxKind.EndOfLineTrivia, Environment.NewLine));
            return newConstructor;
        }
    }
}
