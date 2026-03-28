@echo off
setlocal

rem Keep the working directory pinned to the repository root so relative paths
rem inside the launcher and Python client resolve deterministically.
cd /d "%~dp0"

set "PYTHONUTF8=1"

where py >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    py -3 "python_client\examples\launch_agent_gui.py"
) else (
    python "python_client\examples\launch_agent_gui.py"
)

endlocal
