// The MIT License (MIT)
//
// Copyright (c) 2018 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmDude.Tools;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace AsmDude.CodeCompletion
{
    internal class CodeCompletionCommitManager : IAsyncCompletionCommitManager
    {
        //constructor
        public CodeCompletionCommitManager()
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor", this.ToString()));
        }

        private static readonly ImmutableArray<char> commitChars = new char[] { ' ', '\t', ',', '.', ';', ':', '-' }.ToImmutableArray();

        //
        // Summary:
        //     Returns characters that may commit completion. When completion is active and
        //     a text edit matches one of these characters, Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.IAsyncCompletionCommitManager.ShouldCommitCompletion(System.Char,Microsoft.VisualStudio.Text.SnapshotPoint,System.Threading.CancellationToken)
        //     is called to verify that the character is indeed a commit character at a given
        //     location. Called on UI thread.
        public IEnumerable<char> PotentialCommitCharacters => commitChars;

        public bool ShouldCommitCompletion(IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ShouldCommitCompletion: typedChar='{1}'", this.ToString(), typedChar));

            // This method runs synchronously, potentially before CompletionItem has been computed.
            // The purpose of this method is to filter out characters not applicable at given location.

            // This method is called only when typedChar is among the PotentialCommitCharacters
            // in this simple example, all PotentialCommitCharacters do commit, so we always return true
            return true;
        }

        public CommitResult TryCommit(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
        {
            // Objects of interest here are session.TextView and session.TextView.Caret.
            // This method runs synchronously
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:TryCommit: typedChar='{1}'", this.ToString(), typedChar));
            return CommitResult.Unhandled; // use default commit mechanism.
        }
    }
}
