#include <stdio.h>
#include <string.h>
#include <stdlib.h>

// Stubs for kernel types and functions
typedef unsigned char BYTE;
typedef unsigned long ULONG;
typedef unsigned long DWORD;
typedef int BOOLEAN;
#define TRUE 1
#define FALSE 0
#define RtlZeroMemory(dst, len) memset(dst, 0, len)

// Driver constants
#define MAX_KEYMAP_SIZE 128
#define MAX_MODMAP_SIZE 16
#define VIRTUAL_EJECT 0xF0
#define VIRTUAL_FN    0xF1

enum HidCodes
{
	HidKeyB = 0x5, HidKeyP = 0x13, HidKeyS = 0x16, HidKeyT = 0x17,
	HidF1 = 0x3a, HidF2 = 0x3b, HidF3 = 0x3c, HidF4 = 0x3d,
	HidF5 = 0x3e, HidF6 = 0x3f, HidF7 = 0x40, HidF8 = 0x41,
	HidF9 = 0x42, HidF10 = 0x43, HidF11 = 0x44, HidF12 = 0x45,
	HidF13 = 0x68, HidF14 = 0x69, HidF15 = 0x6a,
	HidF16 = 0x6b, HidF17 = 0x6c, HidF18 = 0x6d, HidF19 = 0x6e,
	HidF20 = 0x6f, HidF21 = 0x70, HidF22 = 0x71, HidF23 = 0x72, HidF24 = 0x73,
	HidDel = 0x4c,
	HidLeft = 0x50, HidHome = 0x4a, HidRight = 0x4f, HidEnd = 0x4d,
	HidUp = 0x52, HidPgUp = 0x4b, HidDown = 0x51, HidPgDown = 0x4e,
	HidEnter = 0x28, HidPrtScr = 0x46, HidScrLck = 0x47,
	HidPauseBreak = 0x48, HidInsert = 0x49,
	HidLCtrlMask = 0x1, HidRCtrlMask = 0x10,
	HidLAltMask = 0x4, HidRAltMask = 0x40,
	HidLCmdMask = 0x8, HidRCmdMask = 0x80,
	HidLShiftMask = 0x2, HidRShiftMask = 0x20
};

// Globals (same as driver)
DWORD g_dwFnLock = 0;
BYTE g_KeyMap[MAX_KEYMAP_SIZE] = { 0 };
ULONG g_KeyMapSize = 0;
BYTE g_ModMap[MAX_MODMAP_SIZE] = { 0 };
ULONG g_ModMapSize = 0;
BOOLEAN g_FakeFnActive = 0;

// Include the actual processing code
static BYTE LookupKeyMap(BYTE source)
{
	for (ULONG i = 0; i + 1 < g_KeyMapSize; i += 2)
	{
		if (g_KeyMap[i] == source)
			return g_KeyMap[i + 1];
	}
	return 0;
}

static void InjectKey(BYTE* buf, BYTE keyCode)
{
	for (int i = 2; i <= 7; i++)
	{
		if (!buf[i]) { buf[i] = keyCode; return; }
	}
}

