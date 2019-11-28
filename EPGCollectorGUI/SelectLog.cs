﻿////////////////////////////////////////////////////////////////////////////////// 
//                                                                              //
//      Copyright (C) 2005-2014 nzsjb                                           //
//                                                                              //
//  This Program is free software; you can redistribute it and/or modify        //
//  it under the terms of the GNU General Public License as published by        //
//  the Free Software Foundation; either version 2, or (at your option)         //
//  any later version.                                                          //
//                                                                              //
//  This Program is distributed in the hope that it will be useful,             //
//  but WITHOUT ANY WARRANTY; without even the implied warranty of              //
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                //
//  GNU General Public License for more details.                                //
//                                                                              //
//  You should have received a copy of the GNU General Public License           //
//  along with GNU Make; see the file COPYING.  If not, write to                //
//  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.       //
//  http://www.gnu.org/copyleft/gpl.html                                        //
//                                                                              //  
//////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Windows.Forms;

using DomainObjects;

namespace EPGCentre
{
    internal partial class SelectLog : Form
    {
        internal string File { get { return ((string)lbLogs.SelectedItem); } }

        internal SelectLog()
        {
            InitializeComponent();

            DirectoryInfo directoryInfo = new DirectoryInfo(RunParameters.DataDirectory);

            foreach (FileInfo fileInfo in directoryInfo.GetFiles("*.log"))
                lbLogs.Items.Add(fileInfo.Name.Substring(0, fileInfo.Name.Length - 4));

            if (lbLogs.Items.Count > 0)
                lbLogs.SelectedIndex = 0;
        }

        private void lbLogsDoubleClick(object sender, MouseEventArgs e)
        {
            int index = lbLogs.IndexFromPoint(e.Location);

            if (index != ListBox.NoMatches)
            {
                this.Close();
                this.DialogResult = DialogResult.OK;
            }
        }
    }
}
