// Kernel-only glue between plain-C Driver.c/Driver.h and the portable
// KeyProcessor class (KeyProcessor.h/.cpp, no kernel headers, also compiled
// into the KeyProcessor.Tests project). Keeps VHFHANDLE/VhfReadReportSubmit/
// pool allocation out of KeyProcessor itself so that class stays unit-testable
// in user mode.
#include "Driver.h"
#include "KeyProcessor.h"

// Kernel mode has no <new> header (no CRT backing it) - placement new must be
// declared by hand. This overload takes no allocator action, it only lets us
// construct KeyProcessor into memory we already got from ExAllocatePoolWithTag.
inline void* operator new(size_t, void* location) noexcept {
    return location;
}

namespace {
    void SubmitConsumerUsageToVhf(USHORT usage, VHFHANDLE vhfHandle) {
        if (!vhfHandle) {
            return;
        }

        // reportBuffer must start with the report ID byte itself, followed by the report data.
        UCHAR reportBuffer[3] = {2, (UCHAR)(usage & 0xFF), (UCHAR)(usage >> 8)};
        HID_XFER_PACKET packet;
        packet.reportBuffer = reportBuffer;
        packet.reportBufferLen = sizeof(reportBuffer);
        packet.reportId = 2;

        NTSTATUS status = VhfReadReportSubmit(vhfHandle, &packet);
        if (!NT_SUCCESS(status)) {
            DebugPrint("Submit consumer usage: VhfReadReportSubmit failed: 0x%x\n", status);
        } else {
            DebugPrint("Submit consumer usage: Success. Usage 0x%04X\n", usage);
        }
    }
}

extern "C" {

PVOID KeyProcessorCreate(void) {
    void* mem = ExAllocatePoolWithTag(NonPagedPoolNx, sizeof(KeyProcessor), 'pKyT');
    if (!mem) {
        return nullptr;
    }
    return new (mem) KeyProcessor();
}

void KeyProcessorDestroy(PVOID handle) {
    if (!handle) {
        return;
    }
    static_cast<KeyProcessor*>(handle)->~KeyProcessor();
    ExFreePool(handle);
}

void KeyProcessorLoadConfig(PVOID handle, DWORD fnLock,
        const BYTE* keyMap, ULONG keyMapSize,
        const BYTE* modMap, ULONG modMapSize,
        const BYTE* specialModMap, ULONG specialModMapSize) {
    static_cast<KeyProcessor*>(handle)->LoadConfig(fnLock, keyMap, keyMapSize, modMap, modMapSize, specialModMap, specialModMapSize);
}

void KeyProcessorProcess(PVOID handle, BYTE* buf, ULONG size, VHFHANDLE vhfHandle) {
    ConsumerUsageSubmission submission = static_cast<KeyProcessor*>(handle)->Process(buf, size);
    if (submission.ShouldSubmit) {
        SubmitConsumerUsageToVhf(submission.Usage, vhfHandle);
    }
}

} // extern "C"
