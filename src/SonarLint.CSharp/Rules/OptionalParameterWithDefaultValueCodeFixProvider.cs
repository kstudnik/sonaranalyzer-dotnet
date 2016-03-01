﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
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
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using SonarLint.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class OptionalParameterWithDefaultValueCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Change to \"[DefaultParameterValue]\"";
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(OptionalParameterWithDefaultValue.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => DocumentBasedFixAllProvider.Instance;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var attribute = root.FindNode(diagnosticSpan) as AttributeSyntax;

            if (attribute.ArgumentList == null ||
                attribute.ArgumentList.Arguments.Count != 1)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync().ConfigureAwait(false);

            var defaultParameterValueAttributeType = semanticModel?.Compilation?.GetTypeByMetadataName(
                KnownType.System_Runtime_InteropServices_DefaultParameterValueAttribute.TypeName);
            if (defaultParameterValueAttributeType == null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c =>
                    {
                        var attributeName = defaultParameterValueAttributeType
                            .ToMinimalDisplayString(semanticModel, attribute.SpanStart);
                        attributeName = attributeName.Remove(attributeName.IndexOf("Attribute", System.StringComparison.Ordinal));

                        var newAttribute = attribute.WithName(SyntaxFactory.ParseName(attributeName));
                        var newRoot = root.ReplaceNode(attribute, newAttribute);
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    }),
                context.Diagnostics);
        }
    }
}

