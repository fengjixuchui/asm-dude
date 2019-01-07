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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;

using AsmTools;
using AsmDude.Tools;
using AsmDude.SignatureHelp;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Core.Imaging;

namespace AsmDude
{
    public sealed class CompletionComparer : IComparer<CompletionItem>
    {
        public int Compare(CompletionItem x, CompletionItem y)
        {
            return x.SortText.CompareTo(y.SortText);
        }
    }

    public sealed class CodeCompletionSource : IAsyncCompletionSource
    {
        #region Static members
        private static readonly string PROPERTY_KEY = "description";

        // ImageElements may be shared by CompletionFilters and CompletionItems. The automationName parameter should be localized.
        private static readonly ImageElement ICON_LABEL =    new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 2708), "Label");
        private static readonly ImageElement ICON_REGISTER = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 2709), "Register");
        private static readonly ImageElement ICON_MNEMONIC = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 2716), "Instruction");
        private static readonly ImageElement ICON_MISC =     new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 3533), "Misc");
        private static readonly ImageElement ICON_DIRECTIVE =new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 3532), "Directive");

        // CompletionFilters are rendered in the UI as buttons
        // The displayText should be localized. Alt + Access Key toggles the filter button.
        private static readonly CompletionFilter FILTER_LABEL = new CompletionFilter("Labels", "L", ICON_LABEL);
        private static readonly CompletionFilter FILTER_REGISTER = new CompletionFilter("Registers", "R", ICON_REGISTER);
        private static readonly CompletionFilter FILTER_MNEMONIC = new CompletionFilter("Instructions", "I", ICON_MNEMONIC);
        private static readonly CompletionFilter FILTER_MISC = new CompletionFilter("Misc", "M", ICON_MISC);
        private static readonly CompletionFilter FILTER_DIRECTIVE = new CompletionFilter("Directive", "D", ICON_DIRECTIVE);

        // CompletionItem takes array of CompletionFilters.
        // In this example, items assigned "MetalloidFilters" are visible in the list if user selects either MetalFilter or NonMetalFilter.
        private static readonly ImmutableArray<CompletionFilter> FILTERS_LABEL = ImmutableArray.Create(FILTER_LABEL);
        private static readonly ImmutableArray<CompletionFilter> FILTERS_REG = ImmutableArray.Create(FILTER_REGISTER);
        private static readonly ImmutableArray<CompletionFilter> FILTERS_MNEMONIC = ImmutableArray.Create(FILTER_MNEMONIC);
        private static readonly ImmutableArray<CompletionFilter> FILTERS_MISC = ImmutableArray.Create(FILTER_MISC);
        private static readonly ImmutableArray<CompletionFilter> FILTERS_DIRECTIVE = ImmutableArray.Create(FILTER_DIRECTIVE);
        //private static readonly ImmutableArray<CompletionFilter> FILTERS_ALL = ImmutableArray.Create(FILTER_LABEL, FILTER_REGISTER, FILTER_MNEMONIC, FILTER_MISC);

        private static readonly ISet<AsmTokenType> selected = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic }.ToImmutableHashSet();
        private static readonly int MAX_LENGTH_DESCR_TEXT = 120;
        #endregion

        private readonly LabelGraph _labelGraph;
        private readonly AsmDudeTools _asmDudeTools;
        private readonly AsmSimulator _asmSimulator;

        //constructor
        public CodeCompletionSource(LabelGraph labelGraph, AsmSimulator asmSimulator)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor", this.ToString()));

            this._labelGraph = labelGraph;
            this._asmDudeTools = AsmDudeTools.Instance;
            this._asmSimulator = asmSimulator;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:InitializeCompletion: trigger.char='{1}'", this.ToString(), trigger.Character));

            // We don't trigger completion when user typed
            /*
            if (char.IsNumber(trigger.Character)         // a number
                || char.IsPunctuation(trigger.Character) // punctuation
                || trigger.Character == '\n'             // new line
                || trigger.Reason == CompletionTriggerReason.Backspace
                || trigger.Reason == CompletionTriggerReason.Deletion)
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }
            */
            if (char.IsPunctuation(trigger.Character) 
                || char.IsWhiteSpace(trigger.Character)
                || (trigger.Character == '\n')
                || (trigger.Reason == CompletionTriggerReason.Backspace)
                || (trigger.Reason == CompletionTriggerReason.Deletion))
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:InitializeCompletion: A: used typed char '{1}'. not participating in code completion", this.ToString(), trigger.Character));
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // We participate in completion and provide the "applicable to span".
            // This span is used:
            // 1. To search (filter) the list of all completion items
            // 2. To highlight (bold) the matching part of the completion items
            // 3. In standard cases, it is replaced by content of completion item upon commit.

            // If you want to extend a language which already has completion, don't provide a span, e.g.
            // return CompletionStartData.ParticipatesInCompletionIfAny

            // If you provide a language, but don't have any items available at this location,
            // consider providing a span for extenders who can't parse the codem e.g.
            // return CompletionStartData(CompletionParticipation.DoesNotProvideItems, spanForOtherExtensions);

            SnapshotSpan tokenSpan = this.FindTokenSpanAtPosition(trigger, triggerLocation);
            return new CompletionStartData(CompletionParticipation.ExclusivelyProvidesItems, tokenSpan);
        }

        // Summary:
        //     Called once per completion session to fetch the set of all completion items available
        //     at a given location. Called on a background thread.
        //
        // Parameters:
        //   trigger:
        //     What caused the completion
        //
        //   triggerLocation:
        //     Location where completion was triggered, on the subject buffer that matches this
        //     Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.IAsyncCompletionSource's
        //     content type
        //
        //   applicableToSpan:
        //     Location where completion will take place, on the view's data buffer: Microsoft.VisualStudio.Text.Editor.ITextView.TextBuffer
        //
        //   token:
        //     Cancellation token that may interrupt this operation
        //
        // Returns:
        //     A struct that holds completion items and applicable span
        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            await Task.Yield();
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetCompletionContextAsync", this.ToString()));

            string partialKeyword = applicableToSpan.GetText();
            bool useCapitals = AsmDudeToolsStatic.Is_All_Upper(partialKeyword);
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetCompletionContextAsync: partialKeyword=\"{1}\"; useCapitals={2}", this.ToString(), partialKeyword, useCapitals));

            ITextSnapshotLine line = triggerLocation.GetContainingLine();
            string lineStr = line.GetText();
            var t = AsmSourceTools.ParseLine(lineStr);
            Mnemonic mnemonic = t.Mnemonic;

            string previousKeyword = AsmDudeToolsStatic.Get_Previous_Keyword(line.Start, triggerLocation).ToUpper();

            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetCompletionContextAsync: lineSt={1}; previousKeyword={2}; mnemonic={3}", this.ToString(), lineStr, previousKeyword, mnemonic));

            if (mnemonic == Mnemonic.NONE)
            {
                if (previousKeyword.Equals("INVOKE")) //TODO INVOKE is a MASM keyword not a NASM one...
                {   // Suggest a label
                    var completion_items = this.Label_Completions(useCapitals, false).ToImmutableArray();
                    return new CompletionContext(completion_items, null, InitialSelectionHint.SoftSelection);
                }
                else
                {
                    var completion_items = this.Selected_Completions(useCapitals, selected, true).ToImmutableArray();
                    return new CompletionContext(completion_items, null, InitialSelectionHint.SoftSelection);
                }
            }
            else // the current line contains a mnemonic
            {
                //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetCompletionContextAsync: lineSt={1}; previousKeyword={2}; mnemonic={3}", this.ToString(), lineStr, previousKeyword, mnemonic));

                if (AsmSourceTools.IsJump(AsmSourceTools.ParseMnemonic(previousKeyword, true)))
                {
                    // previous keyword is jump (or call) mnemonic. Suggest "SHORT" or a label
                    var completion_items = this.Label_Completions(useCapitals, true).ToImmutableArray();
                    //var suggestionOptions = new SuggestionItemOptions("Value of your choice", $"Please enter value for key");
                    return new CompletionContext(completion_items, null, InitialSelectionHint.SoftSelection);
                }
                else if (previousKeyword.Equals("SHORT") || previousKeyword.Equals("NEAR"))
                {   // Suggest a label
                    var completion_items = this.Label_Completions(useCapitals, false).ToImmutableArray();
                    //var suggestionOptions = new SuggestionItemOptions("Value of your choice", $"Please enter value for key");
                    return new CompletionContext(completion_items, null, InitialSelectionHint.SoftSelection);
                }
                else
                {
                    IList<Operand> operands = AsmSourceTools.MakeOperands(t.Args);
                    ISet<AsmSignatureEnum> allowed = new HashSet<AsmSignatureEnum>();
                    int commaCount = AsmSignature.Count_Commas(lineStr);
                    IEnumerable<AsmSignatureElement> allSignatures = this._asmDudeTools.Mnemonic_Store.GetSignatures(mnemonic);

                    ISet<Arch> selectedArchitectures = AsmDudeToolsStatic.Get_Arch_Swithed_On();
                    foreach (AsmSignatureElement se in AsmSignatureHelpSource.Constrain_Signatures(allSignatures, operands, selectedArchitectures))
                    {
                        if (commaCount < se.Operands.Count)
                        {
                            foreach (AsmSignatureEnum s in se.Operands[commaCount])
                            {
                                allowed.Add(s);
                            }
                        }
                    }
                    var completion_items = this.Mnemonic_Operand_Completions(useCapitals, allowed, line.LineNumber).ToImmutableArray();
                    return new CompletionContext(completion_items, null, InitialSelectionHint.SoftSelection);
                }
            }
            //unreachable
            return new CompletionContext(ImmutableArray<CompletionItem>.Empty);
        }

        /// <summary>
        /// Provides detailed element information in the tooltip
        /// </summary>
        //
        // Summary:
        //     Returns tooltip associated with provided Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem.
        //     The returned object will be rendered by Microsoft.VisualStudio.Text.Adornments.IViewElementFactoryService.
        //     See its documentation for default supported types. You may export a Microsoft.VisualStudio.Text.Adornments.IViewElementFactory
        //     to provide a renderer for a custom type. Since this method is called on a background
        //     thread and on multiple platforms, an instance of UIElement may not be returned.
        //
        // Parameters:
        //   item:
        //     Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem
        //     which is a subject of the tooltip
        //
        //   token:
        //     Cancellation token that may interrupt this operation
        //
        // Returns:
        //     An object that will be passed to Microsoft.VisualStudio.Text.Adornments.IViewElementFactoryService.
        //     See its documentation for supported types.
        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetDescriptionAsync", this.ToString()));
            item.Properties.TryGetProperty(PROPERTY_KEY, out string description);
            return Task.FromResult<object>(description);
        }

        // Summary:
        //     Provides the span applicable to the prospective session. Called on UI thread
        //     and expected to return very quickly, based on textual information. This method
        //     is called sequentially on available Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.IAsyncCompletionSources
        //     until one of them returns true. Returning false does not exclude this source
        //     from participating in completion session. If no Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.IAsyncCompletionSources
        //     return true, there will be no completion session.
        //
        // Parameters:
        //   typedChar:
        //     Character typed by the user
        //
        //   triggerLocation:
        //     Location on the subject buffer that matches this Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.IAsyncCompletionSource's
        //     content type
        //
        //   applicableToSpan:
        //     Applicable span for the prospective completion session. You may set it to default
        //     if returning false
        //
        //   token:
        //     Cancellation token that may interrupt this operation
        //
        // Returns:
        //     Whether completion should use the supplied applicable span.
        //
        // Remarks:
        //     A language service should provide the span and return true even if it does not
        //     wish to provide completion. This will enable extensions to provide completion
        //     in syntactically appropriate location.
        private SnapshotSpan FindTokenSpanAtPosition(CompletionTrigger trigger, SnapshotPoint triggerLocation)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:FindTokenSpanAtPosition: triggerLocation={1}", this.ToString(), triggerLocation));
            try
            {
                if (!Settings.Default.CodeCompletion_On) {
                    return new SnapshotSpan(triggerLocation, triggerLocation);
                }
                ITextSnapshotLine line = triggerLocation.GetContainingLine();

                //1] check if current position is in a remark; if we are in a remark, no code completion
                #region
                if (!trigger.Character.Equals('#')) //TODO UGLY since the user can configure this starting character
                {
                    int pos = triggerLocation.Position - line.Start;
                    if (AsmSourceTools.IsInRemark(pos, line.GetText()))
                    {
                        AsmDudeToolsStatic.Output_INFO(string.Format("{0}:FindTokenSpanAtPosition: currently in a remark section", this.ToString()));
                        return new SnapshotSpan(triggerLocation, triggerLocation);
                    }
                }
                #endregion

                //2] find the start of the current keyword
                #region
                SnapshotPoint start = triggerLocation;
                while ((start > line.Start) && !AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
                {
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:FindTokenSpanAtPosition: finding start position. start={1}", this.ToString(), start));
                    start -= 1;
                }
                #endregion

                return new SnapshotSpan(start, triggerLocation);
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:FindTokenSpanAtPosition; e={1}", this.ToString(), e.ToString()));
                return new SnapshotSpan(triggerLocation, triggerLocation);
            }
        }

        #region Private Methods
        private IEnumerable<CompletionItem> Mnemonic_Operand_Completions(bool useCapitals, ISet<AsmSignatureEnum> allowedOperands, int lineNumber)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Mnemonic_Operand_Completions; useCapitals={1}; lineNumber={2}", this.ToString(), useCapitals, lineNumber));

            bool use_AsmSim_In_Code_Completion = this._asmSimulator.Enabled && Settings.Default.AsmSim_Show_Register_In_Code_Completion;
            bool att_Syntax = AsmDudeToolsStatic.Used_Assembler == AssemblerEnum.NASM_ATT;

            SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

            foreach (Rn regName in this._asmDudeTools.Get_Allowed_Registers())
            {
                string additionalInfo = null;
                if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
                {
                    string keyword = regName.ToString();
                    if (use_AsmSim_In_Code_Completion && this._asmSimulator.Tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(regName)))
                    {
                        var (Value, Bussy) = this._asmSimulator.Get_Register_Value(regName, lineNumber, true, false, false, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration));
                        if (!Bussy)
                        {
                            additionalInfo = Value;
                            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Mnemonic_Operand_Completions; register={1} is selected and has value {2}", this.ToString(), keyword, additionalInfo));
                        }
                    }

                    if (att_Syntax)
                    {
                        keyword = "%" + keyword;
                    }

                    Arch arch = RegisterTools.GetArch(regName);
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword : keyword.ToLower();
                    //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GetCompletionContext: keyword={1}; insertionText={2}; useCapitals={3}", this.ToString(), keyword, insertionText, useCapitals));

                    string archStr = (arch == Arch.ARCH_NONE) ? "" : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                    string descriptionStr2 = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                    string displayText = Truncat(keyword + archStr + descriptionStr2);

                    var item = new CompletionItem(
                        displayText: displayText,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Register),
                        filters: Get_Filter(AsmTokenType.Register),
                        suffix: string.Empty,
                        insertText: insertionText,
                        sortText: displayText,
                        filterText: displayText,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    item.Properties.AddProperty(PROPERTY_KEY, descriptionStr);
                    completions.Add(item);
                }
            }

            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this._asmDudeTools.Get_Token_Type_Intel(keyword);
                Arch arch = this._asmDudeTools.Get_Architecture(keyword);

                string keyword2 = keyword;
                bool selected = true;

                //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected +"; arch="+arch);

                switch (type)
                {
                    case AsmTokenType.Misc:
                        {
                            if (!AsmSignatureTools.Is_Allowed_Misc(keyword, allowedOperands))
                            {
                                selected = false;
                            }
                            break;
                        }
                    default:
                        {
                            selected = false;
                            break;
                        }
                }
                if (selected)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword2 : keyword2.ToLower();
                    string archStr = (arch == Arch.ARCH_NONE) ? "" : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                    string descriptionStr2 = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                    string displayText = Truncat(keyword2 + archStr + descriptionStr2);

                    var item = new CompletionItem(
                        displayText: displayText,
                        source: this,
                        icon: Get_Icon(type),
                        filters: Get_Filter(type),
                        suffix: string.Empty,
                        insertText: insertionText,
                        sortText: displayText,
                        filterText: displayText,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    item.Properties.AddProperty(PROPERTY_KEY, descriptionStr);
                    completions.Add(item);
                }
            }
            return completions;
        }

        private static string Truncat(string text)
        {
            if (text.Length < MAX_LENGTH_DESCR_TEXT) return text;
            return text.Substring(0, MAX_LENGTH_DESCR_TEXT) + "...";
        }

        private IEnumerable<CompletionItem> Label_Completions(bool useCapitals, bool addSpecialKeywords)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Label_Completions; useCapitals={1}; addSpecialKeywords={2}", this.ToString(), useCapitals, addSpecialKeywords));

            if (addSpecialKeywords)
            {
                {
                    var str_short = useCapitals ? "SHORT" : "short";
                    var item = new CompletionItem(
                        displayText: str_short,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Misc),
                        filters: Get_Filter(AsmTokenType.Misc),
                        suffix: string.Empty,
                        insertText: str_short,
                        sortText: "!!!!" + str_short, // just a prefix to get as a first item
                        filterText: str_short,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    yield return item;
                }
                {
                    var str_near = useCapitals ? "NEAR" : "near";
                    var item = new CompletionItem(
                        displayText: str_near,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Misc),
                        filters: Get_Filter(AsmTokenType.Misc),
                        suffix: string.Empty,
                        insertText: str_near,
                        sortText: "!!!!" + str_near, // just a prefix to get as a first item
                        filterText: str_near,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    yield return item;
                }
            }

            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            SortedDictionary<string, string> labels = this._labelGraph.Label_Descriptions;
            foreach (KeyValuePair<string, string> entry in labels)
            {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                string displayTextFull = entry.Key + " - " + entry.Value;
                string displayText = Truncat(displayTextFull);
                string insertionText = AsmDudeToolsStatic.Retrieve_Regular_Label(entry.Key, usedAssember);

                var item = new CompletionItem(
                    displayText: displayText,
                    source: this,
                    icon: Get_Icon(AsmTokenType.Label),
                    filters: Get_Filter(AsmTokenType.Label),
                    suffix: string.Empty,
                    insertText: insertionText,
                    sortText: displayText,
                    filterText: insertionText,
                    attributeIcons: ImmutableArray<ImageElement>.Empty
                );
                item.Properties.AddProperty(PROPERTY_KEY, displayTextFull);
                yield return item;
            }
        }

        private IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
        {
            SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

            //Add the completions of AsmDude directives (such as code folding directives)
            #region 
            if (addSpecialKeywords && Settings.Default.CodeFolding_On)
            {
                {
                    string insertionText = Settings.Default.CodeFolding_BeginTag;     //the characters that start the outlining region
                    string displayTextFull = insertionText + " - keyword to start code folding";
                    string displayText = Truncat(displayTextFull);
                    var item = new CompletionItem(
                        displayText: displayText,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Directive),
                        filters: Get_Filter(AsmTokenType.Directive),
                        suffix: string.Empty,
                        insertText: insertionText,
                        sortText: displayTextFull,
                        filterText: displayTextFull,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    item.Properties.AddProperty(PROPERTY_KEY, displayTextFull);
                    completions.Add(item);
                }
                {
                    string insertionText = Settings.Default.CodeFolding_EndTag;       //the characters that end the outlining region
                    string displayTextFull = insertionText + " - keyword to end code folding";
                    string displayText = Truncat(displayTextFull);
                    var item = new CompletionItem(
                        displayText: displayText,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Directive),
                        filters: Get_Filter(AsmTokenType.Directive),
                        suffix: string.Empty,
                        insertText: insertionText,
                        sortText: displayTextFull,
                        filterText: displayTextFull,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    item.Properties.AddProperty(PROPERTY_KEY, displayTextFull);
                    completions.Add(item);
                }
            }
            #endregion
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            #region Add completions

            if (selectedTypes.Contains(AsmTokenType.Mnemonic))
            {
                foreach (Mnemonic mnemonic in this._asmDudeTools.Get_Allowed_Mnemonics())
                {
                    string keyword = mnemonic.ToString();
                    string description = this._asmDudeTools.Mnemonic_Store.GetSignatures(mnemonic).First().Documentation;
                    string insertionText = useCapitals ? keyword : keyword.ToLower();
                    string archStr = ArchTools.ToString(this._asmDudeTools.Mnemonic_Store.GetArch(mnemonic));
                    string descriptionStr = this._asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
                    descriptionStr = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                    string displayText = Truncat(keyword + archStr + descriptionStr);
                    //string description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                    var item = new CompletionItem(
                        displayText: displayText,
                        source: this,
                        icon: Get_Icon(AsmTokenType.Mnemonic),
                        filters: Get_Filter(AsmTokenType.Mnemonic),
                        suffix: string.Empty,
                        insertText: insertionText,
                        sortText: displayText,
                        filterText: displayText,
                        attributeIcons: ImmutableArray<ImageElement>.Empty
                    );
                    item.Properties.AddProperty(PROPERTY_KEY, description);
                    completions.Add(item);
                }
            }

            //Add the completions that are defined in the xml file
            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this._asmDudeTools.Get_Token_Type_Intel(keyword);
                if (selectedTypes.Contains(type))
                {
                    Arch arch = Arch.ARCH_NONE;
                    bool selected = true;

                    if (type == AsmTokenType.Directive)
                    {
                        AssemblerEnum assembler = this._asmDudeTools.Get_Assembler(keyword);
                        if (assembler.HasFlag(AssemblerEnum.MASM))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.MASM)) selected = false;
                        }
                        else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.NASM_INTEL)) selected = false;
                        }
                    }
                    else
                    {
                        arch = this._asmDudeTools.Get_Architecture(keyword);
                        selected = AsmDudeToolsStatic.Is_Arch_Switched_On(arch);
                    }

                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Selected_Completions; keyword=" + keyword + "; arch=" + arch + "; selected=" + selected);

                    if (selected)
                    {
                        //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");

                        // by default, the entry.Key is with capitals
                        string insertionText = useCapitals ? keyword : keyword.ToLower();
                        string archStr = (arch == Arch.ARCH_NONE) ? "" : " [" + ArchTools.ToString(arch) + "]";
                        string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                        string descriptionStr2 = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                        string displayTextFull = keyword + archStr + descriptionStr2;
                        string displayText = Truncat(displayTextFull);
                        //string description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                        var item = new CompletionItem(
                            displayText: displayText,
                            source: this,
                            icon: Get_Icon(type),
                            filters: Get_Filter(type),
                            suffix: string.Empty,
                            insertText: insertionText,
                            sortText: displayText,
                            filterText: displayText,
                            attributeIcons: ImmutableArray<ImageElement>.Empty
                        );
                        item.Properties.AddProperty(PROPERTY_KEY, descriptionStr);
                        completions.Add(item);
                    }
                }
            }
            #endregion

            return completions;
        }

        private static ImageElement Get_Icon(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Mnemonic: 
                case AsmTokenType.Jump: return ICON_MNEMONIC;
                case AsmTokenType.Register: return ICON_REGISTER;
                case AsmTokenType.Label:
                case AsmTokenType.LabelDef: return ICON_LABEL;
                case AsmTokenType.Directive: return ICON_DIRECTIVE;
                case AsmTokenType.Misc:
                case AsmTokenType.Remark:
                case AsmTokenType.Constant:
                case AsmTokenType.UserDefined1:
                case AsmTokenType.UserDefined2:
                case AsmTokenType.UserDefined3:
                case AsmTokenType.UNKNOWN:
                default:
                    return ICON_MISC;
            }
        }

        private static ImmutableArray<CompletionFilter> Get_Filter(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Mnemonic: 
                case AsmTokenType.Jump: return FILTERS_MNEMONIC;
                case AsmTokenType.Register: return FILTERS_REG;
                case AsmTokenType.Label:
                case AsmTokenType.LabelDef: return FILTERS_LABEL;
                case AsmTokenType.Directive: return FILTERS_DIRECTIVE;
                case AsmTokenType.Misc:
                case AsmTokenType.Remark:
                case AsmTokenType.Constant:
                case AsmTokenType.UserDefined1:
                case AsmTokenType.UserDefined2:
                case AsmTokenType.UserDefined3:
                case AsmTokenType.UNKNOWN:
                default:
                    return FILTERS_MISC;
            }
        }

        public void Dispose() { }

        #endregion
    }
}