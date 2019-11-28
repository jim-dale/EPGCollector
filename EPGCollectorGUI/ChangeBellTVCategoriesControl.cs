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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;

using DomainObjects;
using DVBServices;

namespace EPGCentre
{
    internal partial class ChangeBellTVCategoriesControl : UserControl, IUpdateControl
    {
        /// <summary>
        /// Get the heading.
        /// </summary>
        public string Heading { get { return ("EPG Centre - Change Bell TV Program Categories - "); } }
        /// <summary>
        /// Get the default directory.
        /// </summary>
        public string DefaultDirectory { get { return (RunParameters.ConfigDirectory); } }
        /// <summary>
        /// Get the default output file name.
        /// </summary>
        public string DefaultFileName { get { return ("Bell TV Categories"); } }
        /// <summary>
        /// Get the save file filter.
        /// </summary>
        public string SaveFileFilter { get { return ("Bell TV Program Category Files (Bell TV Categories*.cfg)|Bell TV Categories*.cfg"); } }
        /// <summary>
        /// Get the save file title.
        /// </summary>
        public string SaveFileTitle { get { return ("Save EPG Collection Bell TV Program Category File"); } }
        /// <summary>
        /// Get the save file suffix.
        /// </summary>
        public string SaveFileSuffix { get { return ("cfg"); } }

        /// <summary>
        /// Return the state of the data set.
        /// </summary>
        public DataState DataState { get { return (hasDataChanged()); } }

        private BindingList<BellTVProgramCategory> bindingList;

        private string sortedColumnName;
        private string sortedKeyName;
        private bool sortedAscending;

        private bool errors;
        private string currentFileName;

        internal ChangeBellTVCategoriesControl()
        {
            InitializeComponent();
        }

        internal bool Process(string fileName)
        {
            bool reply = BellTVProgramCategory.Load(fileName);
            if (!reply)
                return (false);

            currentFileName = fileName;

            bindingList = new BindingList<BellTVProgramCategory>();
            foreach (BellTVProgramCategory category in BellTVProgramCategory.Categories)
                bindingList.Add(new BellTVProgramCategory(category.CategoryID, category.SubCategoryID, category.FullDescription));

            contentBindingSource.DataSource = bindingList;
            dgContents.FirstDisplayedCell = dgContents.Rows[0].Cells[0];

            return (true);
        }

