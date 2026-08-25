@echo off
cd /d "%~dp0.."
echo Deteniendo CALE...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0free-ports.ps1"
echo Listo.
