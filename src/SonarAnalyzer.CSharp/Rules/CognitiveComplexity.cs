/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public class CognitiveComplexity : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3776";
        internal const string MessageFormat = "Refactor this {0} to reduce its Cognitive Complexity from {1} to the {2} allowed";
        private const int DefaultThreshold = 15;
        private const int DefaultPropertyThreshold = 3;

        [RuleParameter("threshold", PropertyType.Integer, "The maximum authorized complexity.", DefaultThreshold)]
        public int Threshold { get; set; } = DefaultThreshold;

        [RuleParameter("propertyThreshold ", PropertyType.Integer, "The maximum authorized complexity in a property.", DefaultPropertyThreshold)]
        public int PropertyThreshold { get; set; } = DefaultPropertyThreshold;

        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        protected sealed override DiagnosticDescriptor Rule => rule;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<MethodDeclarationSyntax>(c, m => m.Identifier.GetLocation(),
                    "method", Threshold),
                SyntaxKind.MethodDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<ConstructorDeclarationSyntax>(c, co => co.Identifier.GetLocation(),
                    "constructor", Threshold),
                SyntaxKind.ConstructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<DestructorDeclarationSyntax>(c, d => d.Identifier.GetLocation(),
                    "destructor", Threshold),
                SyntaxKind.DestructorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<OperatorDeclarationSyntax>(c, o => o.OperatorToken.GetLocation(),
                    "operator", Threshold),
                SyntaxKind.OperatorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckComplexity<AccessorDeclarationSyntax>(c, a => a.Keyword.GetLocation(),
                    "accessor", PropertyThreshold),
                SyntaxKind.GetAccessorDeclaration,
                SyntaxKind.SetAccessorDeclaration);

            context.RegisterSyntaxNodeActionInNonGenerated(
               c => CheckComplexity<FieldDeclarationSyntax>(c, m => m.Declaration.Variables[0].Identifier.GetLocation(),
                    "field", Threshold),
               SyntaxKind.FieldDeclaration);
        }

        protected void CheckComplexity<TSyntax>(SyntaxNodeAnalysisContext context,
            Func<TSyntax, Location> getLocationToReport, string declarationType, int threshold)
            where TSyntax : SyntaxNode
        {
            var syntax = (TSyntax)context.Node;

            var cognitiveWalker = new CognitiveComplexityWalker();
            cognitiveWalker.Visit(syntax);
            if (cognitiveWalker.NestingLevel != 0)
            {
                throw new Exception($"There is a problem with the cognitive complexity walker. Expecting ending nesting to be '0' got '{cognitiveWalker.NestingLevel}'");
            }

            if (cognitiveWalker.Complexity > Threshold)
            {
                context.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics.First(), getLocationToReport(syntax),
                    cognitiveWalker.Flow.Select(x => x.Location),
                    CreatePropertiesFromCognitiveTrace(cognitiveWalker.Flow),
                    new object[] { declarationType, cognitiveWalker.Complexity, threshold }));
            }
        }

        private ImmutableDictionary<string, string> CreatePropertiesFromCognitiveTrace(
            IEnumerable<CognitiveIncrement> cognitiveTrace)
        {
            int index = 0;

            return cognitiveTrace.ToDictionary(
                x =>
                {
                    string val = index.ToString();
                    index++;
                    return val;
                },
                x => x.Message).ToImmutableDictionary();
        }

        private class CognitiveIncrement
        {
            public CognitiveIncrement(Location location, int localComplexity)
            {
                Location = location;
                Message = localComplexity == 1
                    ? "+1"
                    : $"+{localComplexity} (incl {localComplexity - 1} for nesting)";
            }

            public Location Location { get; }
            public string Message { get; }
        }

        private class CognitiveComplexityWalker : CSharpSyntaxWalker
        {
            private readonly HashSet<CognitiveIncrement> flow = new HashSet<CognitiveIncrement>();
            private readonly HashSet<ExpressionSyntax> logicalOperationsToIgnore = new HashSet<ExpressionSyntax>();

            private string currentMethodName;

            public int NestingLevel { get; private set; } = 0;
            public int Complexity { get; private set; } = 0;
            public IEnumerable<CognitiveIncrement> Flow
            {
                get { return flow; }
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                currentMethodName = node.Identifier.ValueText;
                base.VisitMethodDeclaration(node);
            }

            public override void VisitIfStatement(IfStatementSyntax node)
            {
                if (node.Parent.IsKind(SyntaxKind.ElseClause))
                {
                    base.VisitIfStatement(node);
                }
                else
                {
                    IncreaseComplexityByNestingPlusOne(node.IfKeyword);
                    VisitWithNesting(node, base.VisitIfStatement);
                }
            }

            public override void VisitElseClause(ElseClauseSyntax node)
            {
                IncreaseComplexityByOne(node.ElseKeyword);
                base.VisitElseClause(node);
            }

            public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.QuestionToken);
                VisitWithNesting(node, base.VisitConditionalExpression);
            }

            public override void VisitSwitchStatement(SwitchStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.SwitchKeyword);
                VisitWithNesting(node, base.VisitSwitchStatement);
            }

            public override void VisitForStatement(ForStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.ForKeyword);
                VisitWithNesting(node, base.VisitForStatement);
            }

            public override void VisitWhileStatement(WhileStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.WhileKeyword);
                VisitWithNesting(node, base.VisitWhileStatement);
            }

            public override void VisitDoStatement(DoStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.DoKeyword);
                VisitWithNesting(node, base.VisitDoStatement);
            }

            public override void VisitForEachStatement(ForEachStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.ForEachKeyword);
                VisitWithNesting(node, base.VisitForEachStatement);
            }

            public override void VisitCatchClause(CatchClauseSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.CatchKeyword);
                VisitWithNesting(node, base.VisitCatchClause);
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var identifierNameSyntax = node.Expression as IdentifierNameSyntax;
                if (identifierNameSyntax != null &&
                    identifierNameSyntax.Identifier.ValueText.Equals(currentMethodName, StringComparison.Ordinal))
                {
                    IncreaseComplexityByNestingPlusOne(identifierNameSyntax.Identifier);
                }

                base.VisitInvocationExpression(node);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                var nodeKind = node.Kind();
                if (!logicalOperationsToIgnore.Contains(node) &&
                    (nodeKind == SyntaxKind.LogicalAndExpression ||
                     nodeKind == SyntaxKind.LogicalOrExpression))
                {
                    var left = node.Left.RemoveParentheses();
                    if (!left.IsKind(nodeKind))
                    {
                        IncreaseComplexityByOne(node.OperatorToken);
                    }

                    var right = node.Right.RemoveParentheses();
                    if (right.IsKind(nodeKind))
                    {
                        logicalOperationsToIgnore.Add(right);
                    }
                }

                base.VisitBinaryExpression(node);
            }

            public override void VisitGotoStatement(GotoStatementSyntax node)
            {
                IncreaseComplexityByNestingPlusOne(node.GotoKeyword);
                base.VisitGotoStatement(node);
            }

            public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            {
                VisitWithNesting(node, base.VisitSimpleLambdaExpression);
            }

            public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            {
                VisitWithNesting(node, base.VisitParenthesizedLambdaExpression);
            }

            private void VisitWithNesting<TSyntaxNode>(TSyntaxNode node, Action<TSyntaxNode> visit)
            {
                NestingLevel++;
                visit(node);
                NestingLevel--;
            }

            private void IncreaseComplexityByOne(SyntaxToken token)
            {
                IncreaseComplexity(token, 1);
            }

            private void IncreaseComplexityByNestingPlusOne(SyntaxToken token)
            {
                IncreaseComplexity(token, NestingLevel + 1);
            }

            private void IncreaseComplexity(SyntaxToken token, int increment)
            {
                Complexity += increment;
                flow.Add(new CognitiveIncrement(token.GetLocation(), increment));
            }
        }
    }
}
