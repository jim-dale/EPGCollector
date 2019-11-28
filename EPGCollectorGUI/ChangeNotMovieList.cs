using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EPGCentre
{
    /// <summary>
    /// Change the list of programmes that are not movies.
    /// </summary>
    public partial class ChangeNotMovieList : Form
    {
        private Collection<string> notMovieList;

        /// <summary>
        /// Initialize a new instance of the ChangeNotMovieList class.
        /// </summary>
        /// <param name="notMovieList">The current list of programme names.</param>
        public ChangeNotMovieList(Collection<string> notMovieList)
        {
            InitializeComponent();

            this.notMovieList = notMovieList;

            foreach (string notMovie in notMovieList)
                lbNotMovieList.Items.Add(notMovie);
        }

        private void lbNotMovieList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbNotMovieList.SelectedItem != null)
                tbCurrentEntry.Text = lbNotMovieList.SelectedItem.ToString();
        }

        private void tbCurrentEntry_TextChanged(object sender, EventArgs e)
        {
            btAdd.Enabled = !string.IsNullOrWhiteSpace(tbCurrentEntry.Text);
            btDelete.Enabled = btAdd.Enabled;
        }

        private void btAdd_Click(object sender, EventArgs e)
        {
            foreach (string existingEntry in lbNotMovieList.Items)
            {
                if (tbCurrentEntry.Text.Trim() == existingEntry)
                {
                    MessageBox.Show("The entry already exists.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            lbNotMovieList.Items.Add(tbCurrentEntry.Text.Trim());
            tbCurrentEntry.Text = string.Empty;

            tbCurrentEntry.Focus();
        }

        private void btDelete_Click(object sender, EventArgs e)
        {
            foreach (string existingEntry in lbNotMovieList.Items)
            {
                if (tbCurrentEntry.Text.Trim() == existingEntry)
                {
                    lbNotMovieList.Items.Remove(existingEntry);
                    tbCurrentEntry.Text = string.Empty;
                    return;
                }
            }

            MessageBox.Show("The entry does not exist.", "EPG Centre", MessageBoxButtons.OK, MessageBoxIcon.Error);                    
        }

        private void btOK_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tbCurrentEntry.Text))
            {
                DialogResult result = MessageBox.Show("The current entry has not been processed" + Environment.NewLine + Environment.NewLine+
                "Do you want to process it?", "EPG Centre", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                    return;
            }

            notMovieList.Clear();

            foreach (string notMovie in lbNotMovieList.Items)
                notMovieList.Add(notMovie);

            this.Close();
            this.DialogResult = DialogResult.OK;
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            this.Close();
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
