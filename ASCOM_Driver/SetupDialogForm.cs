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

            if (bleDevicesComboBox.SelectedItem != null && bleDevicesComboBox.SelectedItem is ComboboxItem)
            {
                calibrator.bleDeviceId = (bleDevicesComboBox.SelectedItem as ComboboxItem).Value;
                calibrator.bleDeviceName = (bleDevicesComboBox.SelectedItem as ComboboxItem).Text;
            }
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

            if (calibrator.bleDeviceId != string.Empty)
            {
                if (calibrator.bleDeviceName != string.Empty)
                {
                    currentlyConfiguredDeviceLabel.Text = calibrator.bleDeviceName;
                }
                else
                {
                    currentlyConfiguredDeviceLabel.Text = "<unknown device>";
                }
            }
            else
            {
                currentlyConfiguredDeviceLabel.Text = "<no device configured>";
            }

            bleDevicesComboBox.Items.Add("Detecting nearby BLE devices, please wait...");
            bleDevicesComboBox.SelectedIndex = 0;
            bleDevicesComboBox.Enabled = false;

            // Query for extra properties you want returned
            string[] requestedProperties = {
                "System.ItemNameDisplay"
            };

            deviceWatcher = DeviceInformation.CreateWatcher(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                requestedProperties,
                DeviceInformationKind.Device
            );

            // Register event handlers before starting the watcher.
            // Added, Updated and Removed are required to get all nearby devices
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;

            // EnumerationCompleted and Stopped are optional to implement.
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;

            // Start the watcher.
            deviceWatcher.Start();
        }

        private void SetupDialogForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (deviceWatcher != null)
            {
                try
                {
                    deviceWatcher.Stop();
                }
                catch (Exception)
                {
                    // Ignore
                }

                deviceWatcher = null;
            }
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            if (String.IsNullOrEmpty(deviceInfo.Name) || devices.ContainsKey(deviceInfo.Id))
            {
                return;
            }

            devices.Add(deviceInfo.Id, deviceInfo.Name);

            // Debug.WriteLine(String.Format("Added   {0} {1}", deviceInfo.Id, deviceInfo.Name));
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            if (devices.ContainsKey(deviceInfoUpdate.Id))
            {
                devices.Remove(deviceInfoUpdate.Id);
            }

            // Debug.WriteLine(String.Format("Removed {0} {1}", deviceInfoUpdate.Id, ""));
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher deviceWatcher, object e)
        {
            deviceWatcher.Stop();

            // Debug.WriteLine("Enumeration completed.");

            this.BeginInvoke((MethodInvoker)(() =>
            {
                bleDevicesComboBox.Items.Clear();
                bleDevicesComboBox.Enabled = true;

                bool selectedDeviceFound = false;
                foreach (KeyValuePair<string, string> kv in devices)
                {
                    ComboboxItem item = new ComboboxItem();
                    item.Text = kv.Value;
                    item.Value = kv.Key;
                    bleDevicesComboBox.Items.Add(item);
                    if (kv.Key == calibrator.bleDeviceId)
                    {
                        bleDevicesComboBox.SelectedItem = item;
                        selectedDeviceFound = true;
                    }
                }

                if (!selectedDeviceFound)
                {
                    bleDevicesComboBox.SelectedIndex = 0;
                }
            }));
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            Console.WriteLine("No longer watching for devices.");
        }
    }

    public class ComboboxItem
    {
        public string Text { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
