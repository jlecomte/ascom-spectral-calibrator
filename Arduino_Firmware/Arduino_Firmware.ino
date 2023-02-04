/*
 * Arduino_Firmware.ino
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * If you use a Seeed Studio XIAO nRF52840, make sure you review the following pages:
 *   - https://forum.seeedstudio.com/t/the-getting-started-wiki-page-for-xiao-nrf52840-has-invalid-outdated-instructions/266005
 *   - https://forum.arduino.cc/t/xiao-boards-property-upload-tool-serial-is-undefined/1035010/12
 */

#include <ArduinoBLE.h>

// Uncomment this to debug the firmware when the device is connected via USB.
// Do not forget to comment it out before flashing the final version, or the
// device will not work when not connected to a computer via USB...
// #define DEBUG

// Bluetooth® Low Energy LED Service
BLEService calibratorService("79f465db-72b2-4d52-91e0-be18403ee589");

// Bluetooth® Low Energy LED Switch Characteristic - custom 128-bit UUID, read and writable by the central device
BLEShortCharacteristic switchCharacteristic("b4335a57-58d3-414e-9fd1-43b4d6bf72de", BLERead | BLEWrite);

const int controlPin = 8;

int SIGNAL_ON, SIGNAL_OFF;

// Commands and state values:
const uint8_t CALIBRATOR_OFF = 0;
const uint8_t CALIBRATOR_ON  = 1;
const uint8_t ON_OFF_CYCLE   = 2;

// Current state, defaults to OFF (we set the initial value of the characteristic to that as well)
uint8_t state = CALIBRATOR_OFF;

// Variables used to handle the on/off cycle.
// Note: The period of the on/off cycle is 60 seconds, which should work well for most use cases.
const int ON_OFF_CYCLE_PERIOD_MICROSEC = 60 * 1000 * 1000;
uint8_t dutyCycle;
unsigned long onOffCyclePeriodStartTime;

void setup() {
#ifdef DEBUG
  Serial.begin(9600);
  while (!Serial);
#endif

  pinMode(controlPin, OUTPUT);

  // Test whether HIGH means ON or OFF...
  // See https://forum.arduino.cc/t/digitalwrite-led-builtin-low-turns-on-the-led/965532
  SIGNAL_OFF = digitalRead(controlPin);
  SIGNAL_ON = SIGNAL_OFF == HIGH ? LOW : HIGH;

  if (!BLE.begin()) {
#ifdef DEBUG
    Serial.println("Starting Bluetooth® Low Energy module failed!");
#endif
    while (1);
  }

  // Keep these under 20 bytes. That's the size of the BLE packets.
  // Otherwise, the name may sometimes appear truncated.
  // DSG = DarkSkyGeek of course... :)
  BLE.setDeviceName("DSG-Calibration-Lamp");
  BLE.setLocalName("DSG-Calibration-Lamp");

  BLE.setAdvertisedService(calibratorService);

  calibratorService.addCharacteristic(switchCharacteristic);
  BLE.addService(calibratorService);

  // Set the initial value for the characeristic:
  switchCharacteristic.writeValue((uint16_t)CALIBRATOR_OFF);

  BLE.advertise();

#ifdef DEBUG
  Serial.print("BLE device initialized. MAC address is: ");
  Serial.println(BLE.address());
#endif

  // Blink built-in LED a few times to show that we are alive
  for (int i = 0; i < 5; i++) {
    digitalWrite(LED_BUILTIN, SIGNAL_ON);
    delay(100);
    digitalWrite(LED_BUILTIN, SIGNAL_OFF);
    delay(100);
  }
}

void loop() {
  // Listen for Bluetooth® Low Energy peripherals to connect:
  BLEDevice central = BLE.central();

  // If a central device is connected to our peripheral device:
  if (central) {
#ifdef DEBUG
    Serial.print("Connected to central device: ");
    // Print the central device MAC address:
    Serial.println(central.address());
#endif

    // While the central device is still connected to our peripheral device:
    while (central.connected()) {
      // If the central device wrote to the characteristic,
      // use the value to control the LED:
      if (switchCharacteristic.written()) {
        uint16_t value = switchCharacteristic.value();
#ifdef DEBUG
        Serial.print("Received value: ");
        Serial.println(value);
#endif
        uint8_t command = (uint8_t)(value & 0xff);
        if (command == CALIBRATOR_OFF) {
          turnCalibratorOff();
        } else if (command == CALIBRATOR_ON) {
          turnCalibratorOn();
        } else if (command == ON_OFF_CYCLE) {
          uint8_t arg = (uint8_t)((value >> 8) & 0xff);
          startOnOffCycle(arg);
        } else {
#ifdef DEBUG
          Serial.print("Invalid command:");
          Serial.println(command);
#endif
        }
      }

      if (state == ON_OFF_CYCLE) {
        handleOnOffCycle();
      }
    }

#ifdef DEBUG
    Serial.print(F("Disconnected from central device: "));
    Serial.println(central.address());
#endif

    // We could turn the device off here, but we don't so that the effect
    // of a script can remain after the connection has been terminated.
  }

  // Upon disconnection, we need to continue handling the on/off cycle:
  if (state == ON_OFF_CYCLE) {
    handleOnOffCycle();
  }
}

void turnCalibratorOff() {
#ifdef DEBUG
  Serial.println("Calibrator OFF");
#endif
  state = CALIBRATOR_OFF;
  digitalWrite(controlPin, SIGNAL_OFF);
}

void turnCalibratorOn() {
#ifdef DEBUG
  Serial.println("Calibrator ON");
#endif
  state = CALIBRATOR_ON;
  digitalWrite(controlPin, SIGNAL_ON);  
}

void startOnOffCycle(uint8_t arg) {
#ifdef DEBUG
  Serial.print("Starting on/off cycle with duty cycle value of: ");
  Serial.println(arg);
#endif

  state = ON_OFF_CYCLE;
  dutyCycle = arg;

  // We start in the OFF state:
  digitalWrite(controlPin, SIGNAL_OFF);

  // And we save the current time:
  onOffCyclePeriodStartTime = micros();
}

void handleOnOffCycle() {
  unsigned long now = micros();
  if (now - onOffCyclePeriodStartTime > ON_OFF_CYCLE_PERIOD_MICROSEC) {
    // We completed one period!
    // Turn the calibrator OFF:
#ifdef DEBUG
    Serial.println("Calibrator ON (duty cycle)");
#endif
    digitalWrite(controlPin, SIGNAL_OFF);
    // And save the current time:
    onOffCyclePeriodStartTime = now;
  } else if (now - onOffCyclePeriodStartTime > ON_OFF_CYCLE_PERIOD_MICROSEC * (100 - dutyCycle) / 100) {
    // We are in the ON part of the cycle:
#ifdef DEBUG
    Serial.println("Calibrator OFF (duty cycle)");
#endif
    digitalWrite(controlPin, SIGNAL_ON);
  }
}
