﻿// This file originally obtained from
// https://github.com/DotNetAnalyzers/StyleCopAnalyzers
// and that originally came from
// https://raw.githubusercontent.com/code-cracker/code-cracker/08c1a01337964924eeed12be8b14c8ce8ec6b626/ [...]
// 		src/Common/CodeCracker.Common/Extensions/GeneratedCodeAnalysisExtensions.cs
// It is subject to the Apache License 2.0
// This file has been modified since obtaining it from its original source.

namespace Menees.Analyzers;

internal static class GeneratedCodeAnalysisExtensions
{
	#region Private Data Members

	private const int NonIndex = -1;

	/// <summary>
	/// A cache of the result of computing whether a document has an auto-generated header.
	/// </summary>
	/// <remarks>
	/// This allows many analyzers that run on every token in the file to avoid checking
	/// the same state in the document repeatedly.
	/// </remarks>
	private static readonly ConditionalWeakTable<SyntaxTree, StrongBox<bool?>> GeneratedHeaderPresentCheck = new();

	#endregion

	#region Public Methods

	/// <summary>
	/// Checks whether the given document is auto generated by a tool
	/// (based on filename or comment header).
	/// </summary>
	/// <remarks>
	/// <para>The exact conditions used to identify generated code are subject to change in future releases.
	/// The current algorithm uses the following checks.</para>
	/// <para>Code is considered generated if it meets any of the following conditions.</para>
	/// <list type="bullet">
	/// <item>The code is contained in a file which starts with a comment containing the text
	/// <c>&lt;auto-generated</c>.</item>
	/// <item>The code is contained in a file with a name matching certain patterns (case-insensitive):
	/// <list type="bullet">
	/// <item>*.designer.cs</item>
	/// </list>
	/// </item>
	/// </list>
	/// </remarks>
	/// <param name="tree">The syntax tree to examine.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
	/// <returns>
	/// <para><see langword="true"/> if <paramref name="tree"/> is located in generated code; otherwise,
	/// <see langword="false"/>. If <paramref name="tree"/> is <see langword="null"/>, this method returns
	/// <see langword="false"/>.</para>
	/// </returns>
	public static bool IsGeneratedDocument(this SyntaxTree tree, Settings settings, CancellationToken cancellationToken)
	{
		bool result = false;

		if (tree != null)
		{
			StrongBox<bool?> cachedResult = GeneratedHeaderPresentCheck.GetOrCreateValue(tree);
			if (cachedResult.Value.HasValue)
			{
				result = cachedResult.Value.Value;
			}
			else
			{
				bool autoGenerated = IsGeneratedDocumentNoCache(tree, settings, cancellationToken);

				// Update the strongbox's value with our computed result.
				// This doesn't change the strongbox reference, and its presence in the
				// ConditionalWeakTable is already assured, so we're updating in-place.
				// In the event of a race condition with another thread that set the value,
				// we'll just be re-setting it to the same value.
				cachedResult.Value = autoGenerated;

				result = autoGenerated;
			}
		}

		return result;
	}

	#endregion

	#region Internal Methods

	/// <summary>
	/// Checks whether the given document is auto generated by a tool.
	/// </summary>
	/// <remarks>
	/// <para>This method uses <see cref="IsGeneratedDocument(SyntaxTree, CancellationToken)"/> to determine which
	/// code is considered "generated".</para>
	/// </remarks>
	/// <param name="context">The analysis context for a <see cref="SyntaxTree"/>.</param>
	/// <returns>
	/// <para><see langword="true"/> if the <see cref="SyntaxTree"/> contained in <paramref name="context"/> is
	/// located in generated code; otherwise, <see langword="false"/>.</para>
	/// </returns>
	internal static bool IsGeneratedDocument(this SyntaxTreeAnalysisContext context, Settings settings)
		=> IsGeneratedDocument(context.Tree, settings, context.CancellationToken);

	#endregion

	#region Private Methods

	private static bool IsGeneratedDocumentNoCache(SyntaxTree tree, Settings settings, CancellationToken cancellationToken)
		=> !settings.IsAnalyzeFileNameCandidate(tree.FilePath)
			|| HasAutoGeneratedComment(tree, cancellationToken)
			|| IsEmpty(tree, cancellationToken);

	/// <summary>
	/// Checks whether the given document has an auto-generated comment as its header.
	/// </summary>
	/// <param name="tree">The syntax tree to examine.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
	/// <returns>
	/// <para><see langword="true"/> if <paramref name="tree"/> starts with a comment containing the text
	/// <c>&lt;auto-generated</c>; otherwise, <see langword="false"/>.</para>
	/// </returns>
	private static bool HasAutoGeneratedComment(SyntaxTree tree, CancellationToken cancellationToken)
	{
		bool result = false;

		var root = tree.GetRoot(cancellationToken);
		if (root != null)
		{
			var firstToken = root.GetFirstToken();
			SyntaxTriviaList? trivia = null;
			if (firstToken == default)
			{
				var token = ((CompilationUnitSyntax)root).EndOfFileToken;
				if (token.HasLeadingTrivia)
				{
					trivia = token.LeadingTrivia;
				}
			}
			else if (firstToken.HasLeadingTrivia)
			{
				trivia = firstToken.LeadingTrivia;
			}

			if (trivia != null)
			{
				var comments = trivia.Value.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
				result = comments.Any(t =>
				{
					string s = t.ToString();
					return s.Contains("<auto-generated") || s.Contains("<autogenerated");
				});
			}
		}

		return result;
	}

	/// <summary>
	/// Checks if a given <see cref="SyntaxTree"/> only contains whitespaces. We don't want to analyze empty files.
	/// </summary>
	/// <param name="tree">The syntax tree to examine.</param>
	/// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
	/// <returns>
	/// <para><see langword="true"/> if <paramref name="tree"/> only contains whitespaces; otherwise, <see langword="false"/>.</para>
	/// </returns>
	private static bool IsEmpty(SyntaxTree tree, CancellationToken cancellationToken)
	{
		bool result = false;

		var root = tree.GetRoot(cancellationToken);
		if (root != null)
		{
			var firstToken = root.GetFirstToken(includeZeroWidth: true);

			result = firstToken.IsKind(SyntaxKind.EndOfFileToken)
				&& IndexOfFirstNonWhitespaceTrivia(firstToken.LeadingTrivia) == NonIndex;
		}

		return result;
	}

	/// <summary>
	/// Returns the index of the first non-whitespace trivia in the given trivia list.
	/// </summary>
	/// <param name="triviaList">The trivia list to process.</param>
	/// <returns>The index where the non-whitespace starts, or -1 if there is no non-whitespace trivia.</returns>
	private static int IndexOfFirstNonWhitespaceTrivia(IReadOnlyList<SyntaxTrivia> triviaList)
	{
		int result = NonIndex;

		for (var index = 0; index < triviaList.Count && result == NonIndex; index++)
		{
			var currentTrivia = triviaList[index];
			switch (currentTrivia.Kind())
			{
				case SyntaxKind.EndOfLineTrivia:
				case SyntaxKind.WhitespaceTrivia:
					break;

				default:
					// encountered non-whitespace trivia -> the search is done.
					result = index;
					break;
			}
		}

		return result;
	}

	#endregion
}
