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
BLEService calibratorService("19B10000-E8F2-537E-4F6C-D104768A1214");

// Bluetooth® Low Energy LED Switch Characteristic - custom 128-bit UUID, read and writable by central
BLEByteCharacteristic switchCharacteristic("19B10001-E8F2-537E-4F6C-D104768A1214", BLERead | BLEWrite);

const int controlPin = 8;

int SIGNAL_ON, SIGNAL_OFF;

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

  BLE.setDeviceName("DarkSkyGeek-Calibration-Lamp");
  BLE.setLocalName("DarkSkyGeek-Calibration-Lamp");

  BLE.setAdvertisedService(calibratorService);

  calibratorService.addCharacteristic(switchCharacteristic);
  BLE.addService(calibratorService);

  // Set the initial value for the characeristic:
  switchCharacteristic.writeValue(0);

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
    // Print the central's MAC address:
    Serial.println(central.address());
#endif

    // While the central device is still connected to our peripheral device:
    while (central.connected()) {
      // If the central device wrote to the characteristic,
      // use the value to control the LED:
      if (switchCharacteristic.written()) {
        if (switchCharacteristic.value() != 0) {
#ifdef DEBUG
          Serial.println("Calibrator on");
#endif
          digitalWrite(controlPin, SIGNAL_ON);
        } else {
#ifdef DEBUG
          Serial.println("Calibrator off");
#endif
          digitalWrite(controlPin, SIGNAL_OFF);
        }
      }
    }

#ifdef DEBUG
    Serial.print(F("Disconnected from central device: "));
    Serial.println(central.address());
#endif
  }
}
