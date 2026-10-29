#include "UsbDescriptorFix.h"

namespace {
    // Windows cannot start the battery interface of the 2024 keyboards: its descriptor is these 83 bytes of the first
    // generation one (which Windows accepts) plus two more collections and four zero bytes.
    const unsigned char kBatteryDescriptor[] = {
        0x06, 0x00, 0xFF, 0x09, 0x0B, 0xA1, 0x01, 0x06, 0x00, 0xFF, 0x09, 0x0B,
        0x15, 0x00, 0x26, 0xFF, 0x00, 0x75, 0x08, 0x96, 0x04, 0x00, 0x85, 0xE0,
        0x81, 0x22, 0x09, 0x0B, 0x96, 0x01, 0x00, 0x85, 0x9A, 0x81, 0x22, 0xC0,
        0x06, 0x00, 0xFF, 0x09, 0x14, 0xA1, 0x01, 0x85, 0x90, 0x05, 0x84, 0x75,
        0x01, 0x95, 0x03, 0x15, 0x00, 0x25, 0x01, 0x09, 0x61, 0x05, 0x85, 0x09,
        0x44, 0x09, 0x46, 0x81, 0x02, 0x95, 0x05, 0x81, 0x01, 0x75, 0x08, 0x95,
        0x01, 0x15, 0x00, 0x26, 0xFF, 0x00, 0x09, 0x65, 0x81, 0x02, 0xC0};
    const unsigned int kTrimmedLength = sizeof(kBatteryDescriptor);
    const unsigned int kFullLength = 263;

    const unsigned int kHidDescriptorSize = 9;
    const unsigned char kHidDescriptorType = 0x21;
}

unsigned int MkpTrimBatteryReportDescriptor(const unsigned char* descriptor, unsigned int length) {
    if (!descriptor || length <= kTrimmedLength) {
        return length;
    }
    for (unsigned int i = 0; i < kTrimmedLength; i++) {
        if (descriptor[i] != kBatteryDescriptor[i]) {
            return length;
        }
    }
    return kTrimmedLength;
}

unsigned int MkpPatchHidDescriptorLength(unsigned char* descriptors, unsigned int length) {
    unsigned int patched = 0;
    unsigned int position = 0;
    while (descriptors && position + 2 <= length) {
        unsigned int size = descriptors[position];
        if (size < 2 || position + size > length) {
            break;
        }
        unsigned char* descriptor = descriptors + position;
        if (size == kHidDescriptorSize && descriptor[1] == kHidDescriptorType && descriptor[6] == MKP_USB_DESCRIPTOR_HID_REPORT
            && descriptor[7] == (kFullLength & 0xFF) && descriptor[8] == (kFullLength >> 8)) {
            descriptor[7] = (unsigned char)(kTrimmedLength & 0xFF);
            descriptor[8] = (unsigned char)(kTrimmedLength >> 8);
            patched++;
        }
        position += size;
    }
    return patched;
}
