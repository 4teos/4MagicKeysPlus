#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#define MKP_USB_DESCRIPTOR_HID_REPORT 0x22

    unsigned int MkpTrimBatteryReportDescriptor(const unsigned char* descriptor, unsigned int length);
    unsigned int MkpPatchHidDescriptorLength(unsigned char* descriptors, unsigned int length);

#ifdef __cplusplus
}
#endif
