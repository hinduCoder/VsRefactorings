using Analyzer1.Test.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CodeRefactoring.Test
{
    public class InitFromCtorTests
    {
        public static IEnumerable<object[]> GetTestCases()
        {
            var testCases = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            return testCases.GroupBy(resName => Parse(resName).Test).Select(group =>
            {
                var original = group.Single(x => Parse(x).Role == "original");
                var expected = group.Single(x => Parse(x).Role == "expected");

                return new[] { Read(original), Read(expected) };
            });

            static string Read(string res)
            {
                using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(res) ?? throw new InvalidOperationException(res);
                using var streamReader = new StreamReader(s);
                return streamReader.ReadToEnd();
            }
            
            static (string Test, string Role) Parse(string res)
            {
                var match = Regex.Match(res, @"(?<test>\w+)\.(?<role>\w+)\.cs$");
                return (match.Groups["test"].Value, match.Groups["role"].Value);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public async Task InitFromCtor(string input, string expectedResult)
        {
            var project = Factory.CreateProject(input);

            var huyClass = await GetHuyClass(project);
            var operations = await Run<InitializeFromConstructorRefactoringProvider>(
                    project.SingleDocument(),
                    huyClass.DescendantNodes().OfType<FieldDeclarationSyntax>().First().Span);

            var newProject = project.ApplyOperation(operations[0]);
            var newSrc = newProject.SingleDocument().GetTextAsync().Result.ToString();

            Assert.Equal(expectedResult, newSrc);

            async Task<ClassDeclarationSyntax> GetHuyClass(Project project)
            {
                return (await project.SingleDocument().NodesOfType<ClassDeclarationSyntax>()).Single(n => n.Identifier.Text == "HUY");
            }
        }

        private Task<ImmutableArray<CodeActionOperation>> Run<T>(Document document, TextSpan span) where T : CodeRefactoringProvider, new()
        {
            var tcs = new TaskCompletionSource<ImmutableArray<CodeActionOperation>>();
            var provider = new T();
            provider.ComputeRefactoringsAsync(new CodeRefactoringContext(
                document,
                span,
                async action => tcs.SetResult(await action.GetOperationsAsync(CancellationToken.None)),
                CancellationToken.None));
            return tcs.Task;
        }
    }
}
