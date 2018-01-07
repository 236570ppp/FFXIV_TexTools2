﻿// FFXIV TexTools
// Copyright © 2017 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FFXIV_TexTools2.Helpers;
using FFXIV_TexTools2.Resources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FFXIV_TexTools2.Views
{
    /// <summary>
    /// Interaction logic for ProblemCheckView.xaml
    /// </summary>
    public partial class ProblemCheckView : Window
    {
        List<int> modOffsetList = new List<int>();
        List<int> originalOffsetList = new List<int>();

        public ProblemCheckView()
        {
            InitializeComponent();

            AddText("Intializing Problem Check....\n\n", "Black");

            AddText("Checking Index Dat Values....\n", "Blue");
            if (CheckIndex())
            {
                AddText("\nErrors found attempting to repair....\n", "Blue");
                if (!Helper.IsIndexLocked(false))
                {
                    if (FixIndex())
                    {
                        AddText("Repairs Complete\n", "Green");
                        CheckIndex();
                    }
                }
                else
                {
                    AddText("\nCannot run repairs with game open. Please exit the game and run Check For Problems again. \n", "Red");

                }
            }

            AddText("\nChecking Modlist....\n", "Blue");
            if (CheckModlist())
            {
                AddText("\nThe original offset is not valid and will cause issues when reverting, please use Help > Start Over.\n", "Orange");
            }

            AddText("\nChecking Index Values....\n", "Blue");
            if (CheckIndexValues())
            {
                AddText("\nThere are modded entries in the index that are unknown to TexTools, please use Help > Start Over.\n", "Orange");
            }

            AddText("\nChecking Index Backups....\n", "Blue");
            if (CheckBackups())
            {
                AddText("\nBackup files are corrupt, request new backups from the TexTools discord.\n", "Orange");
            }


        }

        /// <summary>
        /// Checks the index for the number of dats the game will attempt to read
        /// </summary>
        /// <returns></returns>
        private bool CheckIndex()
        {
            bool problemFound = false;

            foreach (var indexFile in Info.ModIndexDict)
            {
                var indexPath = string.Format(Info.indexDir, indexFile.Key);
                var index2Path = string.Format(Info.index2Dir, indexFile.Key);

                AddText("\t" +indexFile.Key + ".win32.index", "Black");

                try
                {
                    using (BinaryReader br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(1104, SeekOrigin.Begin);

                        var numDats = br.ReadInt16();

                        if (numDats != indexFile.Value)
                        {
                            AddText("\t\u2716\n", "Red");

                            problemFound = true;
                        }
                        else
                        {
                            AddText("\t\u2714\n", "Green");

                        }
                    }
                }
                catch (Exception e)
                {
                    AddText("\t\nError: " + e.Message + "\n", "Red");
                }

                AddText("\t" + indexFile.Key + ".win32.index2", "Black");
                try
                {
                    using (BinaryReader br = new BinaryReader(File.OpenRead(index2Path)))
                    {
                        br.BaseStream.Seek(1104, SeekOrigin.Begin);

                        var numDats = br.ReadInt16();

                        if (numDats != indexFile.Value)
                        {
                            AddText("\t\u2716\n", "Red");

                            problemFound = true;
                        }
                        else
                        {
                            AddText("\t\u2714\n", "Green");
                        }
                    }
                }
                catch (Exception e)
                {
                    AddText("\t\nError: " + e.Message + "\n", "Red");
                }
            }

            return problemFound;
        }

        private bool CheckBackups()
        {
            bool problem = false;

            if (Directory.Exists(Info.indexBackupDir))
            {
                foreach (var indexFile in Info.ModIndexDict)
                {
                    var indexPath = string.Format(Info.indexBackupFile, indexFile.Key);
                    var index2Path = string.Format(Info.index2BackupFile, indexFile.Key);
                    bool check = false;

                    AddText("\t" + indexFile.Key + ".win32.index", "Black");

                    try
                    {
                        using (BinaryReader br = new BinaryReader(File.OpenRead(indexPath)))
                        {
                            br.BaseStream.Seek(1036, SeekOrigin.Begin);
                            int numOfFiles = br.ReadInt32();

                            br.BaseStream.Seek(2048, SeekOrigin.Begin);
                            for (int i = 0; i < numOfFiles; br.ReadBytes(4), i += 16)
                            {
                                br.ReadBytes(8);
                                int offset = br.ReadInt32();

                                int datNum = (offset & 0x000f) / 2;

                                if (indexFile.Key.Equals(Strings.ItemsDat))
                                {
                                    if (datNum > 3)
                                    {
                                        AddText("\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                                else if (indexFile.Key.Equals(Strings.UIDat))
                                {
                                    if(datNum > 0)
                                    {
                                        AddText("\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                                else if(offset == 0)
                                {
                                    AddText("\u2716\n", "Red");
                                    problem = true;
                                    check = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AddText("\t\nError: " + e.Message + "\n", "Red");
                    }
                    if (!check)
                    {
                        AddText("\t\u2714\n", "Green");
                    }

                    check = false;
                    AddText("\t" + indexFile.Key + ".win32.index2", "Black");

                    try
                    {
                        using (BinaryReader br = new BinaryReader(File.OpenRead(index2Path)))
                        {
                            br.BaseStream.Seek(1036, SeekOrigin.Begin);
                            int numOfFiles = br.ReadInt32();

                            br.BaseStream.Seek(2048, SeekOrigin.Begin);
                            for (int i = 0; i < numOfFiles; i += 8)
                            {
                                br.ReadBytes(4);
                                int offset = br.ReadInt32();
                                int datNum = (offset & 0x000f) / 2;

                                if (indexFile.Key.Equals(Strings.ItemsDat))
                                {
                                    if (datNum > 3)
                                    {
                                        AddText("\t" + i + "\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                                else if (indexFile.Key.Equals(Strings.UIDat))
                                {
                                    if (datNum > 0)
                                    {
                                        AddText("\t" + i + "\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                                else if (offset == 0)
                                {
                                    AddText("\u2716\n", "Red");
                                    problem = true;
                                    check = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        AddText("\t\nError: " + e.Message + "\n", "Red");
                    }
                    if (!check)
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
            }
            else
            {
                AddText("\t\nNo Index Backups Found!\n", "Red");

            }
            return problem;
        }

        private bool CheckModlist()
        {
            string line;
            JsonEntry modEntry = null;
            bool check = false;

            if(File.ReadLines(Info.modListDir).Count() > 0)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(Info.modListDir))
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            modEntry = JsonConvert.DeserializeObject<JsonEntry>(line);
                            if (!modEntry.name.Equals(""))
                            {
                                var originalOffset = modEntry.originalOffset / 8;
                                var modOffset = modEntry.modOffset / 8;

                                int datNum = (originalOffset & 0xF) / 2;

                                var tabs = "";

                                if (Path.GetFileName(modEntry.fullPath).Length < 21)
                                {
                                    tabs = "\t";
                                }

                                AddText("\t" + Path.GetFileName(modEntry.fullPath) + tabs, "Black");

                                if (modEntry.datFile.Equals(Strings.ItemsDat))
                                {
                                    if (datNum > 3 || originalOffset == 0)
                                    {
                                        AddText("\u2716\n", "Red");
                                        check = true;
                                    }
                                    else if (modOffset == 0)
                                    {
                                        AddText("\u2716\n", "Red");
                                        AddText("\tMod Offset for the above texture was 0, Disable from File > Modlist and reimport.\n", "Red");
                                        check = true;
                                    }
                                    else
                                    {
                                        AddText("\t\u2714\n", "Green");
                                    }
                                }
                                else if (modEntry.datFile.Equals(Strings.UIDat))
                                {
                                    if (datNum > 0 || originalOffset == 0)
                                    {
                                        AddText("\u2716\n", "Red");
                                        check = true;
                                    }
                                    else if (modOffset == 0)
                                    {
                                        AddText("\u2716\n", "Red");
                                        AddText("\tMod Offset for the above texture was 0, Disable from File > Modlist and reimport.\n", "Red");
                                        check = true;
                                    }
                                    else
                                    {
                                        AddText("\t\u2714\n", "Green");
                                    }
                                }

                                modOffsetList.Add(modOffset);
                                originalOffsetList.Add(originalOffset);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AddText("\t\nError: " + e.Message + "\n", "Red");
                }
            }
            else
            {
                AddText("\tNo entries found in modlist.\n", "Orange");
            }



            return check;
        }

        private bool CheckIndexValues()
        {
            bool problem = false;


            foreach (var indexFile in Info.ModIndexDict)
            {
                var indexPath = string.Format(Info.indexDir, indexFile.Key);
                var index2Path = string.Format(Info.index2Dir, indexFile.Key);
                bool check = false;

                AddText("\t" + indexFile.Key + ".win32.index\t", "Black");

                try
                {
                    using (BinaryReader br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(1036, SeekOrigin.Begin);
                        int numOfFiles = br.ReadInt32();

                        br.BaseStream.Seek(2048, SeekOrigin.Begin);
                        for (int i = 0; i < numOfFiles; br.ReadBytes(4), i += 16)
                        {
                            br.ReadBytes(8);
                            int offset = br.ReadInt32();

                            int datNum = (offset & 0x000f) / 2;

                            if (indexFile.Key.Equals(Strings.ItemsDat))
                            {
                                if (datNum == 4)
                                {
                                    if (!modOffsetList.Contains(offset))
                                    {
                                        AddText(" \u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                            }
                            else if (indexFile.Key.Equals(Strings.UIDat))
                            {
                                if (datNum == 1)
                                {
                                    if (!modOffsetList.Contains(offset))
                                    {
                                        AddText("\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                            }
                            else if (offset == 0)
                            {
                                AddText("\u2716\n", "Red");
                                problem = true;
                                check = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AddText("\t\nError: " + e.Message + "\n", "Red");
                }
                if (!check)
                {
                    AddText("\u2714\n", "Green");
                }

                check = false;
                AddText("\t" + indexFile.Key + ".win32.index2\t", "Black");

                try
                {
                    using (BinaryReader br = new BinaryReader(File.OpenRead(index2Path)))
                    {
                        br.BaseStream.Seek(1036, SeekOrigin.Begin);
                        int numOfFiles = br.ReadInt32();

                        br.BaseStream.Seek(2048, SeekOrigin.Begin);
                        for (int i = 0; i < numOfFiles; i += 8)
                        {
                            br.ReadBytes(4);
                            int offset = br.ReadInt32();
                            int datNum = (offset & 0x000f) / 2;

                            if (indexFile.Key.Equals(Strings.ItemsDat))
                            {
                                if (datNum == 4)
                                {
                                    if (!modOffsetList.Contains(offset))
                                    {
                                        AddText("\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                            }
                            else if (indexFile.Key.Equals(Strings.UIDat))
                            {
                                if (datNum == 1)
                                {
                                    if (!modOffsetList.Contains(offset))
                                    {
                                        AddText("\u2716\n", "Red");
                                        problem = true;
                                        check = true;
                                        break;
                                    }
                                }
                            }
                            else if (offset == 0)
                            {
                                AddText("\u2716\n", "Red");
                                problem = true;
                                check = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    AddText("\t\nError: " + e.Message + "\n", "Red");
                }
                if (!check)
                {
                    AddText("\u2714\n", "Green");
                }
            }

            return problem;
        }

        /// <summary>
        /// Changes the number of dats the game will attempt to read to 5
        /// </summary>
        private bool FixIndex()
        {
            foreach (var indexFile in Info.ModIndexDict)
            {
                var indexPath = string.Format(Info.indexDir, indexFile.Key);
                var index2Path = string.Format(Info.index2Dir, indexFile.Key);

                try
                {
                    using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(indexPath)))
                    {
                        bw.BaseStream.Seek(1104, SeekOrigin.Begin);
                        bw.Write((byte)indexFile.Value);
                    }
                }
                catch (Exception e)
                {
                    AddText("\nError: " + e.Message + "\n", "Red");
                    return false;
                }

                try
                {
                    using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(index2Path)))
                    {
                        bw.BaseStream.Seek(1104, SeekOrigin.Begin);
                        bw.Write((byte)indexFile.Value);
                    }
                }
                catch (Exception e)
                {
                    AddText("\nError: " + e.Message + "\n", "Red");
                    return false;
                }
            }

            return true;
        }


        private void AddText(string text, string color)
        {
            BrushConverter bc = new BrushConverter();
            TextRange tr = new TextRange(rtb.Document.ContentEnd, rtb.Document.ContentEnd);
            tr.Text = text;
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFromString(color));
            }
            catch (FormatException) { }
        }

        private void CloseProblemView_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}