#include <stdio.h>
#include "test_stubs.h"

DWORD g_dwFnLock = 0;
BYTE g_KeyMap[MAX_KEYMAP_SIZE] = { 0 };
ULONG g_KeyMapSize = 0;
BYTE g_ModMap[MAX_MODMAP_SIZE] = { 0 };
ULONG g_ModMapSize = 0;

static int g_tests_passed = 0;
static int g_tests_failed = 0;

static void reset_globals(void)
{
	g_dwFnLock = 0;
	memset(g_KeyMap, 0, sizeof(g_KeyMap));
	g_KeyMapSize = 0;
	memset(g_ModMap, 0, sizeof(g_ModMap));
	g_ModMapSize = 0;
}

static void make_buf(BYTE* buf, BYTE modifier, BYTE key1, BYTE specialKey)
{
	memset(buf, 0, 9);
	buf[0] = modifier;
	buf[2] = key1;
	buf[8] = specialKey;
}

#define ASSERT_EQ(name, actual, expected) do { \
	if ((actual) != (expected)) { \
		printf("  FAIL: %s: expected 0x%02X, got 0x%02X\n", name, (int)(expected), (int)(actual)); \
		g_tests_failed++; return; \
	} \
} while(0)

#define TEST_PASS() do { g_tests_passed++; printf("  PASS\n"); } while(0)

static void test_eject_default_maps_to_delete(void) {
	printf("test_eject_default_maps_to_delete\n");
	reset_globals();
	BYTE km[] = { 0xF0, HidDel }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; make_buf(buf, 0, 0, 0x01);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidDel);
	ASSERT_EQ("specialKey cleared", buf[8], 0);
	TEST_PASS();
}