        private void dgContents_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgContents.CurrentCell.ColumnIndex == dgContents.Columns["descriptionColumn"].Index)
            {
                TextBox textEdit = e.Control as TextBox;
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
            }
            else
            {
                if (dgContents.CurrentCell.ColumnIndex == dgContents.Columns["wmcDescriptionColumn"].Index)
                {
                    TextBox textEdit = e.Control as TextBox;
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                }
                else
                {
                    if (dgContents.CurrentCell.ColumnIndex == dgContents.Columns["dvblogicDescriptionColumn"].Index)
                    {
                        TextBox textEdit = e.Control as TextBox;
                        textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                        textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                        textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                        textEdit.KeyPress += new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                    }
                    else
                    {
                        if (dgContents.CurrentCell.ColumnIndex == dgContents.Columns["dvbviewerDescriptionColumn"].Index)
                        {
                            TextBox textEdit = e.Control as TextBox;
                            textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                            textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                            textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                            textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                            textEdit.KeyPress += new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                        }
                        else
                        {
                            if (dgContents.CurrentCell.ColumnIndex == dgContents.Columns["categoryColumn"].Index)
                            {
                                TextBox textEdit = e.Control as TextBox;
                                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                                textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressNumeric);
                            }
                        }
                    }
                }
            }
        }

        private void textEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z0-9,!&*()--+'?\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void dvblogicTextEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z,\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void dvbviewerTextEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789,\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void textEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void dgContentsRowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            string contentIDString;
            string description;
            string wmcDescription;
            string dvblogicDescription;
            string dvbviewerDescription;

            if (dgContents.Rows[e.RowIndex].Cells["contentIDColumn"].Value == null)
                contentIDString = string.Empty;
            else
                contentIDString = dgContents.Rows[e.RowIndex].Cells["contentIDColumn"].Value.ToString().Trim();

            if (dgContents.Rows[e.RowIndex].Cells["descriptionColumn"].Value == null)
                description = string.Empty;
            else
                description = dgContents.Rows[e.RowIndex].Cells["descriptionColumn"].Value.ToString().Trim();

            if (dgContents.Rows[e.RowIndex].Cells["wmcDescriptionColumn"].Value == null)
                wmcDescription = string.Empty;
            else
                wmcDescription = dgContents.Rows[e.RowIndex].Cells["wmcDescriptionColumn"].Value.ToString().Trim();

            if (dgContents.Rows[e.RowIndex].Cells["dvblogicDescriptionColumn"].Value == null)
                dvblogicDescription = string.Empty;
            else
                dvblogicDescription = dgContents.Rows[e.RowIndex].Cells["dvblogicDescriptionColumn"].Value.ToString().Trim();

            if (dgContents.Rows[e.RowIndex].Cells["dvbviewerDescriptionColumn"].Value == null)
                dvbviewerDescription = string.Empty;
            else
                dvbviewerDescription = dgContents.Rows[e.RowIndex].Cells["dvbviewerDescriptionColumn"].Value.ToString().Trim();

            if (contentIDString == string.Empty &&
                description == string.Empty &&
                wmcDescription == string.Empty &&
                dvblogicDescription == string.Empty &&
                dvbviewerDescription == string.Empty)
            {
                errors = false;
                e.Cancel = true;
                return;
            }

            errors = true;
            int categoryID;            

            try
            {
                categoryID = Int32.Parse(contentIDString);
            }
            catch (FormatException)
            {
                MessageBox.Show("Category ID '" + contentIDString + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
                return;
            }

            foreach (BellTVProgramCategory category in bindingList)
            {
                if (category.CategoryID == categoryID && bindingList.IndexOf(category) != e.RowIndex)
                {
                    MessageBox.Show("Category " + contentIDString + " already exists.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            if (description == string.Empty)
            {
                MessageBox.Show("The Bell TV description for category " + contentIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
                return;
            }

            if (dvblogicDescription != null)
            {
                bool validValue = DVBLogicProgramCategory.CheckDescription(dvblogicDescription.ToString());
                if (!validValue)
                {
                    MessageBox.Show("The DVBLogic description for category " + contentIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            if (dvbviewerDescription != null)
            {
                bool validValue = DVBViewerProgramCategory.CheckDescription(dvbviewerDescription.ToString());
                if (!validValue)
                {
                    MessageBox.Show("The DVBViewer description for category " + contentIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            errors = false;
        }

        private void dgContentsDefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["contentIDColumn"].Value = string.Empty;
            e.Row.Cells["descriptionColumn"].Value = string.Empty;
            e.Row.Cells["wmcDescriptionColumn"].Value = string.Empty;
            e.Row.Cells["dvblogicDescriptionColumn"].Value = string.Empty;
            e.Row.Cells["dvbviewerDescriptionColumn"].Value = string.Empty;
        }

        private void dgContentsColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (sortedColumnName == null)
            {
                sortedAscending = true;
                sortedColumnName = dgContents.Columns[e.ColumnIndex].Name;
            }
            else
            {
                if (sortedColumnName == dgContents.Columns[e.ColumnIndex].Name)
                    sortedAscending = !sortedAscending;
                else
                    sortedColumnName = dgContents.Columns[e.ColumnIndex].Name;
            }

            Collection<BellTVProgramCategory> sortedContents = new Collection<BellTVProgramCategory>();

            foreach (BellTVProgramCategory category in bindingList)
            {
                switch (dgContents.Columns[e.ColumnIndex].Name)
                {
                    case "contentIDColumn":
                        addInOrder(sortedContents, category, sortedAscending, "CategoryID");
                        break;
                    case "descriptionColumn":
                        addInOrder(sortedContents, category, sortedAscending, "Description");
                        break;
                    case "wmcDescriptionColumn":
                        addInOrder(sortedContents, category, sortedAscending, "WMCDescription");
                        break;
                    case "dvblogicDescriptionColumn":
                        addInOrder(sortedContents, category, sortedAscending, "DVBLogicDescription");
                        break;
                    case "dvbviewerDescriptionColumn":
                        addInOrder(sortedContents, category, sortedAscending, "DVBViewerDescription");
                        break;
                    default:
                        return;
                }
            }

            bindingList = new BindingList<BellTVProgramCategory>();
            foreach (BellTVProgramCategory category in sortedContents)
                bindingList.Add(category);

            contentBindingSource.DataSource = bindingList;
        }

        private void addInOrder(Collection<BellTVProgramCategory> categories, BellTVProgramCategory newCategory, bool sortedAscending, string keyName)
        {
            sortedKeyName = keyName;

            foreach (BellTVProgramCategory oldCategory in categories)
            {
                if (sortedAscending)
                {
                    if (oldCategory.CompareForSorting(newCategory, keyName) > 0)
                    {
                        categories.Insert(categories.IndexOf(oldCategory), newCategory);
                        return;
                    }
                }
                else
                {
                    if (oldCategory.CompareForSorting(newCategory, keyName) < 0)
                    {
                        categories.Insert(categories.IndexOf(oldCategory), newCategory);
                        return;
                    }
                }
            }

            categories.Add(newCategory);
        }

        private DataState hasDataChanged()
        {
            dgContents.EndEdit();

            if (errors)
                return (DataState.HasErrors);

            if (bindingList.Count != BellTVProgramCategory.Categories.Count)
                return (DataState.Changed);

            foreach (BellTVProgramCategory category in bindingList)
            {
                BellTVProgramCategory existingCategory = BellTVProgramCategory.FindCategory(category.CategoryID, category.SubCategoryID);
                if (existingCategory == null)
                    return (DataState.Changed);

                if (existingCategory.FullDescription != category.FullDescription)
                    return (DataState.Changed);
            }

            return (DataState.NotChanged);
        }

        /// <summary>
        /// Validate the data and set up to save it.
        /// </summary>
        /// <returns>True if the data can be saved; false otherwise.</returns>
        public bool PrepareToSave()
        {
            dgContents.EndEdit();

            if (bindingList.Count == 0)
            {
                MessageBox.Show("No categories defined.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            Collection<BellTVProgramCategory> addedList = new Collection<BellTVProgramCategory>();

            foreach (BellTVProgramCategory category in bindingList)
            {
                bool valid = validateEntry(addedList, category);
                if (!valid)
                    return (false);

                addedList.Add(new BellTVProgramCategory(category.CategoryID, category.SubCategoryID, category.FullDescription));
            }

            BellTVProgramCategory.Categories.Clear();

            foreach (BellTVProgramCategory category in bindingList)
                BellTVProgramCategory.AddCategory(category.CategoryID, category.SubCategoryID, category.FullDescription);

            return (true);
        }

        private bool validateEntry(Collection<BellTVProgramCategory> addedList, BellTVProgramCategory category)
        {
            foreach (BellTVProgramCategory existingContent in addedList)
            {
                if (existingContent.CategoryID == category.CategoryID)
                {
                    MessageBox.Show("Category " + existingContent.CategoryIDString + " already exists.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return (false);
                }
            }

            if (category.FullDescription == null || category.FullDescription == string.Empty)
            {
                MessageBox.Show("The Bell TV description for category " + category.CategoryIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            bool validValue = DVBLogicProgramCategory.CheckDescription(category.DVBLogicDescription);
            if (!validValue)
            {
                MessageBox.Show("The DVBLogic description for category " + category.CategoryIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            validValue = DVBViewerProgramCategory.CheckDescription(category.DVBViewerDescription);
            if (!validValue)
            {
                MessageBox.Show("The DVBViewer description for category " + category.CategoryIDString + " is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            return (true);
        }

        /// <summary>
        /// Save the current data set to the original file.
        /// </summary>
        /// <returns>True if the data has been saved; false otherwise.</returns>
        public bool Save()
        {
            FileInfo fileInfo = new FileInfo(currentFileName);
            return (Save(Path.Combine(RunParameters.DataDirectory, fileInfo.Name)));
        }

        /// <summary>
        /// Save the current data set to a specified file.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <returns>True if the data has been saved; false otherwise.</returns>
        public bool Save(string fileName)
        {
            string[] parts = fileName.Split(new char[] { Path.DirectorySeparatorChar });
            if (parts.Length < 2 || fileName != Path.Combine(RunParameters.DataDirectory, parts[parts.Length - 1]))
            {
                DialogResult result = MessageBox.Show("The data must be saved to the data directory.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            string message = BellTVProgramCategory.Save(fileName);

            if (message == null)
            {
                MessageBox.Show("The Bell TV program categories have been saved to '" + fileName + "'", " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                currentFileName = fileName;
            }
            else
                MessageBox.Show("An error has occurred while writing the Bell TV program categories." + Environment.NewLine + Environment.NewLine + message, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return (message == null);
        }
    }
}
