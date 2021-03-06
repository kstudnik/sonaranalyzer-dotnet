﻿/*
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SonarAnalyzer.Common.CSharp
{
    public class Metrics : MetricsBase
    {
        public Metrics(SyntaxTree tree) : base(tree)
        {
            var root = tree.GetRoot();
            if (root.Language != LanguageNames.CSharp)
            {
                throw new ArgumentException(InitalizationErrorTextPattern, nameof(tree));
            }
        }

        protected override bool IsEndOfFile(SyntaxToken token) => token.IsKind(SyntaxKind.EndOfFileToken);

        protected override bool IsNoneToken(SyntaxToken token) => token.IsKind(SyntaxKind.None);

        protected override bool IsCommentTrivia(SyntaxTrivia trivia) => TriviaKinds.Contains(trivia.Kind());

        protected override bool IsClass(SyntaxNode node) => ClassKinds.Contains(node.Kind());

        protected override bool IsStatement(SyntaxNode node) => node is StatementSyntax && !node.IsKind(SyntaxKind.Block);

        protected override bool IsFunction(SyntaxNode node)
        {
            var property = node as PropertyDeclarationSyntax;
            if (property != null && property.ExpressionBody != null)
            {
                return true;
            }

            var method = node as MethodDeclarationSyntax;
            if (method != null && method.ExpressionBody != null)
            {
                return true;
            }

            if (FunctionKinds.Contains(node.Kind()) &&
                node.ChildNodes().Any(c => c.IsKind(SyntaxKind.Block)))
            {
                // Non-abstract, non-interface methods
                return true;
            }

            var accessor = node as AccessorDeclarationSyntax;
            if (accessor != null)
            {
                if (accessor.Body != null)
                {
                    return true;
                }

                var prop = accessor.Parent.Parent as BasePropertyDeclarationSyntax;
                if (prop == null)
                {
                    // Unexpected
                    return false;
                }

                if (prop.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
                {
                    return false;
                }

                return !(prop.Parent is InterfaceDeclarationSyntax);
            }

            return false;
        }

        protected override IEnumerable<SyntaxNode> PublicApiNodes
        {
            get
            {
                var root = tree.GetRoot();
                var publicNodes = ImmutableArray.CreateBuilder<SyntaxNode>();
                var toVisit = new Stack<SyntaxNode>();

                var members = root.ChildNodes()
                    .Where(childNode => childNode is MemberDeclarationSyntax);
                foreach (var member in members)
                {
                    toVisit.Push(member);
                }

                while (toVisit.Any())
                {
                    var member = toVisit.Pop();

                    var isPublic = member.ChildTokens().Any(t => t.IsKind(SyntaxKind.PublicKeyword));
                    if (isPublic)
                    {
                        publicNodes.Add(member);
                    }

                    if (!isPublic &&
                        !member.IsKind(SyntaxKind.NamespaceDeclaration))
                    {
                        continue;
                    }

                    members = member.ChildNodes()
                        .Where(childNode => childNode is MemberDeclarationSyntax);
                    foreach (var child in members)
                    {
                        toVisit.Push(child);
                    }
                }

                return publicNodes.ToImmutable();
            }
        }

        protected override bool IsReturnButNotLast(SyntaxNode node) =>
            node.IsKind(SyntaxKind.ReturnStatement) && !IsLastStatement(node);

        protected override bool IsComplexityIncreasingKind(SyntaxNode node) =>
            ComplexityIncreasingKinds.Contains(node.Kind());

        private bool IsLastStatement(SyntaxNode node)
        {
            var nextToken = node.GetLastToken().GetNextToken();
            return nextToken.Parent.IsKind(SyntaxKind.Block) &&
                IsFunction(nextToken.Parent.Parent);
        }

        private static readonly ISet<SyntaxKind> TriviaKinds = ImmutableHashSet.Create(
            SyntaxKind.SingleLineCommentTrivia,
            SyntaxKind.MultiLineCommentTrivia,
            SyntaxKind.SingleLineDocumentationCommentTrivia,
            SyntaxKind.MultiLineDocumentationCommentTrivia
        );

        private static readonly ISet<SyntaxKind> ClassKinds = ImmutableHashSet.Create(
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration
        );

        private static readonly ISet<SyntaxKind> FunctionKinds = ImmutableHashSet.Create(
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.OperatorDeclaration
        );

        private static readonly ISet<SyntaxKind> ComplexityIncreasingKinds = ImmutableHashSet.Create(
            SyntaxKind.IfStatement,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.ConditionalAccessExpression,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.SwitchStatement,
            SyntaxKind.LabeledStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.CaseSwitchLabel
        );
    }
}
