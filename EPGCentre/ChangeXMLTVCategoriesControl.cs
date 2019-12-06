////////////////////////////////////////////////////////////////////////////////// 
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
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;

using DomainObjects;
using XmltvParser;

namespace EPGCentre
{
    internal partial class ChangeXMLTVCategoriesControl : UserControl, IUpdateControl
    {
        /// <summary>
        /// Get the general window heading for the data.
        /// </summary>
        public string Heading { get { return ("EPG Centre - Change XMLTV Program Categories - "); } }
        /// <summary>
        /// Get the default directory.
        /// </summary>
        public string DefaultDirectory { get { return (RunParameters.ConfigDirectory); } }
        /// <summary>
        /// Get the default output file name.
        /// </summary>
        public string DefaultFileName { get { return ("XMLTV Categories"); } }
        /// <summary>
        /// Get the save file filter.
        /// </summary>
        public string SaveFileFilter { get { return ("XMLTV Program Category Files (XMLTV Categories*.cfg)|XMLTV Categories*.cfg"); } }
        /// <summary>
        /// Get the save file title.
        /// </summary>
        public string SaveFileTitle { get { return ("Save EPG Collection XMLTV Program Category File"); } }
        /// <summary>
        /// Get the save file suffix.
        /// </summary>
        public string SaveFileSuffix { get { return ("cfg"); } }

        /// <summary>
        /// Return the state of the data set.
        /// </summary>
        public DataState DataState { get { return (hasDataChanged()); } }

        private BindingList<XmltvProgramCategory> bindingList;

        private string sortedColumnName;
        private string sortedKeyName;
        private bool sortedAscending;

        private bool errors;
        private string currentFileName;

        internal ChangeXMLTVCategoriesControl()
        {
            InitializeComponent();
        }

        internal bool Process(string fileName)
        {
            if (fileName != null)
            {
                bool reply = XmltvProgramCategory.Load(fileName);
                if (!reply)
                    return (false);
                currentFileName = fileName;
            }
            else
                currentFileName = Path.Combine(RunParameters.DataDirectory, CustomProgramCategory.FileName + ".cfg");

            bindingList = new BindingList<XmltvProgramCategory>();
            foreach (XmltvProgramCategory category in XmltvProgramCategory.Categories)
                bindingList.Add(new XmltvProgramCategory(category.CategoryTag, category.FullDescription));

            categoryBindingSource.DataSource = bindingList;
            dgCategories.FirstDisplayedCell = dgCategories.Rows[0].Cells[0];

            return (true);
        }

