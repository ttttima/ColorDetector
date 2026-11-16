#include <Arduino.h>
#include "USB.h"
#include "USBHID.h"
#include "tusb.h"

static const uint8_t mouseReportDescriptor[] = {
  0x05, 0x01,        // Usage Page (Generic Desktop)
  0x09, 0x02,        // Usage (Mouse)
  0xA1, 0x01,        // Collection (Application)
  0x09, 0x01,        //   Usage (Pointer)
  0xA1, 0x00,        //   Collection (Physical)
  0x05, 0x09,        //     Usage Page (Button)
  0x19, 0x01,        //     Usage Minimum (Button 1)
  0x29, 0x03,        //     Usage Maximum (Button 3)
  0x15, 0x00,        //     Logical Minimum (0)
  0x25, 0x01,        //     Logical Maximum (1)
  0x95, 0x03,        //     Report Count (3)
  0x75, 0x01,        //     Report Size (1)
  0x81, 0x02,        //     Input (Data, Variable, Absolute)
  0x95, 0x01,        //     Report Count (1)
  0x75, 0x05,        //     Report Size (5)
  0x81, 0x03,        //     Input (Constant, Variable, Absolute)
  0x05, 0x01,        //     Usage Page (Generic Desktop)
  0x09, 0x30,        //     Usage (X)
  0x09, 0x31,        //     Usage (Y)
  0x09, 0x38,        //     Usage (Wheel)
  0x15, 0x81,        //     Logical Minimum (-127)
  0x25, 0x7F,        //     Logical Maximum (127)
  0x75, 0x08,        //     Report Size (8)
  0x95, 0x03,        //     Report Count (3)
  0x81, 0x06,        //     Input (Data, Variable, Relative)
  0xC0,              //   End Collection
  0xC0               // End Collection
};

struct MouseReport {
  uint8_t buttons;
  int8_t x;
  int8_t y;
  int8_t wheel;
} __attribute__((packed));

class MinimalMouse : public USBHIDDevice {
 public:
  MinimalMouse() {
    hid.addDevice(this, sizeof(mouseReportDescriptor));
  }

  void begin() {
    hid.begin();
  }

  uint16_t _onGetDescriptor(uint8_t* buffer) override {
    memcpy(buffer, mouseReportDescriptor, sizeof(mouseReportDescriptor));
    return sizeof(mouseReportDescriptor);
  }

 private:
  USBHID hid;
};

MinimalMouse mouse;

void setup() {
  USB.VID(0x303A);
  USB.PID(0x4013);
  USB.manufacturerName("M5Stack");
  USB.productName("ColorDetector Minimal Mouse");
  USB.serialNumber("COLORDETECTOR-USB-TEST-04");

  mouse.begin();
  USB.begin();

  delay(5000);
}

void loop() {
  while (!tud_hid_ready()) {
    delay(1);
  }
  tud_hid_mouse_report(0, 1, 0, 0, 0, 0);
  delay(60);
  while (!tud_hid_ready()) {
    delay(1);
  }
  tud_hid_mouse_report(0, 0, 0, 0, 0, 0);

  delay(2000);
}
