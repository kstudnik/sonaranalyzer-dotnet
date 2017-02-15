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
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public class CheckFileLicense : ParameterLoadingDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1451";
        internal const string MessageFormat = "Add or update the header of this file.";

        internal const string HeaderFormatRuleParameterKey = "headerFormat";
        internal const string HeaderFormatPropertyKey = nameof(HeaderFormat);
        internal const string HeaderFormatDefaultValue = @"/*
 * SonarQube, open source software quality management tool.
 * Copyright (C) 2008-2013 SonarSource
 * mailto:contact AT sonarsource DOT com
 *
 * SonarQube is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * SonarQube is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */";

        internal const string IsRegularExpressionRuleParameterKey = "isRegularExpression";
        internal const string IsRegularExpressionPropertyKey = nameof(IsRegularExpression);
        internal const string IsRegularExpressionDefaultValue = "false";

        [RuleParameter(HeaderFormatRuleParameterKey, PropertyType.String, "Expected copyright and license header.",
            HeaderFormatDefaultValue)]
        public string HeaderFormat { get; set; } = HeaderFormatDefaultValue;

        [RuleParameter(IsRegularExpressionRuleParameterKey, PropertyType.Boolean,
            "Whether the headerFormat is a regular expression.", IsRegularExpressionDefaultValue)]
        public bool IsRegularExpression { get; set; } = bool.Parse(IsRegularExpressionDefaultValue);

        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        protected sealed override DiagnosticDescriptor Rule => rule;

        protected override void Initialize(ParameterLoadingAnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(stac =>
            {
                if (HeaderFormat == null)
                {
                    return;
                }

                if (IsRegularExpression && !IsRegexPatternValid(HeaderFormat))
                {
                    throw new ArgumentException($"Invalid regular expression: {HeaderFormat}", HeaderFormatRuleParameterKey);
                }

                var firstNode = stac.Tree.GetRoot().ChildTokens().FirstOrDefault().Parent;
                if (!HasValidLicenseHeader(firstNode))
                {
                    var properties = CreateDiagnosticProperties();
                    stac.ReportDiagnostic(Diagnostic.Create(Rule, Location.Create(stac.Tree, TextSpan.FromBounds(0, 0)), properties));
                }
            });
        }

        private static bool IsRegexPatternValid(string pattern)
        {
            try
            {
                Regex.Match(string.Empty, pattern);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool HasValidLicenseHeader(SyntaxNode node)
        {
            if (node == null || !node.HasLeadingTrivia)
            {
                return false;
            }

            var header = GetHeaderOrDefault(node.GetLeadingTrivia().First());
            if (header == null || !AreHeadersEqual(header))
            {
                return false;
            }

            return true;
        }

        private static string GetHeaderOrDefault(SyntaxTrivia trivia)
        {
            var isComment = trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia);

            return isComment ? trivia.ToString() : null;
        }

        private bool AreHeadersEqual(string currentHeader)
        {
            if (IsRegularExpression)
            {
                return Regex.IsMatch(currentHeader, HeaderFormat, RegexOptions.Compiled);
            }
            else
            {
                return currentHeader == HeaderFormat;
            }
        }

        private ImmutableDictionary<string, string> CreateDiagnosticProperties()
        {
            return ImmutableDictionary<string, string>.Empty
                .Add(HeaderFormatPropertyKey, HeaderFormat)
                .Add(IsRegularExpressionPropertyKey, IsRegularExpression.ToString());
        }
    }
}
