#define USB_PID 0x4048

#include <Arduino.h>
#include "USB.h"
#include "USBHID.h"

namespace {
const uint8_t MouseReportDescriptor[] = {
  0x05, 0x01,  // Usage Page (Generic Desktop)
  0x09, 0x02,  // Usage (Mouse)
  0xA1, 0x01,  // Collection (Application)
  0x09, 0x01,  //   Usage (Pointer)
  0xA1, 0x00,  //   Collection (Physical)
  0x05, 0x09,  //     Usage Page (Buttons)
  0x19, 0x01,  //     Usage Minimum (1)
  0x29, 0x03,  //     Usage Maximum (3)
  0x15, 0x00,  //     Logical Minimum (0)
  0x25, 0x01,  //     Logical Maximum (1)
  0x95, 0x03,  //     Report Count (3)
  0x75, 0x01,  //     Report Size (1)
  0x81, 0x02,  //     Input (Data, Variable, Absolute)
  0x95, 0x01,  //     Report Count (1)
  0x75, 0x05,  //     Report Size (5)
  0x81, 0x03,  //     Input (Constant)
  0x05, 0x01,  //     Usage Page (Generic Desktop)
  0x09, 0x30,  //     Usage (X)
  0x09, 0x31,  //     Usage (Y)
  0x15, 0x81,  //     Logical Minimum (-127)
  0x25, 0x7F,  //     Logical Maximum (127)
  0x75, 0x08,  //     Report Size (8)
  0x95, 0x02,  //     Report Count (2)
  0x81, 0x06,  //     Input (Data, Variable, Relative)
  0xC0,        //   End Collection
  0xC0         // End Collection
};

class BootMouse : public USBHIDDevice
{
public:
  BootMouse()
    : hid(HID_ITF_PROTOCOL_MOUSE)
  {
    USBHID::addDevice(this, sizeof(MouseReportDescriptor));
  }

  void begin()
  {
    hid.begin();
  }

  bool send(uint8_t buttons, int8_t x, int8_t y)
  {
    const uint8_t report[] = {
      buttons,
      static_cast<uint8_t>(x),
      static_cast<uint8_t>(y)
    };
    return hid.SendReport(0, &report, sizeof(report));
  }

  uint16_t _onGetDescriptor(uint8_t *buffer) override
  {
    memcpy(
      buffer,
      MouseReportDescriptor,
      sizeof(MouseReportDescriptor));
    return sizeof(MouseReportDescriptor);
  }

private:
  USBHID hid;
};

BootMouse mouse;
String command;

void handleCommand()
{
  command.trim();

  if (command == "CLICK") {
    const bool downSent = mouse.send(0x01, 0, 0);

    delay(5);

    const bool upSent = mouse.send(0, 0, 0);

    Serial.println(
      downSent && upSent ? "OK CLICK" : "ERR HID");
  } else if (command.startsWith("LINE ")) {
    const int firstSeparator = command.indexOf(' ', 5);
    const int secondSeparator = firstSeparator < 0
      ? -1
      : command.indexOf(' ', firstSeparator + 1);
    if (firstSeparator < 0 || secondSeparator < 0) {
      Serial.println("ERR MOVE");
    } else {
      const int totalX = command.substring(5, firstSeparator).toInt();
      const int totalY = command.substring(
        firstSeparator + 1,
        secondSeparator).toInt();
      const int thirdSeparator = command.indexOf(
        ' ',
        secondSeparator + 1);
      const int stepDelayMs = constrain(
        command.substring(
          secondSeparator + 1,
          thirdSeparator < 0 ? command.length() : thirdSeparator).toInt(),
        0,
        20);
      const int maximumStepCounts = thirdSeparator < 0
        ? 8
        : constrain(
            command.substring(thirdSeparator + 1).toInt(),
            1,
            127);
      const int maximumDistance = max(abs(totalX), abs(totalY));
      const int steps = max(
        1,
        (maximumDistance + maximumStepCounts - 1)
          / maximumStepCounts);
      int sentX = 0;
      int sentY = 0;
      bool sent = true;

      for (int step = 1; step <= steps; step++) {
        const int targetX = static_cast<int>(
          round(totalX * step / static_cast<double>(steps)));
        const int targetY = static_cast<int>(
          round(totalY * step / static_cast<double>(steps)));
        const int8_t deltaX = static_cast<int8_t>(targetX - sentX);
        const int8_t deltaY = static_cast<int8_t>(targetY - sentY);
        sent = mouse.send(0, deltaX, deltaY) && sent;
        sentX = targetX;
        sentY = targetY;
        if (stepDelayMs > 0) {
          delay(stepDelayMs);
        }
      }

      Serial.println(sent ? "OK LINE" : "ERR HID");
    }
  } else if (command.startsWith("MOVE ")) {
    const int separator = command.indexOf(' ', 5);
    if (separator < 0) {
      Serial.println("ERR MOVE");
    } else {
      int x = command.substring(5, separator).toInt();
      int y = command.substring(separator + 1).toInt();
      bool sent = true;

      while (x != 0 || y != 0) {
        const int8_t stepX = static_cast<int8_t>(constrain(x, -127, 127));
        const int8_t stepY = static_cast<int8_t>(constrain(y, -127, 127));
        sent = mouse.send(0, stepX, stepY) && sent;
        x -= stepX;
        y -= stepY;
        delay(1);
      }

      Serial.println(sent ? "OK MOVE" : "ERR HID");
    }
  } else if (!command.isEmpty()) {
    Serial.println("ERR UNKNOWN");
  }

  command.clear();
}
}

void setup() {
  Serial.begin(115200);
  mouse.begin();
  USB.begin();

  command.reserve(64);
}

void loop() {
  while (Serial.available() > 0) {
    const char value = static_cast<char>(Serial.read());

    if (value == '\n') {
      handleCommand();
    } else if (value != '\r' && command.length() < 63) {
      command += value;
    }
  }

  delay(1);
}
