#include "Driver.h"
#include "KeyProcessor.h"

// Kernel mode has no <new>: placement new is declared by hand.
inline void* operator new(size_t, void* location) noexcept {
    return location;
}

namespace {
    // A reload (passive level) can come while Process() runs (up to dispatch level): the lock keeps them apart.
    struct KeyProcessorInstance {
        KSPIN_LOCK Lock;
        KeyProcessor Processor;
    };

    void SubmitConsumerUsageToVhf(USHORT usage, VHFHANDLE vhfHandle) {
        if (!vhfHandle) {
            return;
        }

        // reportBuffer starts with the report ID byte itself.
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