void ProcessKeyBuffer(BYTE* buf, ULONG size)
{
    if (!buf || size < 9)
        return;

    BYTE* pModifier = &buf[0];
    BYTE* pSpecialKey = &buf[8];

    BOOLEAN fnPressed = (*pSpecialKey & 0x2) != 0;
    BOOLEAN ejectPressed = (*pSpecialKey & 0x1) != 0;
    *pSpecialKey = 0;

    BYTE fnTarget = LookupKeyMap(VIRTUAL_FN);

    // Apply modifier remapping
    if (g_ModMapSize >= 2)
    {
        BYTE original = *pModifier;
        BYTE mappedMask = 0;
        for (ULONG i = 0; i + 1 < g_ModMapSize; i += 2)
            mappedMask |= g_ModMap[i];

        *pModifier = original & ~mappedMask;
        for (ULONG i = 0; i + 1 < g_ModMapSize; i += 2)
        {
            if (original & g_ModMap[i])
                *pModifier |= g_ModMap[i + 1];
        }
    }

    // FnLock toggles F1..F12 <-> F13..F24
    BOOLEAN convertFKeys = (fnPressed && !fnTarget) ? (g_dwFnLock == 0) : (g_dwFnLock != 0);

    for (int i = 2; i <= 7; i++)
    {
        if (buf[i] >= HidF1 && buf[i] <= HidF12)
        {
            if (convertFKeys)
                buf[i] = (BYTE)(buf[i] - HidF1 + HidF13);
        }
    }

    // Process physical Fn+[key] combos for navigation/editing keys
    if (fnPressed && !fnTarget)
    {
        switch (buf[2])
        {
            case HidLeft:  buf[2] = HidHome;   break;
            case HidRight: buf[2] = HidEnd;    break;
            case HidUp:    buf[2] = HidPgUp;   break;
            case HidDown:  buf[2] = HidPgDown; break;
            case HidEnter: buf[2] = HidInsert; break;
            case HidKeyP:  buf[2] = HidPrtScr; break;
            case HidKeyB:  buf[2] = HidPauseBreak; break;
            case HidKeyS:  buf[2] = HidScrLck; break;
            default:
                if (*pModifier & HidLCtrlMask)
                {
                    *pModifier &= ~HidLCtrlMask;
                    *pModifier |= HidRCtrlMask;
                }
                break;
        }
    }

    // Apply KeyMap to regular keys
    for (int i = 2; i <= 7; i++)
    {
        if (buf[i] && buf[i] < 0xF0)
        {
            BYTE target = LookupKeyMap(buf[i]);
            if (target)
                buf[i] = target;
        }
    }

    // Inject Eject target
    if (ejectPressed)
    {
        BYTE target = LookupKeyMap(VIRTUAL_EJECT);
        if (target)
            InjectKey(buf, target);
    }

    // Inject Fn target (mapped Fn)
    if (fnPressed && fnTarget)
        InjectKey(buf, fnTarget);
}


///////////////////////////////////////////////////////////////////////////////
// Test framework
//

static int g_tests_passed = 0;
static int g_tests_failed = 0;

static void reset_globals(void)
{
	g_dwFnLock = 0;
	memset(g_KeyMap, 0, sizeof(g_KeyMap));
	g_KeyMapSize = 0;
	memset(g_ModMap, 0, sizeof(g_ModMap));
	g_ModMapSize = 0;
	g_FakeFnActive = 0;
}

// buf format: [modifier, reserved, key1..key6, specialKey]
static void make_buf(BYTE* buf, BYTE modifier, BYTE key1, BYTE specialKey)
{
	memset(buf, 0, 9);
	buf[0] = modifier;
	buf[2] = key1;
	buf[8] = specialKey;
}

static void print_buf(const char* label, BYTE* buf)
{
	printf("  %s: [mod=%02X res=%02X keys=%02X %02X %02X %02X %02X %02X spec=%02X]\n",
		label, buf[0], buf[1], buf[2], buf[3], buf[4], buf[5], buf[6], buf[7], buf[8]);
}

#define ASSERT_EQ(name, actual, expected) do { \
	if ((actual) != (expected)) { \
		printf("  FAIL: %s: expected 0x%02X, got 0x%02X\n", name, (int)(expected), (int)(actual)); \
		g_tests_failed++; \
		return; \
	} \
} while(0)

#define TEST_PASS() do { g_tests_passed++; printf("  PASS\n"); } while(0)

///////////////////////////////////////////////////////////////////////////////
// Tests
//

static void test_eject_default_maps_to_delete(void)
{
	printf("test_eject_default_maps_to_delete\n");
	reset_globals();
	BYTE km[] = { 0xF0, HidDel };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	make_buf(buf, 0, 0, 0x01); // Eject pressed
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidDel);
	ASSERT_EQ("specialKey cleared", buf[8], 0);
	TEST_PASS();
}

static void test_fn_arrow_left_to_home(void)
{
	printf("test_fn_arrow_left_to_home\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidLeft, 0x02); // Fn + Left
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidHome);
	TEST_PASS();
}

