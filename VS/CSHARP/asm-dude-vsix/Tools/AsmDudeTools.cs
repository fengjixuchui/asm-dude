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

using Amib.Threading;
using AsmDude.Tools;
using AsmTools;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace AsmDude
{
    public sealed class AsmDudeTools : IDisposable
    {
        private XmlDocument _xmlData;
        private IDictionary<string, AsmTokenType> _type;
        private IDictionary<string, AssemblerEnum> _assembler;
        private IDictionary<string, Arch> _arch;
        private IDictionary<string, string> _description;
        private readonly ISet<Mnemonic> _mnemonics_switched_on;
        private readonly ISet<Rn> _register_switched_on;

        private readonly ErrorListProvider _errorListProvider;
        private readonly SmartThreadPool _threadPool;

        #region Singleton Stuff
        private static readonly Lazy<AsmDudeTools> lazy = new Lazy<AsmDudeTools>(() => new AsmDudeTools());
        public static AsmDudeTools Instance { get { return lazy.Value; } }
        #endregion Singleton Stuff


        /// <summary>
        /// Singleton pattern: use AsmDudeTools.Instance for the instance of this class
        /// </summary>
        private AsmDudeTools()
        {
            //AsmDudeToolsStatic.Output_INFO("AsmDudeTools constructor");

            ThreadHelper.ThrowIfNotOnUIThread();

            #region Initialize ErrorListProvider
            IServiceProvider serviceProvider = new ServiceProvider(Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
            this._errorListProvider = new ErrorListProvider(serviceProvider)
            {
                ProviderName = "Asm Errors",
                ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode)
            };
            #endregion

            this._threadPool = new SmartThreadPool();

            #region load Signature Store and Performance Store
            string path = AsmDudeToolsStatic.Get_Install_Path() + "Resources" + Path.DirectorySeparatorChar;
            {
                string filename_Regular = path + "signature-may2019.txt";
                string filename_Hand = path + "signature-hand-1.txt";
            }
            #endregion

            this.Init_Data();

            this._mnemonics_switched_on = new HashSet<Mnemonic>();
            this.UpdateMnemonicSwitchedOn();

            this._register_switched_on = new HashSet<Rn>();
            this.UpdateRegisterSwitchedOn();
        }

        #region Public Methods

        public bool MnemonicSwitchedOn(Mnemonic mnemonic)
        {
            return this._mnemonics_switched_on.Contains(mnemonic);
        }
        public IEnumerable<Mnemonic> Get_Allowed_Mnemonics()
        {
            return this._mnemonics_switched_on;
        }
        public void UpdateMnemonicSwitchedOn()
        {
            this._mnemonics_switched_on.Clear();
            ISet<Arch> selectedArchs = AsmDudeToolsStatic.Get_Arch_Swithed_On();
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
            }
        }

        public bool RegisterSwitchedOn(Rn reg)
        {
            return this._register_switched_on.Contains(reg);
        }
        public IEnumerable<Rn> Get_Allowed_Registers()
        {
            return this._register_switched_on;
        }
        public void UpdateRegisterSwitchedOn()
        {
            this._register_switched_on.Clear();
            ISet<Arch> selectedArchs = AsmDudeToolsStatic.Get_Arch_Swithed_On();
            foreach (Rn reg in Enum.GetValues(typeof(Rn)))
            {
                if (reg != Rn.NOREG)
                {
                    if (selectedArchs.Contains(RegisterTools.GetArch(reg)))
                    {
                        this._register_switched_on.Add(reg);
                    }
                }
            }
        }

        public ErrorListProvider Error_List_Provider { get { return this._errorListProvider; } }

        public SmartThreadPool Thread_Pool { get { return this._threadPool; } }

        /// <summary>Get the collection of Keywords (in CAPITALS), but NOT mnemonics and registers</summary>
        public IEnumerable<string> Get_Keywords()
        {
            if (this._type == null)
            {
                this.Init_Data();
            }

            return this._type.Keys;
        }

        public AsmTokenType Get_Token_Type_Att(string keyword)
        {
            Debug.Assert(keyword == keyword.ToUpper());
            int length = keyword.Length;
            Debug.Assert(length > 0);

            char firstChar = keyword[0];

            #region Test if keyword is a register
            if (firstChar == '%')
            {
                string keyword2 = keyword.Substring(1);
                Rn reg = RegisterTools.ParseRn(keyword2, true);
                if (reg != Rn.NOREG)
                {
                    return (this.RegisterSwitchedOn(reg))
                       ? AsmTokenType.Register
                       : AsmTokenType.Register; //TODO
                }
            }
            #endregion
            #region Test if keyword is an imm
            if (firstChar == '$')
            {
                return AsmTokenType.Constant;
            }
            #endregion
            #region Test if keyword is an instruction
            {
                (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(keyword, true);
                if (mnemonic != Mnemonic.NONE)
                {
                    return (this.MnemonicSwitchedOn(mnemonic))
                        ? AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic
                        : AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.MnemonicOff;
                }
            }
            #endregion

            return this._type.TryGetValue(keyword, out AsmTokenType tokenType) ? tokenType : AsmTokenType.UNKNOWN;
        }

        public AsmTokenType Get_Token_Type_Intel(string keyword)
        {
            Debug.Assert(keyword == keyword.ToUpper());

            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword, true);
            if (mnemonic != Mnemonic.NONE)
            {
                return (this.MnemonicSwitchedOn(mnemonic))
                    ? AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic
                    : AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.MnemonicOff;
            }
            Rn reg = RegisterTools.ParseRn(keyword, true);
            if (reg != Rn.NOREG)
            {
                return (this.RegisterSwitchedOn(reg))
                    ? AsmTokenType.Register
                    : AsmTokenType.Register; //TODO
            }
            return this._type.TryGetValue(keyword, out AsmTokenType tokenType) ? tokenType : AsmTokenType.UNKNOWN;
        }

        public AssemblerEnum Get_Assembler(string keyword)
        {
            Debug.Assert(keyword == keyword.ToUpper());
            return this._assembler.TryGetValue(keyword, out AssemblerEnum value) ? value : AssemblerEnum.UNKNOWN;
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string Get_Url(Mnemonic mnemonic)
        {
            return "";// this.Mnemonic_Store.GetHtmlRef(mnemonic);
        }

        /// <summary>
        /// get descripton for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an description. Keyword has to be in CAPITALS
        /// </summary>
        public string Get_Description(string keyword)
        {
            Debug.Assert(keyword == keyword.ToUpper());
            return this._description.TryGetValue(keyword, out string description) ? description : "";
        }

        /// <summary>
        /// Get architecture of the provided keyword. Keyword has to be in CAPITALS
        /// </summary>
        public Arch Get_Architecture(string keyword)
        {
            Debug.Assert(keyword == keyword.ToUpper());
            return this._arch.TryGetValue(keyword, out Arch value) ? value : Arch.ARCH_NONE;
        }

        public void Invalidate_Data()
        {
            this._xmlData = null;
            this._type = null;
            this._description = null;
        }

        #endregion Public Methods
        #region Private Methods

        private void Init_Data()
        {
            this._type = new Dictionary<string, AsmTokenType>();
            this._arch = new Dictionary<string, Arch>();
            this._assembler = new Dictionary<string, AssemblerEnum>();
            this._description = new Dictionary<string, string>();
            // fill the dictionary with keywords
            XmlDocument xmlDoc = this.Get_Xml_Data();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found misc with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    this._type[name] = AsmTokenType.Misc;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._description[name] = this.Retrieve_Description(node);
                }
            }

            foreach (XmlNode node in xmlDoc.SelectNodes("//directive"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found directive with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    this._type[name] = AsmTokenType.Directive;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._assembler[name] = this.Retrieve_Assembler(node);
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//register"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found register with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    //this._type[name] = AsmTokenType.Register;
                    this._arch[name] = this.Retrieve_Arch(node);
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined1"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined1 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    this._type[name] = AsmTokenType.UserDefined1;
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined2"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined2 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    this._type[name] = AsmTokenType.UserDefined2;
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined3"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined3 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpper();
                    this._type[name] = AsmTokenType.UserDefined3;
                    this._description[name] = this.Retrieve_Description(node);
                }
            }
        }

        private Arch Retrieve_Arch(XmlNode node)
        {
            try
            {
                XmlAttribute archAttribute = node.Attributes["arch"];
                return (archAttribute == null) ? Arch.ARCH_NONE : ArchTools.ParseArch(archAttribute.Value.ToUpper());
            }
            catch (Exception)
            {
                return Arch.ARCH_NONE;
            }
        }

        private AssemblerEnum Retrieve_Assembler(XmlNode node)
        {
            try
            {
                XmlAttribute archAttribute = node.Attributes["tool"];
                return (archAttribute == null) ? AssemblerEnum.UNKNOWN : AsmSourceTools.ParseAssembler(archAttribute.Value);
            }
            catch (Exception)
            {
                return AssemblerEnum.UNKNOWN;
            }
        }

        private string Retrieve_Description(XmlNode node)
        {
            try
            {
                XmlNode node2 = node.SelectSingleNode("./description");
                return (node2 == null) ? "" : node2.InnerText.Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }

        private XmlDocument Get_Xml_Data()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
            if (this._xmlData == null)
            {
                string filename = AsmDudeToolsStatic.Get_Install_Path() + "Resources" + Path.DirectorySeparatorChar + "AsmDudeData.xml";
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: AsmDudeTools:getXmlData: going to load file \"{0}\"", filename));
                try
                {
                    this._xmlData = new XmlDocument();
                    this._xmlData.Load(filename);
                }
                catch (FileNotFoundException)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: could not find file \"" + filename + "\".");
                }
                catch (XmlException)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: xml error while reading file \"" + filename + "\".");
                }
                catch (Exception e)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
                }
            }
            return this._xmlData;
        }

        public void Dispose()
        {
            this._errorListProvider.Dispose();
            this._threadPool.Dispose();
        }

        #endregion
    }
}