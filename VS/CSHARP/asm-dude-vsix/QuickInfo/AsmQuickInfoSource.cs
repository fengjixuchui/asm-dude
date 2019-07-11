// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace AsmDude.QuickInfo
{
    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
     //internal sealed class QuickInfoSource : IAsyncQuickInfoSource //XYZZY NEW
    internal sealed class AsmQuickInfoSource : IQuickInfoSource //XYZZY OLD
    {
        private readonly ITextBuffer _textBuffer;

        public AsmQuickInfoSource(ITextBuffer textBuffer)
        {
            this._textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) //XYZZY OLD
        {
            applicableToSpan = null;
            var triggerPoint = session.GetTriggerPoint(this._textBuffer.CurrentSnapshot);
            if (triggerPoint != null)
            {
                var line = triggerPoint.Value.GetContainingLine();
                applicableToSpan = this._textBuffer.CurrentSnapshot.CreateTrackingSpan(line.Extent, SpanTrackingMode.EdgeInclusive);
                quickInfoContent.Add(new InstructionTooltipWindow());
            }
        }

        public void Dispose() {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Dispose", this.ToString()));
        }
    }
}