static void test_fn_arrow_right_to_end(void)
{
	printf("test_fn_arrow_right_to_end\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidRight, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidEnd);
	TEST_PASS();
}

static void test_fn_arrow_up_to_pgup(void)
{
	printf("test_fn_arrow_up_to_pgup\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidUp, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidPgUp);
	TEST_PASS();
}

static void test_fn_arrow_down_to_pgdown(void)
{
	printf("test_fn_arrow_down_to_pgdown\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidDown, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidPgDown);
	TEST_PASS();
}

static void test_fn_f1_to_f13(void)
{
	printf("test_fn_f1_to_f13\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidF1, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidF13);
	TEST_PASS();
}

static void test_fn_f12_to_f24(void)
{
	printf("test_fn_f12_to_f24\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidF12, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidF24);
	TEST_PASS();
}

static void test_fn_unknown_key_passthrough(void) {
	printf("test_fn_unknown_key_passthrough\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidKeyT, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 passthrough T", buf[2], HidKeyT);
	TEST_PASS();
}

static void test_fnlock_arrow_preserves_left(void)
{
	printf("test_fnlock_arrow_preserves_left\n");
	reset_globals();
	g_dwFnLock = 1;

	BYTE buf[9];
	make_buf(buf, 0, HidLeft, 0); // No Fn pressed, FnLock on
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1 left arrow preserved", buf[2], HidLeft);
	TEST_PASS();
}

static void test_fnlock_unknown_key_passthrough(void)
{
	printf("test_fnlock_unknown_key_passthrough\n");
	reset_globals();
	g_dwFnLock = 1;

	BYTE buf[9];
	make_buf(buf, 0, HidKeyT, 0); // FnLock on, T key (no Fn-combo)
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1 passthrough", buf[2], HidKeyT);
	TEST_PASS();
}

static void test_keymap_f13_to_prtscr(void)
{
	printf("test_keymap_f13_to_prtscr\n");
	reset_globals();
	BYTE km[] = { HidF13, HidPrtScr };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	make_buf(buf, 0, HidF13, 0);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidPrtScr);
	TEST_PASS();
}

static void test_keymap_multiple_remaps(void)
{
	printf("test_keymap_multiple_remaps\n");
	reset_globals();
	BYTE km[] = { HidF13, HidPrtScr, HidF14, HidScrLck, HidF15, HidPauseBreak, 0xF0, HidDel };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	// Test F14
	BYTE buf[9];
	make_buf(buf, 0, HidF14, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("F14->ScrLck", buf[2], HidScrLck);

	// Test F15
	make_buf(buf, 0, HidF15, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("F15->PauseBreak", buf[2], HidPauseBreak);

	TEST_PASS();
}

static void test_fn_mapped_to_insert(void)
{
	printf("test_fn_mapped_to_insert\n");
	reset_globals();
	BYTE km[] = { VIRTUAL_FN, HidInsert };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	make_buf(buf, 0, 0, 0x02); // Fn pressed alone
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1=Insert", buf[2], HidInsert);
	TEST_PASS();
}

static void test_fn_mapped_no_fn_combos(void)
{
	printf("test_fn_mapped_no_fn_combos\n");
	reset_globals();
	BYTE km[] = { VIRTUAL_FN, HidInsert };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	memset(buf, 0, 9);
	buf[2] = HidLeft;  // Left arrow
	buf[8] = 0x02;     // Fn pressed
	ProcessKeyBuffer(buf, 9);

	// Fn is mapped, so it should NOT act as modifier (FnLock is off)
	// Left should stay as Left, and Insert should be injected
	ASSERT_EQ("key1=Left (not Home)", buf[2], HidLeft);
	ASSERT_EQ("key2=Insert (injected)", buf[3], HidInsert);
	TEST_PASS();
}

static void test_fn_mapped_with_fnlock(void)
{
	printf("test_fn_mapped_with_fnlock\n");
	reset_globals();
	g_dwFnLock = 1;
	BYTE km[] = { VIRTUAL_FN, HidInsert };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	make_buf(buf, 0, HidF1, 0); // F1, no Fn pressed, FnLock on
	ProcessKeyBuffer(buf, 9);

	// FnLock should activate F-key conversion (F1 -> F13)
	ASSERT_EQ("key1=F13 (FnLock)", buf[2], HidF13);
	TEST_PASS();
}

static void test_modmap_swap_alt_cmd(void)
{
	printf("test_modmap_swap_alt_cmd\n");
	reset_globals();
	BYTE mm[] = { 0x04, 0x08, 0x08, 0x04, 0x40, 0x80, 0x80, 0x40 };
	memcpy(g_ModMap, mm, sizeof(mm));
	g_ModMapSize = sizeof(mm);

	// LAlt pressed -> should become LCmd
	BYTE buf[9];
	make_buf(buf, HidLAltMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LAlt->LCmd", buf[0], HidLCmdMask);

	// LCmd pressed -> should become LAlt
	make_buf(buf, HidLCmdMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LCmd->LAlt", buf[0], HidLAltMask);

	// RAlt pressed -> should become RCmd
	make_buf(buf, HidRAltMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("RAlt->RCmd", buf[0], HidRCmdMask);

	// RCmd pressed -> should become RAlt
	make_buf(buf, HidRCmdMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("RCmd->RAlt", buf[0], HidRAltMask);

	TEST_PASS();
}

static void test_modmap_both_alt_cmd_pressed(void)
{
	printf("test_modmap_both_alt_cmd_pressed\n");
	reset_globals();
	BYTE mm[] = { 0x04, 0x08, 0x08, 0x04 };
	memcpy(g_ModMap, mm, sizeof(mm));
	g_ModMapSize = sizeof(mm);

	// Both LAlt+LCmd pressed -> both should swap, still both set
	BYTE buf[9];
	make_buf(buf, HidLAltMask | HidLCmdMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("both swapped", buf[0], HidLAltMask | HidLCmdMask);

	TEST_PASS();
}

static void test_modmap_preserves_unmapped_bits(void)
{
	printf("test_modmap_preserves_unmapped_bits\n");
	reset_globals();
	BYTE mm[] = { 0x04, 0x08, 0x08, 0x04 }; // Only LAlt<->LCmd
	memcpy(g_ModMap, mm, sizeof(mm));
	g_ModMapSize = sizeof(mm);

	// LAlt + LShift pressed -> LAlt swaps to LCmd, LShift preserved
	BYTE buf[9];
	make_buf(buf, HidLAltMask | HidLShiftMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LShift preserved + LCmd", buf[0], HidLCmdMask | HidLShiftMask);

	TEST_PASS();
}

static void test_eject_and_key_simultaneous(void)
{
	printf("test_eject_and_key_simultaneous\n");
	reset_globals();
	BYTE km[] = { 0xF0, HidDel };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	BYTE buf[9];
	memset(buf, 0, 9);
	buf[2] = HidKeyT; // T key in slot 1
	buf[8] = 0x01;    // Eject pressed
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1=T", buf[2], HidKeyT);
	ASSERT_EQ("key2=Del (injected)", buf[3], HidDel);
	TEST_PASS();
}

static void test_no_keymap_passthrough(void)
{
	printf("test_no_keymap_passthrough\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidKeyT, 0);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1 unchanged", buf[2], HidKeyT);
	TEST_PASS();
}

static void test_keymap_chain_with_fnlock(void)
{
	printf("test_keymap_chain_with_fnlock\n");
	reset_globals();
	g_dwFnLock = 1;
	BYTE km[] = { HidF13, HidPrtScr };
	memcpy(g_KeyMap, km, sizeof(km));
	g_KeyMapSize = sizeof(km);

	// F1 + FnLock -> Fn-combo makes F1->F13, then KeyMap makes F13->PrtScr
	BYTE buf[9];
	make_buf(buf, 0, HidF1, 0);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("F1->F13->PrtScr", buf[2], HidPrtScr);
	TEST_PASS();
}

static void test_fn_enter_to_insert(void)
{
	printf("test_fn_enter_to_insert\n");
	reset_globals();

	BYTE buf[9];
	make_buf(buf, 0, HidEnter, 0x02);
	ProcessKeyBuffer(buf, 9);

	ASSERT_EQ("key1", buf[2], HidInsert);
	TEST_PASS();
}

///////////////////////////////////////////////////////////////////////////////
// Main
//

int main(void)
{
	printf("=== WinAppleKey KeyProcessor Tests ===\n\n");

	test_eject_default_maps_to_delete();
	test_fn_arrow_left_to_home();
	test_fn_arrow_right_to_end();
	test_fn_arrow_up_to_pgup();
	test_fn_arrow_down_to_pgdown();
	test_fn_f1_to_f13();
	test_fn_f12_to_f24();
	test_fn_enter_to_insert();
	test_fn_unknown_key_passthrough();
	test_fnlock_arrow_preserves_left();
	test_fnlock_unknown_key_passthrough();
	test_keymap_f13_to_prtscr();
	test_keymap_multiple_remaps();
	test_keymap_chain_with_fnlock();
	test_fn_mapped_to_insert();
	test_fn_mapped_no_fn_combos();
	test_fn_mapped_with_fnlock();
	test_modmap_swap_alt_cmd();
	test_modmap_both_alt_cmd_pressed();
	test_modmap_preserves_unmapped_bits();
	test_eject_and_key_simultaneous();
	test_no_keymap_passthrough();

	printf("\n=== Results: %d passed, %d failed ===\n",
		g_tests_passed, g_tests_failed);

	return g_tests_failed ? 1 : 0;
}
