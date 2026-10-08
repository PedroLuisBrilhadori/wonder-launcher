@echo off
rem Compila dinput8.dll (32 bits). Requer Python com o pacote ziglang:  python -m pip install ziglang
cd /d "%~dp0"
python -m ziglang cc -target x86-windows-gnu -O2 -shared -fno-builtin -fno-stack-protector ^
  -Wall -Wextra -o ..\dinput8.dll sync_patch.c -lkernel32 -luser32
if errorlevel 1 (echo FALHOU & exit /b 1)
del /q ..\*.lib ..\*.pdb 2>nul
echo OK: ..\dinput8.dll
