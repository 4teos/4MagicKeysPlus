// Kernel-only glue between plain-C Driver.c/Driver.h and the portable
// KeyProcessor class (KeyProcessor.h/.cpp, no kernel headers, also compiled
// into the KeyProcessor.Tests project). Keeps VHFHANDLE/VhfReadReportSubmit/
// pool allocation out of KeyProcessor itself so that class stays unit-testable
// in user mode.
#include "Driver.h"
#include "KeyProcessor.h"

// Kernel mode has no <new> header (no CRT backing it) - placement new must be
// declared by hand. This overload takes no allocator action, it only lets us
// construct KeyProcessor into memory we already got from ExAllocatePool2.
inline void* operator new(size_t, void* location) noexcept {
    return location;
}

namespace {
    // A reload (passive level, from the control device) can come while Process() runs for a
    // report (up to dispatch level), so each instance carries the lock that keeps them apart.
    // KeyProcessor itself stays free of kernel types.
    struct KeyProcessorInstance {
        KSPIN_LOCK Lock;
        KeyProcessor Processor;
    };

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
    void* mem = ExAllocatePool2(POOL_FLAG_NON_PAGED, sizeof(KeyProcessorInstance), 'pKyT');
    if (!mem) {
        return nullptr;
    }
    KeyProcessorInstance* instance = new (mem) KeyProcessorInstance();
    KeInitializeSpinLock(&instance->Lock);
    return instance;
}

void KeyProcessorDestroy(PVOID handle) {
    if (!handle) {
        return;
    }
    static_cast<KeyProcessorInstance*>(handle)->~KeyProcessorInstance();
    ExFreePool(handle);
}

ULONG KeyProcessorLoadConfig(PVOID handle, DWORD fnLock, const KEY_RULE* rules, ULONG ruleCount) {
    KeyProcessorInstance* instance = static_cast<KeyProcessorInstance*>(handle);

    KIRQL oldIrql;
    KeAcquireSpinLock(&instance->Lock, &oldIrql);
    ULONG accepted = instance->Processor.LoadConfig(fnLock, rules, ruleCount);
    KeReleaseSpinLock(&instance->Lock, oldIrql);
    return accepted;
}

void KeyProcessorProcess(PVOID handle, BYTE* buf, ULONG size, VHFHANDLE vhfHandle) {
    KeyProcessorInstance* instance = static_cast<KeyProcessorInstance*>(handle);

    KIRQL oldIrql;
    KeAcquireSpinLock(&instance->Lock, &oldIrql);
    ConsumerUsageSubmission submission = instance->Processor.Process(buf, size);
    KeReleaseSpinLock(&instance->Lock, oldIrql);

    if (submission.ShouldSubmit) {
        SubmitConsumerUsageToVhf(submission.Usage, vhfHandle);
    }
}

} // extern "C"