static void test_fn_arrow_left_to_home(void) {
	printf("test_fn_arrow_left_to_home\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidLeft, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidHome); TEST_PASS();
}

static void test_fn_arrow_right_to_end(void) {
	printf("test_fn_arrow_right_to_end\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidRight, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidEnd); TEST_PASS();
}

static void test_fn_arrow_up_to_pgup(void) {
	printf("test_fn_arrow_up_to_pgup\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidUp, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidPgUp); TEST_PASS();
}

static void test_fn_arrow_down_to_pgdown(void) {
	printf("test_fn_arrow_down_to_pgdown\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidDown, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidPgDown); TEST_PASS();
}

static void test_fn_f1_to_f13(void) {
	printf("test_fn_f1_to_f13\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidF1, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidF13); TEST_PASS();
}

static void test_fn_f12_to_f24(void) {
	printf("test_fn_f12_to_f24\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidF12, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidF24); TEST_PASS();
}

static void test_fn_enter_to_insert(void) {
	printf("test_fn_enter_to_insert\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidEnter, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidInsert); TEST_PASS();
}

static void test_fn_unknown_key_passthrough(void) {
	printf("test_fn_unknown_key_passthrough\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidKeyT, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 passthrough T", buf[2], HidKeyT);
	TEST_PASS();
}

static void test_fnlock_arrow_preserves_left(void) {
	printf("test_fnlock_arrow_preserves_left\n"); reset_globals(); g_dwFnLock = 1;
	BYTE buf[9]; make_buf(buf, 0, HidLeft, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 left arrow preserved", buf[2], HidLeft); TEST_PASS();
}

static void test_fnlock_f1_to_f13(void) {
	printf("test_fnlock_f1_to_f13\n"); reset_globals(); g_dwFnLock = 1;
	BYTE buf[9]; make_buf(buf, 0, HidF1, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 F1->F1", buf[2], HidF1); TEST_PASS();
}

static void test_fnlock_fn_plus_f1_reverts_to_f1(void) {
	printf("test_fnlock_fn_plus_f1_reverts_to_f1\n"); reset_globals(); g_dwFnLock = 1;
	BYTE buf[9]; make_buf(buf, 0, HidF1, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 Fn+F1->F13", buf[2], HidF13); TEST_PASS();
}

static void test_fnlock_unknown_key_passthrough(void) {
	printf("test_fnlock_unknown_key_passthrough\n"); reset_globals(); g_dwFnLock = 1;
	BYTE buf[9]; make_buf(buf, 0, HidKeyT, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 passthrough", buf[2], HidKeyT); TEST_PASS();
}

static void test_keymap_f13_to_prtscr(void) {
	printf("test_keymap_f13_to_prtscr\n"); reset_globals();
	BYTE km[] = { HidF13, HidPrtScr }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; make_buf(buf, 0, HidF13, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1", buf[2], HidPrtScr); TEST_PASS();
}

static void test_keymap_multiple_remaps(void) {
	printf("test_keymap_multiple_remaps\n"); reset_globals();
	BYTE km[] = { HidF13, HidPrtScr, HidF14, HidScrLck, HidF15, HidPauseBreak };
	memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9];
	make_buf(buf, 0, HidF14, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("F14->ScrLck", buf[2], HidScrLck);
	make_buf(buf, 0, HidF15, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("F15->PauseBreak", buf[2], HidPauseBreak);
	TEST_PASS();
}

static void test_keymap_chain_with_fnlock(void) {
	printf("test_keymap_chain_with_fnlock\n"); reset_globals(); g_dwFnLock = 1;
	BYTE km[] = { HidF13, HidPrtScr }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; make_buf(buf, 0, HidF1, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("F1->F13->PrtScr", buf[2], HidPrtScr); TEST_PASS();
}

static void test_fn_mapped_to_insert(void) {
	printf("test_fn_mapped_to_insert\n"); reset_globals();
	BYTE km[] = { VIRTUAL_FN, HidInsert }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; make_buf(buf, 0, 0, 0x02);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1=Insert", buf[2], HidInsert); TEST_PASS();
}

static void test_fn_mapped_no_fn_combos(void) {
	printf("test_fn_mapped_no_fn_combos\n"); reset_globals();
	BYTE km[] = { VIRTUAL_FN, HidInsert }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; memset(buf, 0, 9); buf[2] = HidLeft; buf[8] = 0x02;
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1=Left (not Home)", buf[2], HidLeft);
	ASSERT_EQ("key2=Insert (injected)", buf[3], HidInsert);
	TEST_PASS();
}

static void test_fn_mapped_with_fnlock(void) {
	printf("test_fn_mapped_with_fnlock\n"); reset_globals(); g_dwFnLock = 1;
	BYTE km[] = { VIRTUAL_FN, HidInsert }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; make_buf(buf, 0, HidLeft, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1=Home (FnLock)", buf[2], HidHome); TEST_PASS();
}

static void test_modmap_swap_alt_cmd(void) {
	printf("test_modmap_swap_alt_cmd\n"); reset_globals();
	BYTE mm[] = { 0x04,0x08, 0x08,0x04, 0x40,0x80, 0x80,0x40 };
	memcpy(g_ModMap, mm, sizeof(mm)); g_ModMapSize = sizeof(mm);
	BYTE buf[9];
	make_buf(buf, HidLAltMask, 0, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LAlt->LCmd", buf[0], HidLCmdMask);
	make_buf(buf, HidLCmdMask, 0, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LCmd->LAlt", buf[0], HidLAltMask);
	make_buf(buf, HidRAltMask, 0, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("RAlt->RCmd", buf[0], HidRCmdMask);
	make_buf(buf, HidRCmdMask, 0, 0); ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("RCmd->RAlt", buf[0], HidRAltMask);
	TEST_PASS();
}

static void test_modmap_both_alt_cmd_pressed(void) {
	printf("test_modmap_both_alt_cmd_pressed\n"); reset_globals();
	BYTE mm[] = { 0x04, 0x08, 0x08, 0x04 }; memcpy(g_ModMap, mm, sizeof(mm)); g_ModMapSize = sizeof(mm);
	BYTE buf[9]; make_buf(buf, HidLAltMask | HidLCmdMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("both swapped", buf[0], HidLAltMask | HidLCmdMask); TEST_PASS();
}

static void test_modmap_preserves_unmapped_bits(void) {
	printf("test_modmap_preserves_unmapped_bits\n"); reset_globals();
	BYTE mm[] = { 0x04, 0x08, 0x08, 0x04 }; memcpy(g_ModMap, mm, sizeof(mm)); g_ModMapSize = sizeof(mm);
	BYTE buf[9]; make_buf(buf, HidLAltMask | HidLShiftMask, 0, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("LShift+LCmd", buf[0], HidLCmdMask | HidLShiftMask); TEST_PASS();
}

static void test_eject_and_key_simultaneous(void) {
	printf("test_eject_and_key_simultaneous\n"); reset_globals();
	BYTE km[] = { 0xF0, HidDel }; memcpy(g_KeyMap, km, sizeof(km)); g_KeyMapSize = sizeof(km);
	BYTE buf[9]; memset(buf, 0, 9); buf[2] = HidKeyT; buf[8] = 0x01;
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1=T", buf[2], HidKeyT);
	ASSERT_EQ("key2=Del", buf[3], HidDel);
	TEST_PASS();
}

static void test_no_keymap_passthrough(void) {
	printf("test_no_keymap_passthrough\n"); reset_globals();
	BYTE buf[9]; make_buf(buf, 0, HidKeyT, 0);
	ProcessKeyBuffer(buf, 9);
	ASSERT_EQ("key1 unchanged", buf[2], HidKeyT); TEST_PASS();
}

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
	test_fnlock_f1_to_f13();
	test_fnlock_fn_plus_f1_reverts_to_f1();
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
	printf("\n=== Results: %d passed, %d failed ===\n", g_tests_passed, g_tests_failed);
	return g_tests_failed ? 1 : 0;
}
