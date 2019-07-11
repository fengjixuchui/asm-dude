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

using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace AsmDude.Tools
{
    public static class AsmDudeToolsStatic
    {
        private static bool first_log_message = true;

        #region Singleton Factories

         #endregion Singleton Factories

        
        public static string GetFilename(ITextBuffer buffer, int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var task = GetFilenameAsync(buffer);
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)) == task)
                {
                    return await task;
                }
                else
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:GetFilename; could not get filename within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return "";
                }
            });
        }

        /// <summary>Get the full filename (with path) of the provided buffer; returns null if such name does not exist</summary>
        public static async Task<string> GetFilenameAsync(ITextBuffer buffer)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document);
            string filename = document?.FilePath;
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Get_Filename_Async: retrieving filename {1}", typeof(AsmDudeToolsStatic), filename));
            return filename;
        }


        public static async System.Threading.Tasks.Task Open_Disassembler_Async()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            dte.ExecuteCommand("Debug.Disassembly");
        }

        public static int GetFontSize(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var task = GetFontSizeAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)) == task)
                {
                    return await task;
                }
                else
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:GetFontSize; could not get font size within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return 8;
                }
            });
        }

        public static async Task<int> GetFontSizeAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontSize");
            int fontSize = (short)prop.Value;
            return fontSize;
        }

        public static FontFamily GetFontType(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var task = GetFontTypeAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)) == task)
                {
                    return await task;
                }
                else
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:GetFontType; could not get font size within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new FontFamily("Comic Sans MS");
                }
            });
        }

        public static async Task<FontFamily> GetFontTypeAsync()
        {
            await System.Threading.Tasks.Task.Yield();
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontFamily");
            string font = (string)prop.Value;
            //AsmDudeToolsStatic.Output_INFO(string.Format("AsmDudeToolsStatic:Get_Font_Type {0}", font));
            return new FontFamily(font);
        }

        public static Brush GetFontColor(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var task = GetFontColorAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)) == task)
                {
                    return await task;
                }
                else
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:GetFontColor; could not get font color within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new SolidColorBrush(Colors.Gray);
                }
            });
        }

        public static async Task<Brush> GetFontColorAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontsAndColorsItems");

            FontsAndColorsItems fci = (FontsAndColorsItems)prop.Object;

            for (int i = 1; i < fci.Count; ++i)
            {
                ColorableItems ci = fci.Item(i);
                if (ci.Name.Equals("PLAIN TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(ConvertColor(System.Drawing.ColorTranslator.FromOle((int)ci.Foreground)));
                }
            }
            Output_WARNING("AsmDudeToolsStatic:Get_Font_Color: could not retrieve text color");
            return new SolidColorBrush(Colors.Gray);
        }

        public static Brush GetBackgroundColor(int timeout_ms = 200)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var task = GetBackgroundColorAsync();
                if (await System.Threading.Tasks.Task.WhenAny(task, System.Threading.Tasks.Task.Delay(timeout_ms)) == task)
                {
                    return await task;
                }
                else
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:GetBackgroundColor; could not get font color within timeout {1} ms", "AsmDudeToolsStatic", timeout_ms));
                    return new SolidColorBrush(Colors.Gray);
                }
            });
        }

        public static async Task<Brush> GetBackgroundColorAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            DTE dte = Package.GetGlobalService(typeof(SDTE)) as DTE;
            Properties propertiesList = dte.get_Properties("FontsAndColors", "TextEditor");
            Property prop = propertiesList.Item("FontsAndColorsItems");

            FontsAndColorsItems fci = (FontsAndColorsItems)prop.Object;

            for (int i = 1; i < fci.Count; ++i)
            {
                ColorableItems ci = fci.Item(i);
                if (ci.Name.Equals("PLAIN TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(ConvertColor(System.Drawing.ColorTranslator.FromOle((int)ci.Background)));
                }
            }
            Output_WARNING("AsmDudeToolsStatic:Get_Background_Color: could not retrieve text color");
            return new SolidColorBrush(Colors.Gray);
        }

        public static async void Error_Task_Navigate_Handler(object sender, EventArgs arguments)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            Microsoft.VisualStudio.Shell.Task task = sender as Microsoft.VisualStudio.Shell.Task;

            if (task == null)
            {
                throw new ArgumentException("sender parm cannot be null");
            }
            if (string.IsNullOrEmpty(task.Document))
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: task.Document is empty");
                return;
            }

            Output_INFO("AsmDudeToolsStatic: Error_Task_Navigate_Handler: task.Document=" + task.Document);


            IVsUIShellOpenDocument openDoc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null)
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: openDoc is null");
                return;
            }

            Guid logicalView = VSConstants.LOGVIEWID_Code;

            int hr = openDoc.OpenDocumentViaProject(task.Document, ref logicalView, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider, out IVsUIHierarchy hierarchy, out uint itemId, out IVsWindowFrame frame);
            if (ErrorHandler.Failed(hr) || (frame == null))
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: OpenDocumentViaProject failed");
                return;
            }

            frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out object docData);

            VsTextBuffer buffer = docData as VsTextBuffer;
            if (buffer == null)
            {
                if (docData is IVsTextBufferProvider bufferProvider)
                {
                    ErrorHandler.ThrowOnFailure(bufferProvider.GetTextBuffer(out IVsTextLines lines));
                    buffer = lines as VsTextBuffer;

                    if (buffer == null)
                    {
                        Output_INFO("INFO: AsmDudeToolsStatic:Error_Task_Navigate_Handler: buffer is null");
                        return;
                    }
                }
            }
            IVsTextManager mgr = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (mgr == null)
            {
                Output_INFO("AsmDudeToolsStatic:Error_Task_Navigate_Handler: IVsTextManager is null");
                return;
            }

            //Output("INFO: AsmDudeToolsStatic:errorTaskNavigateHandler: navigating to row="+task.Line);
            int iStartIndex = task.Column & 0xFFFF;
            int iEndIndex = (task.Column >> 16) & 0xFFFF;
            mgr.NavigateToLineAndColumn(buffer, ref logicalView, task.Line, iStartIndex, task.Line, iEndIndex);
        }

        /// <summary>
        /// Get the path where this visual studio extension is installed.
        /// </summary>
        public static string Get_Install_Path()
        {
            try
            {
                string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string filenameDll = "AsmDude.dll";
                return fullPath.Substring(0, fullPath.Length - filenameDll.Length);
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>Get the linenumber of the start of the provided span</summary>
        public static int Get_LineNumber(SnapshotSpan span)
        {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        public static Color ConvertColor(System.Drawing.Color drawingColor)
        {
            return Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }

        public static System.Drawing.Color ConvertColor(Color mediaColor)
        {
            return System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
        }

        public static ImageSource Bitmap_From_Uri(Uri bitmapUri)
        {
            BitmapImage bitmap = new BitmapImage();
            try
            {
                bitmap.BeginInit();
                bitmap.UriSource = bitmapUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }
            catch (Exception e)
            {
                Output_WARNING("bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string Cleanup(string line)
        {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > AsmDudePackage.maxNumberOfCharsInToolTips)
            {
                return cleanedString.Substring(0, AsmDudePackage.maxNumberOfCharsInToolTips - 3) + "...";
            }
            else
            {
                return cleanedString;
            }
        }

        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_INFO(string msg)
        {
#           if DEBUG
            OutputAsync("INFO: " + msg).ConfigureAwait(false);
#           endif
        }
        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_WARNING(string msg)
        {
            OutputAsync("WARNING: " + msg).ConfigureAwait(false);
        }
        /// <summary>Output message to the AsmDude window</summary>
        public static void Output_ERROR(string msg)
        {
            OutputAsync("ERROR: " + msg).ConfigureAwait(false);
        }

        /// <summary>
        /// Output message to the AsmSim window
        /// </summary>
        public static async System.Threading.Tasks.Task OutputAsync(string msg)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsOutputWindowPane outputPane = await GetOutputPaneAsync();
            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);

            if (first_log_message)
            {
                first_log_message = false;

                StringBuilder sb = new StringBuilder();
                sb.Append("Welcome to\n");
                sb.Append(" _____             ____        _     \n");
                sb.Append("|  _  |___ _____  |    \\ _ _ _| |___ \n");
                sb.Append("|     |_ -|     | |  |  | | | . | -_|\n");
                sb.Append("|__|__|___|_|_|_| |____/|___|___|___|\n");
                sb.Append("INFO: Open source assembly extension. Making programming in assembler almost bearable.\n");
                sb.Append("INFO: More info at https://github.com/HJLebbink/asm-dude \n");
                sb.Append("----------------------------------\n");
                msg2 = sb.ToString() + msg2;
            }
            if (outputPane == null)
            {
                Debug.Write(msg2);
            }
            else
            {
                outputPane.OutputString(msg2);
                outputPane.Activate();
            }
        }

        public static async Task<IVsOutputWindowPane> GetOutputPaneAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5141");
                outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
                outputWindow.GetPane(paneGuid, out IVsOutputWindowPane pane);
                return pane;
            }
        }


        /// <summary>
        /// Find the previous keyword (if any) that exists BEFORE the provided triggerPoint, and the provided start.
        /// Eg. qqqq xxxxxx yyyyyyy zzzzzz
        ///     ^             ^
        ///     |begin        |end
        /// the previous keyword is xxxxxx
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
 
        public static bool Is_All_Upper(string input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]) && !char.IsUpper(input[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
