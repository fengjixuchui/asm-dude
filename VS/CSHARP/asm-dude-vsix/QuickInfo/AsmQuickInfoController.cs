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
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;

namespace AsmDude.QuickInfo
{
    internal sealed class AsmQuickInfoController : IIntellisenseController
    {
        private readonly IList<ITextBuffer> _subjectBuffers;
        private readonly IQuickInfoBroker _quickInfoBroker; //XYZZY OLD
        //private readonly IAsyncQuickInfoBroker _quickInfoBroker; //XYZZY NEW
        private ITextView _textView;

        internal AsmQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            IQuickInfoBroker quickInfoBroker)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor", this.ToString()));
            this._textView = textView ?? throw new ArgumentNullException(nameof(textView));
            this._subjectBuffers = subjectBuffers ?? throw new ArgumentNullException(nameof(subjectBuffers));
            this._quickInfoBroker = quickInfoBroker ?? throw new ArgumentNullException(nameof(quickInfoBroker));
           // this._textView.MouseHover += this.OnTextViewMouseHover;
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:ConnectSubjectBuffer", this.ToString()));
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:DisconnectSubjectBuffer", this.ToString()));
        }

        public void Detach(ITextView textView)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Detach", this.ToString()));
            if (this._textView == textView)
            {
                this._textView.MouseHover -= this.OnTextViewMouseHover;
                this._textView = null;
            }
        }

        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
        {
            SnapshotPoint? point = this.GetMousePosition(new SnapshotPoint(this._textView.TextSnapshot, e.Position));
            if (point.HasValue)
            {
                int pos = point.Value.Position;

                ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(pos, PointTrackingMode.Positive);

                if (this._quickInfoBroker.IsQuickInfoActive(this._textView))
                {
                    //IAsyncQuickInfoSession current_Session = this._quickInfoBroker.GetSession(this._textView); //XYZZY NEW
                    IQuickInfoSession current_Session = this._quickInfoBroker.GetSessions(this._textView)[0]; //XYZZY OLD

                    var span = current_Session.ApplicableToSpan;
                    if ((span != null) && span.GetSpan(this._textView.TextSnapshot).IntersectsWith(new Span(pos, 0)))
                    {
                        AsmDudeToolsStatic.Output_INFO(string.Format("{0}::OnTextViewMouseHover: A: quickInfoBroker is already active: intersects!", this.ToString()));
                    }
                    else
                    {
                        AsmDudeToolsStatic.Output_INFO("QuickInfoController:OnTextViewMouseHover: B: quickInfoBroker is already active, but we need a new session at " + pos);
                        //_ = current_Session.DismissAsync(); //XYZZY NEW
                        //_ = this._quickInfoBroker.TriggerQuickInfoAsync(this._textView, triggerPoint, QuickInfoSessionOptions.None); //BUG here QuickInfoSessionOptions.None behaves as TrackMouse  //XYZZY NEW
                        current_Session.Dismiss(); //XYZZY OLD
                        this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);  //XYZZY OLD
                    }
                }
                else
                {
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}::OnTextViewMouseHover: C: quickInfoBroker was not active, create a new session for triggerPoint {1}", this.ToString(), pos));
                    //_ = this._quickInfoBroker.TriggerQuickInfoAsync(this._textView, triggerPoint, QuickInfoSessionOptions.None); //XYZZY NEW
                    this._quickInfoBroker.TriggerQuickInfo(this._textView, triggerPoint, false);  //XYZZY OLD
                }
            }
            else
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:OnTextViewMouseHover: point has not value", this.ToString()));
            }
        }

        /// <summary>
        /// Get mouse location on screen. Used to determine what word the cursor is currently hovering over.
        /// </summary>
        private SnapshotPoint? GetMousePosition(SnapshotPoint topPosition)
        {
            // Map this point down to the appropriate subject buffer.

            return this._textView.BufferGraph.MapDownToFirstMatch(
                topPosition,
                PointTrackingMode.Positive,
                snapshot => this._subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor
            );
        }
    }
}
