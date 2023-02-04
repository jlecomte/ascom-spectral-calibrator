/*
 * SetupDialogForm.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using System.Diagnostics;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using System.Collections.Generic;

namespace ASCOM.DarkSkyGeek
{
    // Form not registered for COM!
    [ComVisible(false)]

    public partial class SetupDialogForm : Form
    {
        SpectralCalibrator calibrator;
        DeviceWatcher deviceWatcher;
        Dictionary<string, string> devices = new Dictionary<string, string>();

        public SetupDialogForm(SpectralCalibrator calibrator)
        {
            InitializeComponent();
            this.calibrator = calibrator;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            calibrator.tl.Enabled = chkTrace.Checked;
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void BrowseToHomepage(object sender, EventArgs e)
        {
            try
            {
                Process.Start("https://github.com/jlecomte/ascom-spectral-calibrator");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void SetupDialogForm_Load(object sender, EventArgs e)
        {
            chkTrace.Checked = calibrator.tl.Enabled;
        }
    }
}
