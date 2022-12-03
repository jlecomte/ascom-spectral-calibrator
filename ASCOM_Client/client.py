# First, make sure that you have installed the ASCOM driver.
# Refer to the README on how to do this...
#
# Next, install the pypiwin32 package (you only need to do this once):
#     > python -m pip install pypiwin32
#
# Next, make sure that your ASCOM profile already has a BLE device selected.
# The easiest way to do this is to use an application like N.I.N.A.
# In the "Equipment" tab, open the "Switch" tab, and select the "DarkSkyGeek Spectral Calibrator" device.
# Then, click on the button with the "gear" icon to open the driver setup dialog.
# In the setup dialog, select the "DSG-Calibration-Lamp" device and click OK.
#
# Finally, you can run this script. Here are a few examples:
#
#     > client.py on
#     > client.py off
#     > client.py dutycycle 30

import argparse
import win32com.client

parser = argparse.ArgumentParser()
sp = parser.add_subparsers(help='commands', title='commands', dest='command')
sp.required = True
on_parser = sp.add_parser('on', help='Turns the device on')
off_parser = sp.add_parser('off', help='Turns device off')
dutycycle_parser = sp.add_parser('dutycycle', help='Starts an on-off cycle')
dutycycle_parser.add_argument('ratio', type=int, help='Duty cycle ratio')
args = parser.parse_args()

device = win32com.client.Dispatch('ASCOM.DarkSkyGeek.SpectralCalibrator')
if not device.Connected:
    device.Connected = True

if args.command == 'on':
    print('Turning on')
    device.SetSwitch(0, True)
    input("Press Enter to exit the program...\n")
elif args.command == 'off':
    print('Turning off')
    device.SetSwitch(0, False)
    input("Press Enter to exit the program...\n")
elif args.command == 'dutycycle':
    print('Starting on-off cycle')
    device.Action('SetDutyCycle', args.ratio)
    input("Observe the calibrator device, and when you are ready, press Enter to exit the program...\n")

device.Connected = False
