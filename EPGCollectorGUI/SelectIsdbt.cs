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

using System;
using System.Windows.Forms;

using DomainObjects;

namespace EPGCentre
{
    internal partial class SelectIsdbt : Form
    {
        /// <summary>
        /// Get the selected provider.
        /// </summary>
        public ISDBTerrestrialProvider Provider { get { return (cboProvider.SelectedItem as ISDBTerrestrialProvider); } }
        
        /// <summary>
        /// Get the selected frequency.
        /// </summary>
        public ISDBTerrestrialFrequency Frequency { get { return (lbFrequency.SelectedItem as ISDBTerrestrialFrequency); } }

        internal SelectIsdbt()
        {
            InitializeComponent();

            ISDBTerrestrialProvider.Load();

            cboProvider.Items.Insert(0, " -- New --");

            foreach (ISDBTerrestrialProvider provider in ISDBTerrestrialProvider.Providers)
            {
                cboProvider.Items.Add(provider);
            }
            if (cboProvider.Items.Count > 1)
                cboProvider.SelectedIndex = 1;
            else
                cboProvider.SelectedIndex = 0;
        }

        private void cboProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbFrequency.Items.Clear();

            lbFrequency.Items.Add(" -- New --");
            lbFrequency.SelectedIndex = 0;

            if (cboProvider.SelectedIndex == 0)
                return;

            foreach (ISDBTerrestrialFrequency frequency in ((ISDBTerrestrialProvider)cboProvider.SelectedItem).Frequencies)
                lbFrequency.Items.Add(frequency);
        }

        private void btOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
