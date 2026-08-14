@echo off
REM ============================================================
REM  RoundedTB - build script
REM  Builds just the main app (RoundedTB.exe). The MSIX packaging
REM  project is intentionally NOT built (only needed for the Store).
REM
REM  Usage: build.bat [canary|dev|master]   (default: canary)
REM    channel controls branding (icon/banner/subtitle/version/log):
REM      canary - debug/test build (default)
REM      dev    - prerelease build
REM      master - release build, e.g.  build.bat master
REM
REM  Works with either:
REM    - "Build Tools for Visual Studio"  -> uses msbuild (same as CI)
REM    - ".NET SDK" (e.g. .NET 8)         -> uses dotnet
REM ============================================================
setlocal
cd /d "%~dp0"

set "CHANNEL=%~1"
if "%CHANNEL%"=="" set "CHANNEL=canary"
if /i not "%CHANNEL%"=="canary" if /i not "%CHANNEL%"=="dev" if /i not "%CHANNEL%"=="master" (
    echo [build.bat] ERROR: unknown channel "%CHANNEL%". Use: canary ^| dev ^| master
    exit /b 1
)

where msbuild >nul 2>nul
if %errorlevel%==0 goto :with_msbuild

where dotnet >nul 2>nul
if %errorlevel%==0 goto :with_dotnet

echo [build.bat] ERROR: neither "msbuild" nor "dotnet" was found on PATH.
echo              Install either the ".NET SDK" or "Build Tools for Visual Studio",
echo              then run this script again.
exit /b 1

:with_msbuild
echo [build.bat] Building with MSBuild (Release, channel=%CHANNEL%)...
msbuild -restore -property:Configuration=Release -property:Channel=%CHANNEL% -t:RoundedTB -verbosity:minimal
if errorlevel 1 goto :fail
goto :done

:with_dotnet
echo [build.bat] Building with dotnet (Release, channel=%CHANNEL%)...
dotnet build RoundedTB\RoundedTB.csproj -c Release -p:Channel=%CHANNEL%
if errorlevel 1 goto :fail
goto :done

:fail
echo [build.bat] BUILD FAILED - see the errors above.
pause
exit /b 1

:done
echo [build.bat] Build OK (channel=%CHANNEL%).
echo              Output: RoundedTB\bin\Release\net8.0-windows10.0.19041\RoundedTB.exe
echo.
pause
endlocal
exit /b 0
