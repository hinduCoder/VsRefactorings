using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace CodeRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InitializeFromConstructorRefactoringProvider)), Shared]
    public class InitializeFromConstructorRefactoringProvider : CodeRefactoringProvider
    {
        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            
            var node = root.FindNode(context.Span);

            while (!(node is FieldDeclarationSyntax) && node != null)
                node = node.Parent;

            if (node == null)
                return;
            var fieldNode = (FieldDeclarationSyntax)node;

            if (fieldNode.Declaration.Variables[0].Initializer != null)
                return;

            if (fieldNode.Modifiers.Any(SyntaxKind.PublicKeyword))
                return;

            var constructor = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().FirstOrDefault();
            var variableIdentifier = fieldNode.Declaration.Variables[0].Identifier;

            context.RegisterRefactoring(CodeAction.Create("Initialize from contstructor", async token =>
            {
                var newParameter = SyntaxFactory.Parameter(
                    new SyntaxList<AttributeListSyntax>(), 
                    SyntaxFactory.TokenList(), 
                    fieldNode.Declaration.Type, 
                    SyntaxFactory.Identifier(variableIdentifier.Text.TrimStart('_')),
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
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            (newParameter.Identifier.Text == variableIdentifier.Text
                                ? (ExpressionSyntax)SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.ThisExpression(),
                                    SyntaxFactory.IdentifierName(variableIdentifier))
                                : SyntaxFactory.IdentifierName(variableIdentifier)),
                            SyntaxFactory.IdentifierName(newParameter.Identifier))));

                newRoot = newRoot.ReplaceNode(newRoot.DescendantNodes().OfType<ConstructorDeclarationSyntax>().First(), newConstructor);
                
                return await Formatter.FormatAsync(context.Document.WithSyntaxRoot(newRoot), cancellationToken: context.CancellationToken);
            }));
        }

        private static ConstructorDeclarationSyntax CreateConstructor(ClassDeclarationSyntax classDeclaration)
        {
            ConstructorDeclarationSyntax newConstructor = 
                SyntaxFactory.ConstructorDeclaration(
                    new SyntaxList<AttributeListSyntax>(),
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")))),
                    classDeclaration.Identifier.WithoutTrivia(),
                    SyntaxFactory.ParameterList(),
                    null,
                    SyntaxFactory.Block()).WithLeadingTrivia(SyntaxTriviaList.Create(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, Environment.NewLine)));
            return newConstructor;
        }
    }
}
