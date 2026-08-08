@echo off
call "c:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x64 >nul 2>&1
cl /nologo /W3 /Fe:c:\Users\fedor\Documents\Projects\WinAppleKey\test_keyprocessor.exe c:\Users\fedor\Documents\Projects\WinAppleKey\test_keyprocessor.c
if %errorlevel% neq 0 exit /b %errorlevel%
c:\Users\fedor\Documents\Projects\WinAppleKey\test_keyprocessor.exe
