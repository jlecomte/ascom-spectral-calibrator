# First, make sure that you have installed the ASCOM driver, and that you have Python 3 installed.
# Refer to the README on how to do this...
#
# Next, install the pypiwin32 package (you only need to do this once):
#     C:\> python -m pip install pypiwin32
#
# Next, make sure that your ASCOM profile already has a BLE device selected.
# The easiest way to do this is to use an application like N.I.N.A.
# In the "Equipment" tab, open the "Switch" tab, and select the "DarkSkyGeek Spectral Calibrator" device.
# Then, click on the button with the gear icon to open the driver settings dialog.
# In the settings dialog, select the "DSG-Calibration-Lamp" device, and click OK.
#
# Finally, you can run this script. Here are a few examples:
#
#     C:\> calibrator.py on
#     C:\> calibrator.py off
#     C:\> calibrator.py dutycycle 30

import sys
import argparse
import win32com.client
from threading import Timer

parser = argparse.ArgumentParser()
sp = parser.add_subparsers(help='commands', title='commands', dest='command')
sp.required = True
on_parser = sp.add_parser('on', help='Turns the device on')
off_parser = sp.add_parser('off', help='Turns device off')
dutycycle_parser = sp.add_parser('dutycycle', help='Starts an on-off cycle')
dutycycle_parser.add_argument('ratio', type=int, help='Duty cycle ratio')
args = parser.parse_args()

device = win32com.client.Dispatch('ASCOM.DarkSkyGeek.SpectralCalibrator')

try:
    print('Connecting...')
    device.Connected = True
    print('Connected!')
except:
    sys.exit("""
Failed to connect to the device.
1. Did you install the ASCOM driver?
2. Did you select the right device in the driver settings dialog?
3. Is the device powered up?
4. Did you rapidly disconnect/re-connect? (known issue: wait ~ 10-15 seconds after disconnecting, and before connecting again)
""")

if args.command == 'on':
    print('Turning on')
    device.SetSwitch(0, True)
elif args.command == 'off':
    print('Turning off')
    device.SetSwitch(0, False)
elif args.command == 'dutycycle':
    print('Starting on-off cycle')
    device.Action('SetDutyCycle', args.ratio)

def disconnect():
    print('Disconnecting...')
    device.Connected = False
    print('Disconnected!')

Timer(3.0, disconnect)
