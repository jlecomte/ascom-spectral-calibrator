/*
 * SpectralCalibratorDriver.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace ASCOM.DarkSkyGeek
{
    //
    // Your driver's DeviceID is ASCOM.DarkSkyGeek.SpectralCalibrator
    //
    // The Guid attribute sets the CLSID for ASCOM.DarkSkyGeek.SpectralCalibrator
    // The ClassInterface/None attribute prevents an empty interface called
    // _DarkSkyGeek from being created and used as the [default] interface
    //

    /// <summary>
    /// DarkSkyGeek’s Spectral Calibrator ASCOM Switch Driver.
    /// </summary>
    [Guid("73085480-9405-4674-8e83-391835721abe")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SpectralCalibrator : ISwitchV2
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        private const string driverID = "ASCOM.DarkSkyGeek.SpectralCalibrator";

        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private const string deviceName = "DarkSkyGeek’s Spectral Calibrator";

        // Constants used for Profile persistence
        private const string traceStateProfileName = "Trace Level";
        private const string traceStateDefault = "false";

        private const string bleDeviceIdProfileName = "BLE Device ID";
        private const string bleDeviceIdDefault = "";

        private const string bleDeviceNameProfileName = "BLE Device Name";
        private const string bleDeviceNameDefault = "";

        // Variables to hold the current device configuration
        internal string bleDeviceId = string.Empty;
        internal string bleDeviceName = string.Empty;

        // Constants
        private Guid BLE_UUID = new Guid("f2d9de7d-6a59-40a3-bb7f-0c31970529bf");

        private const byte CALIBRATOR_OFF = 0x00;
        private const byte CALIBRATOR_ON  = 0x01;
        private const byte ON_OFF_CYCLE   = 0x02;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        internal TraceLogger tl;

        /// <summary>
        /// Variable to hold the physical BLE device we are communicating with.
        /// </summary>
        private BluetoothLEDevice bleDevice;

        /// <summary>
        /// Variable to hold the physical BLE device characteristic we are interacting with.
        /// </summary>
        private GattCharacteristic bleCharacteristic;

        /// <summary>
        /// Initializes a new instance of the <see cref="DarkSkyGeek"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public SpectralCalibrator()
        {
            tl = new TraceLogger("", "DarkSkyGeek");
            ReadProfile();
            tl.LogMessage("SpectralCalibrator", "Starting initialization");
            connectedState = false;
            tl.LogMessage("SpectralCalibrator", "Completed initialization");
        }

        //
        // PUBLIC COM INTERFACE ISwitchV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm(this))
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning [\"SetDutyCycle\"]");
                return new ArrayList()
                {
                    1, "SetDutyCycle"
                };
            }
        }

        /// <summary>Invokes the specified device-specific custom action.</summary>
        /// <param name="ActionName">A well known name agreed by interested parties that represents the action to be carried out.</param>
        /// <param name="ActionParameters">List of required parameters or an <see cref="String.Empty">Empty String</see> if none are required.</param>
        /// <returns>A string response. The meaning of returned strings is set by the driver author.
        /// <para>Suppose filter wheels start to appear with automatic wheel changers; new actions could be <c>QueryWheels</c> and <c>SelectWheel</c>. The former returning a formatted list
        /// of wheel names and the second taking a wheel name and making the change, returning appropriate values to indicate success or failure.</para>
        /// </returns>
        public string Action(string actionName, string actionParameters)
        {
            switch (actionName.ToUpper())
            {
                case "SETDUTYCYCLE":
                    int value;
                    try
                    {
                        value = int.Parse(actionParameters);
                    }
                    catch (FormatException)
                    {
                        throw new ASCOM.InvalidValueException($"Unable to parse '{actionParameters}' as an integer.");
                    }
                    if (value < 0 || value > 100)
                    {
                        throw new ASCOM.InvalidValueException($"Duty cycle must be an integer between 0 and 100.");
                    }
                    StartOnOffCycle(value);
                    return string.Empty;
                default:
                    LogMessage("Action", "Action {0} is not implemented by this driver", actionName);
                    throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
            }
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and does not wait for a response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        public void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a boolean response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the interpreted boolean response received from the device.
        /// </returns>
        public bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a string response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the string response received from the device.
        /// </returns>
        public string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        /// <summary>
        /// Dispose the late-bound interface, if needed. Will release it via COM
        /// if it is a COM object, else if native .NET will just dereference it
        /// for GC.
        /// </summary>
        public void Dispose()
        {
            Connected = false;
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
        }

        /// <summary>
        /// Set True to connect to the device hardware. Set False to disconnect from the device hardware.
        /// You can also read the property to check whether it is connected. This reports the current hardware state.
        /// </summary>
        /// <value><c>true</c> if connected to the hardware; otherwise, <c>false</c>.</value>
        public bool Connected
        {
            get
            {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected)
                    return;

                if (value)
                {
                    LogMessage("Connected Set", "Connecting...");
                    Task t = ConnectToDevice();
                    t.Wait();
                    if (bleDevice != null && bleCharacteristic != null)
                    {
                        connectedState = true;
                    }
                    else
                    {
                        bleCharacteristic = null;
                        bleDevice?.Dispose();
                        bleDevice = null;
                        throw new ASCOM.DriverException("Failed to connect");
                    }
                }
                else
                {
                    connectedState = false;

                    LogMessage("Connected Set", "Disconnecting...");

                    bleCharacteristic?.Service?.Session?.Dispose();
                    bleCharacteristic?.Service?.Dispose();

                    bleCharacteristic = null;

                    bleDevice?.Dispose();
                    bleDevice = null;
                }
            }
        }

        /// <summary>
        /// Returns a description of the device, such as manufacturer and modelnumber. Any ASCII characters may be used.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get
            {
                tl.LogMessage("Description Get", deviceName);
                return deviceName;
            }
        }

        /// <summary>
        /// Descriptive and version information about this ASCOM driver.
        /// </summary>
        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = deviceName + " Version " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver formatted as 'm.n'.
        /// </summary>
        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// The interface version number that this device supports. 
        /// </summary>
        public short InterfaceVersion
        {
            get
            {
                LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        /// <summary>
        /// The short name of the driver, for display purposes.
        /// </summary>
        public string Name
        {
            get
            {
                tl.LogMessage("Name Get", deviceName);
                return deviceName;
            }
        }

        #endregion

        #region ISwitchV2 Implementation

        private short numSwitch = 1;

        /// <summary>
        /// The number of switches managed by this driver
        /// </summary>
        /// <returns>The number of devices managed by this driver.</returns>
        public short MaxSwitch
        {
            get
            {
                tl.LogMessage("MaxSwitch Get", numSwitch.ToString());
                return this.numSwitch;
            }
        }

        /// <summary>
        /// Return the name of switch device n.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The name of the device</returns>
        public string GetSwitchName(short id)
        {
            Validate("GetSwitchName", id);
            tl.LogMessage("GetSwitchName", $"GetSwitchName({id})");
            return deviceName;
        }

        /// <summary>
        /// Set a switch device name to a specified value.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="name">The name of the device</param>
        public void SetSwitchName(short id, string name)
        {
            Validate("SetSwitchName", id);
            tl.LogMessage("SetSwitchName", $"SetSwitchName({id}) = {name} - not implemented");
            throw new MethodNotImplementedException("SetSwitchName");
        }

        /// <summary>
        /// Gets the description of the specified switch device. This is to allow a fuller description of
        /// the device to be returned, for example for a tool tip.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>
        /// String giving the device description.
        /// </returns>
        public string GetSwitchDescription(short id)
        {
            Validate("GetSwitchDescription", id);
            tl.LogMessage("GetSwitchDescription", $"GetSwitchDescription({id})");
            return "Turns the spectral calibrator ON or OFF";
        }

        /// <summary>
        /// Reports if the specified switch device can be written to, default true.
        /// This is false if the device cannot be written to, for example a limit switch or a sensor.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>
        /// <c>true</c> if the device can be written to, otherwise <c>false</c>.
        /// </returns>
        public bool CanWrite(short id)
        {
            bool writable = true;
            Validate("CanWrite", id);
            // default behavour is to report true
            tl.LogMessage("CanWrite", $"CanWrite({id}): {writable}");
            return true;
        }

        #region Boolean switch members

        /// <summary>
        /// Return the state of switch device id as a boolean
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>True or false</returns>
        public bool GetSwitch(short id)
        {
            Validate("GetSwitch", id);
            tl.LogMessage("GetSwitch", $"GetSwitch({id})");
            Task<bool> t = QueryDeviceState();
            t.Wait();
            return t.Result;
        }

        /// <summary>
        /// Sets a switch controller device to the specified state, true or false.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="state">The required control state</param>
        public void SetSwitch(short id, bool state)
        {
            Validate("SetSwitch", id);
            if (!CanWrite(id))
            {
                var str = $"SetSwitch({id}) - Cannot Write";
                tl.LogMessage("SetSwitch", str);
                throw new MethodNotImplementedException(str);
            }
            tl.LogMessage("SetSwitch", $"SetSwitch({id}) = {state}");
            if (state)
                TurnDeviceON();
            else
                TurnDeviceOFF();
        }

        #endregion

        #region Analogue members

        /// <summary>
        /// Returns the maximum value for this switch device, this must be greater than <see cref="MinSwitchValue"/>.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The maximum value to which this device can be set or which a read only sensor will return.</returns>
        public double MaxSwitchValue(short id)
        {
            Validate("MaxSwitchValue", id);
            tl.LogMessage("MaxSwitchValue", $"MaxSwitchValue({id}) - not implemented");
            throw new MethodNotImplementedException("MaxSwitchValue");
        }

        /// <summary>
        /// Returns the minimum value for this switch device, this must be less than <see cref="MaxSwitchValue"/>
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The minimum value to which this device can be set or which a read only sensor will return.</returns>
        public double MinSwitchValue(short id)
        {
            Validate("MinSwitchValue", id);
            tl.LogMessage("MinSwitchValue", $"MinSwitchValue({id}) - not implemented");
            throw new MethodNotImplementedException("MinSwitchValue");
        }

        /// <summary>
        /// Returns the step size that this device supports (the difference between successive values of the device).
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The step size for this device.</returns>
        public double SwitchStep(short id)
        {
            Validate("SwitchStep", id);
            tl.LogMessage("SwitchStep", $"SwitchStep({id}) - not implemented");
            throw new MethodNotImplementedException("SwitchStep");
        }

        /// <summary>
        /// Returns the value for switch device id as a double
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The value for this switch, this is expected to be between <see cref="MinSwitchValue"/> and
        /// <see cref="MaxSwitchValue"/>.</returns>
        public double GetSwitchValue(short id)
        {
            Validate("GetSwitchValue", id);
            tl.LogMessage("GetSwitchValue", $"GetSwitchValue({id})");
            return GetSwitch(id) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the value for this device as a double.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="value">The value to be set, between <see cref="MinSwitchValue"/> and <see cref="MaxSwitchValue"/></param>
        public void SetSwitchValue(short id, double value)
        {
            Validate("SetSwitchValue", id, value);
            if (!CanWrite(id))
            {
                tl.LogMessage("SetSwitchValue", $"SetSwitchValue({id}) - Cannot write");
                throw new ASCOM.MethodNotImplementedException($"SetSwitchValue({id}) - Cannot write");
            }
            tl.LogMessage("SetSwitchValue", $"SetSwitchValue({id}) = {value}");
            SetSwitch(id, value != 0);
        }

        #endregion

        #endregion

        #region Private properties and methods

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Switch";
                if (bRegister)
                {
                    P.Register(driverID, deviceName);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Attempts to connect to the BLE device, and sets the `bleDevice` and
        /// `bleCharacteristic` class instance members in case of success.
        /// </summary>
        private async Task ConnectToDevice()
        {
            bleDevice = await BluetoothLEDevice.FromIdAsync(bleDeviceId);
            if (bleDevice == null)
            {
                throw new ASCOM.DriverException("Could not connect to device. Is it powered on?");
            }

            GattDeviceServicesResult servicesResult = await bleDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status == GattCommunicationStatus.Success)
            {
                var services = servicesResult.Services;
                foreach (var service in services)
                {
                    if (service.Uuid.Equals(BLE_UUID))
                    {
                        var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (characteristicsResult.Status == GattCommunicationStatus.Success)
                        {
                            var characteristics = characteristicsResult.Characteristics;
                            foreach (var characteristic in characteristics)
                            {
                                if (characteristic.Uuid.Equals(BLE_UUID))
                                {
                                    bleCharacteristic = characteristic;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether the device is turned on or off by reading the value of the BLE characteristic.
        /// If the device is currently in On/Off cycle, it will report itself as being turned on, fyi.
        /// </summary>
        private async Task<bool> QueryDeviceState()
        {
            tl.LogMessage("QueryDeviceState", "Reading BLE characteristic value...");
            GattReadResult result = await bleCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                byte[] status = new byte[result.Value.Length];
                DataReader.FromBuffer(result.Value).ReadBytes(status);
                if (status.Length != 2)
                    throw new ASCOM.DriverException("Unexpected value length: " + status.Length);
                return status[0] != 0;
            }

            throw new ASCOM.DriverException("Failed to query device");
        }

        /// <summary>
        /// Writes 0x0000 to the BLE characteristic, thereby turning the device off.
        /// </summary>
        private Task TurnDeviceOFF()
        {
            return UpdateDeviceState(CALIBRATOR_OFF);
        }

        /// <summary>
        /// Writes 0x0001 to the BLE characteristic, thereby turning the device on.
        /// </summary>
        private Task TurnDeviceON()
        {
            return UpdateDeviceState(CALIBRATOR_ON);
        }

        /// <summary>
        /// Writes 0x??02 to the BLE characteristic, thereby starting the on/off cycle.
        /// </summary>
        private Task StartOnOffCycle(int dutycycle)
        {
            return UpdateDeviceState(ON_OFF_CYCLE, (byte)dutycycle);
        }

        /// <summary>
        /// Writes the specified value to the BLE characteristic.
        /// </summary>
        private async Task UpdateDeviceState(byte command, byte arg = 0)
        {
            tl.LogMessage("UpdateDeviceState", "Writing BLE characteristic value...");
            var writer = new DataWriter();
            // First byte is the command
            writer.WriteByte(command);
            // Second byte is the command argument, if any.
            writer.WriteByte(arg);
            GattCommunicationStatus result = await bleCharacteristic.WriteValueAsync(writer.DetachBuffer());
            if (result != GattCommunicationStatus.Success)
            {
                throw new ASCOM.DriverException("Failed to update the device state.");
            }
        }

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";

                try
                {
                    tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                    bleDeviceId = driverProfile.GetValue(driverID, bleDeviceIdProfileName, string.Empty, bleDeviceIdDefault);
                    bleDeviceName = driverProfile.GetValue(driverID, bleDeviceNameProfileName, string.Empty, bleDeviceNameDefault);
                }
                catch (Exception e)
                {
                    tl.LogMessage("SpectralCalibrator", "ReadProfile: Exception handled: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, bleDeviceIdProfileName, bleDeviceId);
                driverProfile.WriteValue(driverID, bleDeviceNameProfileName, bleDeviceName);
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }

        /// <summary>
        /// Checks that the switch id is in range and throws an InvalidValueException if it isn't
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        private void Validate(string message, short id)
        {
            if (id < 0 || id >= numSwitch)
            {
                tl.LogMessage(message, string.Format("Switch {0} not available, range is 0 to {1}", id, numSwitch - 1));
                throw new InvalidValueException(message, id.ToString(), string.Format("0 to {0}", numSwitch - 1));
            }
        }

        /// <summary>
        /// Checks that the switch id and value are in range and throws an
        /// InvalidValueException if they are not.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        /// <param name="value">The value.</param>
        private void Validate(string message, short id, double value)
        {
            Validate(message, id);
            var min = MinSwitchValue(id);
            var max = MaxSwitchValue(id);
            if (value < min || value > max)
            {
                tl.LogMessage(message, string.Format("Value {1} for Switch {0} is out of the allowed range {2} to {3}", id, value, min, max));
                throw new InvalidValueException(message, value.ToString(), string.Format("Switch({0}) range {1} to {2}", id, min, max));
            }
        }

        #endregion
    }
}
