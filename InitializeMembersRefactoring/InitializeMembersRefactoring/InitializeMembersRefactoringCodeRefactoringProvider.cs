using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


[assembly:InternalsVisibleTo("InitializeMembersRefactoringTests")]

namespace InitializeMembersRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(InitializeMembersRefactoringCodeRefactoringProvider)), Shared]
    internal class InitializeMembersRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Only offer a refactoring if the selected node is an initializer expression. 
            if (!(node is InitializerExpressionSyntax objCreationSyntax))
            {
                return;
            }

            if (!(node.Ancestors().FirstOrDefault(anc => anc is ObjectCreationExpressionSyntax) is ObjectCreationExpressionSyntax creat)) { return; }

            // For any type declaration node, create a code action to reverse the identifier text.
            var action = CodeAction.Create("Initialize members", c => InitializeMembersAsync(context.Document, creat, c));

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Document> InitializeMembersAsync(Document document, ObjectCreationExpressionSyntax creat, CancellationToken cancellationToken)
        {
            TypeSyntax type = creat.Type;
            var local = creat.Ancestors().OfType<LocalDeclarationStatementSyntax>().First();
            var sm = await document.GetSemanticModelAsync();
            var bbb = sm.GetTypeInfo(type, cancellationToken);

            if (!(sm.GetSymbolInfo(type, cancellationToken).Symbol is INamedTypeSymbol ts))
                return document;

            IEnumerable<IPropertySymbol> properties = GetPropertySymbols(ts);

            var existingsInitializer = creat.DescendantNodes().OfType<InitializerExpressionSyntax>().FirstOrDefault();
            if (existingsInitializer is null)
                // Unexpected - this refactoring should only be invoked within an existing initializer
                return document;
            var assignments = existingsInitializer.DescendantNodes().OfType<AssignmentExpressionSyntax>().ToList();
            var names = assignments.Select(assn => ((IdentifierNameSyntax)assn.Left).Identifier.ValueText).ToList();

            foreach (var property in properties)
            {
                // Skip if the variable already had assignment
                if (names?.Any(name => name.Equals(property.Name)) ?? false)
                    continue;

                // Create initializer with no body for user to fill in
                var syntax = SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(property.Name),
                    SyntaxFactory.IdentifierName(string.Empty));

                var separator = SyntaxFactory.Token(SyntaxKind.CommaToken);
                SyntaxReference syntaxReference = property.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxReference != null)
                {
                    var declNode = await syntaxReference.GetSyntaxAsync(cancellationToken);
                    if (declNode != null && declNode.ChildTokens().Any(token => token.IsKind(SyntaxKind.RequiredKeyword)))
                        syntax = syntax.WithTrailingTrivia(SyntaxFactory.Comment($" /* required */ "));
                }
                assignments.Add(syntax);
            }

            var assignmentsSyntax = SyntaxFactory.SeparatedList<ExpressionSyntax>(assignments);
            var init = SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression, assignmentsSyntax);
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            SyntaxNode newRoot = root.ReplaceNode(existingsInitializer, init);
            return document.WithSyntaxRoot(newRoot);

        }

        private static IEnumerable<IPropertySymbol> GetPropertySymbols(INamedTypeSymbol typeSymbol)
        {
            foreach (var symbol in typeSymbol.GetMembers())
            {
                if (symbol is IPropertySymbol propertySymbol && propertySymbol.SetMethod != null && !propertySymbol.IsReadOnly && 
                    (propertySymbol.SetMethod.DeclaredAccessibility == Accessibility.Public || propertySymbol.SetMethod.IsInitOnly))
                {
                    yield return propertySymbol;
                }
            }

            if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object) 
            { 
                foreach (var ts in GetPropertySymbols(typeSymbol.BaseType))
                {
                    yield return ts;
                }
            }
        }
    }
}