        private void dgCategories_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgCategories.CurrentCell.ColumnIndex == dgCategories.Columns["descriptionColumn"].Index)
            {
                TextBox textEdit = e.Control as TextBox;
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
            }
            else
            {
                if (dgCategories.CurrentCell.ColumnIndex == dgCategories.Columns["wmcDescriptionColumn"].Index)
                {
                    TextBox textEdit = e.Control as TextBox;
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                    textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                    textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                }
                else
                {
                    if (dgCategories.CurrentCell.ColumnIndex == dgCategories.Columns["dvblogicDescriptionColumn"].Index)
                    {
                        TextBox textEdit = e.Control as TextBox;
                        textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                        textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                        textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                        textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                        textEdit.KeyPress += new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                    }
                    else
                    {
                        if (dgCategories.CurrentCell.ColumnIndex == dgCategories.Columns["dvbviewerDescriptionColumn"].Index)
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
                            if (dgCategories.CurrentCell.ColumnIndex == dgCategories.Columns["categoryColumn"].Index)
                            {
                                TextBox textEdit = e.Control as TextBox;
                                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(textEdit_KeyPressNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(dvblogicTextEdit_KeyPressAlphaNumeric);
                                textEdit.KeyPress -= new KeyPressEventHandler(dvbviewerTextEdit_KeyPressNumeric);
                                textEdit.KeyPress += new KeyPressEventHandler(textEdit_KeyPressAlphaNumeric);
                            }
                        }
                    }
                }
            }
        }

        private void textEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z0-9,!&*()--+'?<>\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void dvblogicTextEdit_KeyPressAlphaNumeric(object sender, KeyPressEventArgs e)
        {
            Regex alphaNumericPattern = new Regex(@"[a-zA-Z,\s\b]");
            e.Handled = !alphaNumericPattern.IsMatch(e.KeyChar.ToString());
        }

        private void textEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void dvbviewerTextEdit_KeyPressNumeric(object sender, KeyPressEventArgs e)
        {
            if ("0123456789,\b".IndexOf(e.KeyChar) == -1)
                e.Handled = true;
        }

        private void dgCategoriesRowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            string categoryTag;
            string description;
            string wmcDescription;
            string dvblogicDescription;
            string dvbviewerDescription; 

            if (dgCategories.Rows[e.RowIndex].Cells["categoryColumn"].Value == null)
                categoryTag = string.Empty;
            else
                categoryTag = dgCategories.Rows[e.RowIndex].Cells["categoryColumn"].Value.ToString().Trim();

            if (dgCategories.Rows[e.RowIndex].Cells["descriptionColumn"].Value == null)
                description = string.Empty;
            else
                description = dgCategories.Rows[e.RowIndex].Cells["descriptionColumn"].Value.ToString().Trim();

            if (dgCategories.Rows[e.RowIndex].Cells["wmcDescriptionColumn"].Value == null)
                wmcDescription = string.Empty;
            else
                wmcDescription = dgCategories.Rows[e.RowIndex].Cells["wmcDescriptionColumn"].Value.ToString().Trim();

            if (dgCategories.Rows[e.RowIndex].Cells["dvblogicDescriptionColumn"].Value == null)
                dvblogicDescription = string.Empty;
            else
                dvblogicDescription = dgCategories.Rows[e.RowIndex].Cells["dvblogicDescriptionColumn"].Value.ToString().Trim();

            if (dgCategories.Rows[e.RowIndex].Cells["dvbviewerDescriptionColumn"].Value == null)
                dvbviewerDescription = string.Empty;
            else
                dvbviewerDescription = dgCategories.Rows[e.RowIndex].Cells["dvbviewerDescriptionColumn"].Value.ToString().Trim();

            
            if (categoryTag == string.Empty &&
                description == string.Empty &&
                wmcDescription == string.Empty &&
                dvblogicDescription == string.Empty &&
                dvbviewerDescription == string.Empty)
            {
                errors = false;
                e.Cancel = true;
                return;
            }

            if (categoryTag == string.Empty)
            {
                MessageBox.Show("Category tag '" + categoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
                return;
            }

            foreach (XmltvProgramCategory category in bindingList)
            {
                if (category.CategoryTag == categoryTag && bindingList.IndexOf(category) != e.RowIndex)
                {
                    MessageBox.Show("Category tag '" + categoryTag + "' already exists.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            if (description == string.Empty)
            {
                MessageBox.Show("The general description for tag '" + categoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
                return;
            }

            if (dvblogicDescription != null)
            {
                bool validValue = DVBLogicProgramCategory.CheckDescription(dvblogicDescription.ToString());
                if (!validValue)
                {
                    MessageBox.Show("The DVBLogic description for tag '" + categoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            if (dvbviewerDescription != null)
            {
                bool validValue = DVBViewerProgramCategory.CheckDescription(dvbviewerDescription.ToString());
                if (!validValue)
                {
                    MessageBox.Show("The DVBViewer description for tag '" + categoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    e.Cancel = true;
                    return;
                }
            }

            errors = false;
        }

        private void dgCategoriesDefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["categoryColumn"].Value = string.Empty;
            e.Row.Cells["descriptionColumn"].Value = string.Empty;
            e.Row.Cells["wmcDescriptionColumn"].Value = string.Empty;
            e.Row.Cells["dvblogicDescriptionColumn"].Value = string.Empty;
            e.Row.Cells["dvbviewerDescriptionColumn"].Value = string.Empty;
        }

        private void dgCategoriesColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (sortedColumnName == null)
            {
                sortedAscending = true;
                sortedColumnName = dgCategories.Columns[e.ColumnIndex].Name;
            }
            else
            {
                if (sortedColumnName == dgCategories.Columns[e.ColumnIndex].Name)
                    sortedAscending = !sortedAscending;
                else
                    sortedColumnName = dgCategories.Columns[e.ColumnIndex].Name;
            }

            Collection<XmltvProgramCategory> sortedCategories = new Collection<XmltvProgramCategory>();

            foreach (XmltvProgramCategory category in bindingList)
            {
                switch (dgCategories.Columns[e.ColumnIndex].Name)
                {
                    case "categoryColumn":
                        addInOrder(sortedCategories, category, sortedAscending, "CategoryTag");
                        break;
                    case "descriptionColumn":
                        addInOrder(sortedCategories, category, sortedAscending, "Description");
                        break;
                    case "wmcDescriptionColumn":
                        addInOrder(sortedCategories, category, sortedAscending, "WMCDescription");
                        break;
                    case "dvblogicDescriptionColumn":
                        addInOrder(sortedCategories, category, sortedAscending, "DVBLogicDescription");
                        break;
                    case "dvbviewerDescriptionColumn":
                        addInOrder(sortedCategories, category, sortedAscending, "DVBViewerDescription");
                        break;
                    default:
                        return;
                }
            }

            bindingList = new BindingList<XmltvProgramCategory>();
            foreach (XmltvProgramCategory category in sortedCategories)
                bindingList.Add(category);

            categoryBindingSource.DataSource = bindingList;
        }

        private void addInOrder(Collection<XmltvProgramCategory> categories, XmltvProgramCategory newCategory, bool sortedAscending, string keyName)
        {
            sortedKeyName = keyName;

            foreach (XmltvProgramCategory oldCategory in categories)
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
            dgCategories.EndEdit();

            if (errors)
                return (DataState.HasErrors);

            if (bindingList.Count != XmltvProgramCategory.Categories.Count)
                return (DataState.Changed);

            foreach (XmltvProgramCategory category in bindingList)
            {
                XmltvProgramCategory existingCategory = XmltvProgramCategory.FindCategory(category.CategoryTag);
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
            dgCategories.EndEdit();

            if (bindingList.Count == 0)
            {
                MessageBox.Show("No categories defined.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            if (bindingList.Count == 1)
            {
                XmltvProgramCategory category = bindingList[0];
                if (category.CategoryTag == string.Empty && category.FullDescription == string.Empty)
                {
                    CustomProgramCategory.Categories.Clear();
                    return (true);
                }
            }

            Collection<XmltvProgramCategory> addedList = new Collection<XmltvProgramCategory>();

            foreach (XmltvProgramCategory category in bindingList)
            {
                bool valid = validateEntry(addedList, category);
                if (!valid)
                    return (false);

                addedList.Add(new XmltvProgramCategory(category.CategoryTag, category.FullDescription));
            }

            XmltvProgramCategory.Categories.Clear();

            foreach (XmltvProgramCategory category in bindingList)
                XmltvProgramCategory.AddCategory(category.CategoryTag, category.FullDescription);

            return (true);
        }

        private bool validateEntry(Collection<XmltvProgramCategory> addedList, XmltvProgramCategory category)
        {
            foreach (XmltvProgramCategory existingCategory in addedList)
            {
                if (existingCategory.CategoryTag == category.CategoryTag)
                {
                    MessageBox.Show("Category tag '" + existingCategory.CategoryTag + "' already exists.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return (false);
                }
            }

            if (category.GeneralDescription == null || category.GeneralDescription == string.Empty)
            {
                MessageBox.Show("The general description for tag '" + category.CategoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            bool validValue = DVBLogicProgramCategory.CheckDescription(category.DVBLogicDescription);
            if (!validValue)
            {
                MessageBox.Show("The DVBLogic description for tag '" + category.CategoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            validValue = DVBViewerProgramCategory.CheckDescription(category.DVBViewerDescription);
            if (!validValue)
            {
                MessageBox.Show("The DVBViewer description for tag '" + category.CategoryTag + "' is incorrect.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        /// <param name="fileName">The name of the file to be saved.</param>
        /// <returns>True if the data has been saved; false otherwise.</returns>
        public bool Save(string fileName)
        {
            string[] parts = fileName.Split(new char[] { Path.DirectorySeparatorChar });
            if (parts.Length < 2 || fileName != Path.Combine(RunParameters.DataDirectory, parts[parts.Length - 1]))
            {
                DialogResult result = MessageBox.Show("The data must be saved to the data directory.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (false);
            }

            string message = XmltvProgramCategory.Save(fileName);

            if (message == null)
            {
                MessageBox.Show("The XMLTV program categories have been saved to '" + fileName + "'", " EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                currentFileName = fileName;
            }
            else
                MessageBox.Show("An error has occurred while writing the XMLTV program categories." + Environment.NewLine + Environment.NewLine + message, "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return (message == null);
        }
    }
}
