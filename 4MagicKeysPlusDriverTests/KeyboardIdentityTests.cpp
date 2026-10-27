#include "pch.h"
#include "CppUnitTest.h"

#include <string>

#include "KeyboardIdentity.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace My4MagicKeysPlusDriverTests {
    namespace {
        struct Result {
            int Found;
            wchar_t Identity[MKP_IDENTITY_MAX_CHARS];
        };

        Result FromBluetooth(const wchar_t* deviceId) {
            Result result;
            result.Found = MkpIdentityFromBluetoothDeviceId(deviceId, result.Identity);
            return result;
        }

        Result FromUsb(const wchar_t* deviceId) {
            Result result;
            result.Found = MkpIdentityFromUsbDeviceId(deviceId, result.Identity);
            return result;
        }

        void AssertNoIdentity(const Result& result) {
            Assert::AreEqual(0, result.Found);
            Assert::AreEqual(L"", result.Identity);
        }

        void AssertIdentity(const wchar_t* expected, const Result& result) {
            Assert::AreEqual(1, result.Found);
            Assert::AreEqual(expected, result.Identity);
        }

        // The instance IDs are the ones of the A1843 on the development machine.
        const wchar_t* kBluetoothHid = L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\\D&15B70467&0&C4141104498E_C00000000";
        const wchar_t* kUsbDevice = L"USB\\VID_05AC&PID_026C\\F0T125100BWJKPGAS";
    }

    TEST_CLASS(BluetoothIdentityTests) {
    public:
        TEST_METHOD(TheAddressOfThePairedHidServiceIsTheIdentity) {
            AssertIdentity(L"bt:C4141104498E", FromBluetooth(kBluetoothHid));
        }

        TEST_METHOD(TheCaseOfTheInstanceIdDoesNotMatter) {
            AssertIdentity(L"bt:C4141104498E", FromBluetooth(L"bthenum\\{00001124-0000-1000-8000-00805f9b34fb}_vid&0001004c_pid&0267\\d&15b70467&0&c4141104498e_c00000000"));
        }

        TEST_METHOD(TheOtherBluetoothServicesAndTheDeviceNodeHaveNone) {
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\DEV_C4141104498E\\D&15B70467&0&BLUETOOTHDEVICE_C4141104498E"));
        }

        TEST_METHOD(AnotherEnumeratorHasNone) {
            AssertNoIdentity(FromBluetooth(kUsbDevice));
            AssertNoIdentity(FromBluetooth(L"HID\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C&COL01\\E&D9CCAE&1A&0000"));
        }

        TEST_METHOD(AnAddressOfTheWrongLengthOrWithNonHexDigitsIsNone) {
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\\D&15B70467&0&C414110449E_C00000000"));
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\\D&15B70467&0&C41411044G8E_C00000000"));
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\\D&15B70467&0&C4141104498E_C0000000G"));
        }

        TEST_METHOD(WithoutTheAmpersandBeforeTheAddressTheIdIsNone) {
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\X\\C4141104498E_C00000000"));
        }

        TEST_METHOD(EmptyAndMissingIdsAreNone) {
            AssertNoIdentity(FromBluetooth(L""));
            AssertNoIdentity(FromBluetooth(nullptr));
            AssertNoIdentity(FromBluetooth(L"BTHENUM\\"));
        }
    };

    TEST_CLASS(UsbIdentityTests) {
    public:
        TEST_METHOD(TheSerialNumberOfTheParentUsbDeviceIsTheIdentity) {
            AssertIdentity(L"usb:F0T125100BWJKPGAS", FromUsb(kUsbDevice));
        }

        TEST_METHOD(TheSerialNumberIsUppercasedAndThePrefixMatchesWithoutCase) {
            AssertIdentity(L"usb:F0T125100BWJKPGAS", FromUsb(L"usb\\vid_05ac&pid_026c\\f0t125100bwjkpgas"));
        }

        TEST_METHOD(AMadeUpIdWithoutASerialNumberIsNone) {
            AssertNoIdentity(FromUsb(L"USB\\VID_05AC&PID_026C\\5&1A2B3C4D&0&2"));
        }

        TEST_METHOD(TheIdOfAnInterfaceIsNone) {
            AssertNoIdentity(FromUsb(L"USB\\VID_05AC&PID_026C&MI_01\\9&286A66C3&1&0001"));
        }

        TEST_METHOD(AnIdWithoutASerialSegmentIsNone) {
            AssertNoIdentity(FromUsb(L"USB\\VID_05AC&PID_026C\\"));
            AssertNoIdentity(FromUsb(L"USB\\VID_05AC&PID_026C"));
            AssertNoIdentity(FromUsb(L"USB\\\\SERIAL"));
        }

        TEST_METHOD(ASerialWithABackslashIsNone) {
            AssertNoIdentity(FromUsb(L"USB\\VID_05AC&PID_026C\\A\\B"));
        }

        TEST_METHOD(AnotherEnumeratorHasNone) {
            AssertNoIdentity(FromUsb(kBluetoothHid));
            AssertNoIdentity(FromUsb(L""));
            AssertNoIdentity(FromUsb(nullptr));
        }

        TEST_METHOD(ASerialThatFitsTheBufferIsKeptAndALongerOneIsNone) {
            // "usb:" and the terminator take 5 of the MKP_IDENTITY_MAX_CHARS characters.
            std::wstring fits(MKP_IDENTITY_MAX_CHARS - 5, L'A');
            std::wstring tooLong(MKP_IDENTITY_MAX_CHARS - 4, L'A');

            AssertIdentity((L"usb:" + fits).c_str(), FromUsb((L"USB\\VID_05AC&PID_026C\\" + fits).c_str()));
            AssertNoIdentity(FromUsb((L"USB\\VID_05AC&PID_026C\\" + tooLong).c_str()));
        }
    };

    TEST_CLASS(ProductIdTests) {
    public:
        TEST_METHOD(TheProductIdOfThePairedHidServiceIsTheModel) {
            Assert::AreEqual(0x026Cu, MkpProductIdFromBluetoothDeviceId(kBluetoothHid));
            Assert::AreEqual(0x0267u, MkpProductIdFromBluetoothDeviceId(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&0267\\D&15B70467&0&C4141104498E_C00000000"));
        }

        TEST_METHOD(TheProductIdOfTheParentUsbDeviceIsTheModel) {
            Assert::AreEqual(0x026Cu, MkpProductIdFromUsbDeviceId(kUsbDevice));
            Assert::AreEqual(0x0267u, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_0267\\F0T125100BWJKPGAS"));
        }

        TEST_METHOD(TheCaseOfTheIdDoesNotMatter) {
            Assert::AreEqual(0x026Cu, MkpProductIdFromBluetoothDeviceId(L"bthenum\\{00001124-0000-1000-8000-00805f9b34fb}_vid&0001004c_pid&026c\\d&15b70467&0&c4141104498e_c00000000"));
            Assert::AreEqual(0x026Cu, MkpProductIdFromUsbDeviceId(L"usb\\vid_05ac&pid_026c\\f0t125100bwjkpgas"));
        }

        TEST_METHOD(TheModelIsKnownWithoutASerialNumberToo) {
            Assert::AreEqual(0x026Cu, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_026C\\5&1A2B3C4D&0&2"));
        }

        TEST_METHOD(AnotherEnumeratorOrTheIdOfAnInterfaceHasNone) {
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(kUsbDevice));
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L"HID\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C&COL01\\E&D9CCAE&1A&0000"));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(kBluetoothHid));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_026C&MI_01\\9&286A66C3&1&0001"));
        }

        TEST_METHOD(AProductIdWithoutFourHexDigitsIsNone) {
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&02G6\\D&15B70467&0&C4141104498E_C00000000"));
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&26C\\D&15B70467&0&C4141104498E_C00000000"));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_02G6\\F0T125100BWJKPGAS"));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_26C\\F0T125100BWJKPGAS"));
        }

        TEST_METHOD(AnIdThatEndsWithTheProductIdIsNone) {
            // The product ID is the end of the first segment: the ID has to go on after it.
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L"USB\\VID_05AC&PID_026C"));
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L"BTHENUM\\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C"));
        }

        TEST_METHOD(EmptyAndMissingIdsAreNone) {
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L""));
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(nullptr));
            Assert::AreEqual(0u, MkpProductIdFromBluetoothDeviceId(L"BTHENUM\\"));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L""));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(nullptr));
            Assert::AreEqual(0u, MkpProductIdFromUsbDeviceId(L"USB\\"));
        }
    };

    TEST_CLASS(IdentityListTests) {
    public:
        TEST_METHOD(FindsAnyStringOfTheList) {
            const wchar_t list[] = L"bt:C4141104498E\0usb:F0T125100BWJKPGAS\0";
            const unsigned int chars = sizeof(list) / sizeof(wchar_t);

            Assert::AreEqual(1, MkpIdentityListContains(list, chars, L"bt:C4141104498E"));
            Assert::AreEqual(1, MkpIdentityListContains(list, chars, L"usb:F0T125100BWJKPGAS"));
        }

        TEST_METHOD(ComparesWithoutRegardToCase) {
            const wchar_t list[] = L"bt:c4141104498e\0";

            Assert::AreEqual(1, MkpIdentityListContains(list, sizeof(list) / sizeof(wchar_t), L"BT:C4141104498E"));
        }

        TEST_METHOD(AStringThatOnlyStartsOrEndsLikeAnEntryDoesNotMatch) {
            const wchar_t list[] = L"bt:C4141104498E\0";
            const unsigned int chars = sizeof(list) / sizeof(wchar_t);

            Assert::AreEqual(0, MkpIdentityListContains(list, chars, L"bt:C4141104498"));
            Assert::AreEqual(0, MkpIdentityListContains(list, chars, L"bt:C4141104498E0"));
            Assert::AreEqual(0, MkpIdentityListContains(list, chars, L"t:C4141104498E"));
        }

        TEST_METHOD(AListWithoutItsFinalTerminatorsStillHasItsLastString) {
            // The registry does not promise the double terminator; the size of the data bounds the list.
            const wchar_t list[] = {L'b', L't', L':', L'A', L'A', L'\0', L'u', L's', L'b', L':', L'B', L'B'};

            Assert::AreEqual(1, MkpIdentityListContains(list, 12, L"usb:BB"));
        }

        TEST_METHOD(NothingBeyondTheSizeOfTheDataIsRead) {
            const wchar_t list[] = L"bt:AA\0usb:BB\0";

            Assert::AreEqual(0, MkpIdentityListContains(list, 6, L"usb:BB"));
            Assert::AreEqual(0, MkpIdentityListContains(list, 11, L"usb:BB"));
            Assert::AreEqual(1, MkpIdentityListContains(list, 12, L"usb:BB"));
        }

        TEST_METHOD(EmptyListsAndEmptyEntriesHaveNothing) {
            const wchar_t empty[] = L"\0";
            const wchar_t gaps[] = L"\0\0bt:AA\0\0";

            Assert::AreEqual(0, MkpIdentityListContains(empty, 1, L"bt:AA"));
            Assert::AreEqual(0, MkpIdentityListContains(nullptr, 0, L"bt:AA"));
            Assert::AreEqual(0, MkpIdentityListContains(gaps, sizeof(gaps) / sizeof(wchar_t), L""));
            Assert::AreEqual(1, MkpIdentityListContains(gaps, sizeof(gaps) / sizeof(wchar_t), L"bt:AA"));
        }
    };
}
