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
using AsmTools;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AsmDude.QuickInfo
{
    public partial class InstructionTooltipWindow : IInteractiveQuickInfoContent
    {
        private IList<TextBox> _itemsOnPage;
        private int _lineNumber;
        private AsmSimulator _asmSimulator;

        internal AsmQuickInfoController Owner { get; set; }
        internal IQuickInfoSession Session { get; set; }

        public InstructionTooltipWindow(Brush foreground)
        {
           this.InitializeComponent();
        }
        
        private void StackPanel_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:StackPanel_Click");
        }

        private void AsmSimExpander_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:AsmSimExpander_Click");
        }

        private void PerformanceExpander_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceExpander_Click");
        }

        private void PerformanceBorder_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceBorder_Click");
        }

        private void ScrollViewer_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:ScrollViewer_Click");
        }

        private void TextBlock_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:TextBlock_Click");
        }

        private void PerformanceExpander_MouseLeftDown(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceExpander_MouseLeftDown");
        }

        public bool KeepQuickInfoOpen
        {
            get
            {
                AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:KeepQuickInfoOpen");
                return true;
                //this.IsMouseOverAggregated || this.IsKeyboardFocusWithin || this.IsKeyboardFocused || this.IsFocused;
            }
        }

        bool IInteractiveQuickInfoContent.IsMouseOverAggregated
        {
            get
            {
                AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:IsMouseOverAggregated");
                return this.IsMouseOver || this.IsMouseDirectlyOver;
            }
        }


        private async System.Threading.Tasks.Task Update_Async(Button button)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Update_Async", this.ToString()));

            if (button == null)
            {
                return;
            }

            if (this._asmSimulator == null)
            {
                return;
            }

            try
            {
                if (!ThreadHelper.CheckAccess())
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                }

                ButtonInfo info = (ButtonInfo)button.Tag;
                info.text.Text = (info.reg == Rn.NOREG)
                    ? info.flag.ToString() + " = " + this._asmSimulator.Get_Flag_Value_and_Block(info.flag, this._lineNumber, info.before)
                    : info.reg.ToString() + " = " + this._asmSimulator.Get_Register_Value_and_Block(info.reg, this._lineNumber, info.before, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration));

                info.text.Visibility = Visibility.Visible;
                button.Visibility = Visibility.Collapsed;
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Update_Async; e={1}", this.ToString(), e.ToString()));
            };
        }
    }
}
