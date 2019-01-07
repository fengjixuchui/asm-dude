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
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace AsmDude.CodeCompletion
{
    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [Name("AsmDude CompletionCommitManagerProvider")]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    class CodeCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
    {
        IDictionary<ITextView, IAsyncCompletionCommitManager> _cache = new Dictionary<ITextView, IAsyncCompletionCommitManager>();

        public IAsyncCompletionCommitManager GetOrCreate(ITextView textView)
        {
            if (this._cache.TryGetValue(textView, out var itemSource))
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetOrCreate: returning cached CompletionCommitManager", this.ToString()));
                return itemSource;
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetOrCreate: returning cached CompletionCommitManager", this.ToString()));
                var manager = new CodeCompletionCommitManager();
                textView.Closed += (o, e) => this._cache.Remove(textView); // clean up memory as files are closed
                this._cache.Add(textView, manager);
                return manager;
            }
        }
    }
}
