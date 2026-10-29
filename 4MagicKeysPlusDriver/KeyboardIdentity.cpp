#include "KeyboardIdentity.h"

namespace {
    wchar_t Upper(wchar_t c) {
        return (c >= L'a' && c <= L'z') ? (wchar_t)(c - L'a' + L'A') : c;
    }

    bool IsHex(wchar_t c) {
        return (c >= L'0' && c <= L'9') || (c >= L'A' && c <= L'F') || (c >= L'a' && c <= L'f');
    }

    unsigned int Length(const wchar_t* text) {
        unsigned int length = 0;
        while (text[length]) {
            length++;
        }
        return length;
    }

    const wchar_t* SkipPrefix(const wchar_t* text, const wchar_t* prefix) {
        while (*prefix) {
            if (Upper(*text) != Upper(*prefix)) {
                return nullptr;
            }
            text++;
            prefix++;
        }
        return text;
    }

    const wchar_t* LastSegment(const wchar_t* text) {
        const wchar_t* segment = nullptr;
        for (const wchar_t* p = text; *p; p++) {
            if (*p == L'\\') {
                segment = p + 1;
            }
        }
        return segment;
    }

    int NoIdentity(wchar_t* identity) {
        identity[0] = L'\0';
        return 0;
    }

    bool EqualsIgnoringCase(const wchar_t* text, unsigned int length, const wchar_t* other) {
        for (unsigned int i = 0; i < length; i++) {
            if (other[i] == L'\0' || Upper(text[i]) != Upper(other[i])) {
                return false;
            }
        }
        return other[length] == L'\0';
    }

    unsigned int ProductIdOfFirstSegment(const wchar_t* deviceId, const wchar_t* prefix, const wchar_t* marker) {
        if (!deviceId) {
            return 0;
        }

        const wchar_t* segment = SkipPrefix(deviceId, prefix);
        if (!segment) {
            return 0;
        }
        const wchar_t* end = segment;
        while (*end && *end != L'\\') {
            end++;
        }
        unsigned int markerLength = Length(marker);
        if (*end != L'\\' || (unsigned int)(end - segment) < markerLength + 4) {
            return 0;
        }

        const wchar_t* digits = end - 4;
        if (!EqualsIgnoringCase(digits - markerLength, markerLength, marker)) {
            return 0;
        }
        unsigned int productId = 0;
        for (int i = 0; i < 4; i++) {
            if (!IsHex(digits[i])) {
                return 0;
            }
            wchar_t digit = Upper(digits[i]);
            productId = productId * 16 + (unsigned int)(digit <= L'9' ? digit - L'0' : digit - L'A' + 10);
        }
        return productId;
    }
}

extern "C" {

    unsigned int MkpProductIdFromBluetoothDeviceId(const wchar_t* deviceId) {
        return ProductIdOfFirstSegment(deviceId, L"BTHENUM\\", L"_PID&");
    }

    unsigned int MkpProductIdFromUsbDeviceId(const wchar_t* deviceId) {
        return ProductIdOfFirstSegment(deviceId, L"USB\\", L"&PID_");
    }

    int MkpIdentityFromBluetoothDeviceId(const wchar_t* deviceId, wchar_t* identity) {
        identity[0] = L'\0';
        if (!deviceId) {
            return 0;
        }

        const wchar_t* afterPrefix = SkipPrefix(deviceId, L"BTHENUM\\");
        const wchar_t* segment = afterPrefix ? LastSegment(afterPrefix) : nullptr;
        if (!segment) {
            return 0;
        }

        // The last segment ends with &<12 hex digits of the address>_C<8 hex digits>.
        unsigned int length = Length(segment);
        if (length < 23) {
            return 0;
        }
        const wchar_t* tail = segment + length - 10;
        if (tail[0] != L'_' || Upper(tail[1]) != L'C') {
            return 0;
        }
        for (int i = 2; i < 10; i++) {
            if (!IsHex(tail[i])) {
                return 0;
            }
        }
        const wchar_t* address = tail - 12;
        if (address[-1] != L'&') {
            return 0;
        }
        for (int i = 0; i < 12; i++) {
            if (!IsHex(address[i])) {
                return NoIdentity(identity);
            }
        }

        identity[0] = L'b';
        identity[1] = L't';
        identity[2] = L':';
        for (int i = 0; i < 12; i++) {
            identity[3 + i] = Upper(address[i]);
        }
        identity[15] = L'\0';
        return 1;
    }

    int MkpIdentityFromUsbDeviceId(const wchar_t* deviceId, wchar_t* identity) {
        identity[0] = L'\0';
        if (!deviceId) {
            return 0;
        }

        // USB\<hardware IDs>\<serial number>
        const wchar_t* hardware = SkipPrefix(deviceId, L"USB\\");
        if (!hardware) {
            return 0;
        }
        const wchar_t* separator = hardware;
        while (*separator && *separator != L'\\') {
            separator++;
        }
        if (*separator != L'\\' || separator == hardware) {
            return 0;
        }

        const wchar_t* serial = separator + 1;
        unsigned int length = Length(serial);
        if (length == 0 || length + 5 > MKP_IDENTITY_MAX_CHARS) {
            return 0;
        }
        for (unsigned int i = 0; i < length; i++) {
            if (serial[i] == L'&' || serial[i] == L'\\') {
                return NoIdentity(identity);
            }
        }

        identity[0] = L'u';
        identity[1] = L's';
        identity[2] = L'b';
        identity[3] = L':';
        for (unsigned int i = 0; i < length; i++) {
            identity[4 + i] = Upper(serial[i]);
        }
        identity[4 + length] = L'\0';
        return 1;
    }

    int MkpIsUsbInterface0(const wchar_t* deviceId) {
        if (!deviceId) {
            return 0;
        }

        const wchar_t* segment = SkipPrefix(deviceId, L"USB\\");
        if (!segment) {
            return 0;
        }
        const wchar_t* end = segment;
        while (*end && *end != L'\\') {
            end++;
        }

        const wchar_t marker[] = L"&MI_00";
        unsigned int markerLength = Length(marker);
        if (*end != L'\\' || (unsigned int)(end - segment) < markerLength) {
            return 0;
        }
        return EqualsIgnoringCase(end - markerLength, markerLength, marker) ? 1 : 0;
    }

    int MkpIdentityListContains(const wchar_t* list, unsigned int chars, const wchar_t* identity) {
        unsigned int i = 0;
        while (i < chars) {
            unsigned int start = i;
            while (i < chars && list[i] != L'\0') {
                i++;
            }
            if (i > start && EqualsIgnoringCase(list + start, i - start, identity)) {
                return 1;
            }
            i++;
        }
        return 0;
    }

} // extern "C"
