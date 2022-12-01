/*
 * SetupDialogForm.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.Utilities;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ASCOM.DarkSkyGeek
{
    // Form not registered for COM!
    [ComVisible(false)]

    public partial class SetupDialogForm : Form
    {
        // Holder for a reference to the driver's trace logger
        TraceLogger tl;

        public SetupDialogForm(TraceLogger tlDriver)
        {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialogue
            tl = tlDriver;

            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            // TODO
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void InitUI()
        {
            // TODO
        }

        private void BrowseToHomepage(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://github.com/jlecomte/ascom-spectral-calibrator");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }
    }
}
