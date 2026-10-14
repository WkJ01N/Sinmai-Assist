@echo off
setlocal
set "COMMIT_HASH=NOT SET"

echo Before Building Action...
where git >nul 2>nul
if errorlevel 1 (
    echo Git is not installed. Using default commit hash.
    set "COMMIT_HASH=NOT SET"
) else (
    for /f "tokens=1" %%i in ('git -C "%~dp0." rev-parse --short HEAD 2^>nul') do set "COMMIT_HASH=%%i"
)

for /f "usebackq delims=" %%t in (`powershell -Command "(Get-Date -Format 'o')"`) do (
    set TIMESTAMP=%%t
)

echo CommitHash="%COMMIT_HASH%"
echo BuildDate="%TIMESTAMP%"

> "%~dp0BuildInfo.cs" (
    echo namespace SinmaiAssist {
    echo     public static partial class BuildInfo {
    echo         public const string CommitHash = "%COMMIT_HASH%";
    echo         public const string BuildDate = "%TIMESTAMP%";
    echo     }
    echo }
)

endlocal
