@echo off
REM ============================================================
REM  RoundedTB - build script
REM  Builds the main app (RoundedTB.exe). The MSIX packaging
REM  project is intentionally NOT built (only needed for the Store).
REM
REM  Usage:
REM    build.bat [canary|dev|master]
REM      channel controls branding (icon/banner/subtitle/version/log):
REM        canary - debug/test build (default)
REM        dev    - prerelease build
REM        master - release build, e.g.  build.bat master
REM
REM    build.bat --release --version <ver>
REM      Builds multi-arch release packages (x86 / x64 / arm64), each both
REM      self-contained (bundles the .NET runtime) and framework-dependent
REM      (requires .NET Desktop Runtime). Zips land in .\release\ next to
REM      this script. Example:
REM        build.bat --release --version R4.1.1
REM      -> RoundedTB-R4.1.1-win-x86.zip
REM         RoundedTB-R4.1.1-win-x86-framework-dependent.zip
REM         RoundedTB-R4.1.1-win-x64.zip
REM         RoundedTB-R4.1.1-win-x64-framework-dependent.zip
REM         RoundedTB-R4.1.1-win-arm64.zip
REM         RoundedTB-R4.1.1-win-arm64-framework-dependent.zip
REM
REM  Works with either:
REM    - "Build Tools for Visual Studio"  -> uses msbuild (same as CI)
REM    - ".NET SDK" (e.g. .NET 8)         -> uses dotnet
REM  (--release requires the .NET SDK.)
REM ============================================================
setlocal
cd /d "%~dp0"

REM ------------------------------------------------------------
REM  Parse arguments: --release, --version <ver>, [channel]
REM ------------------------------------------------------------
set "DO_RELEASE=0"
set "PKG_VER="
set "CHANNEL="

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="--release" (
    set "DO_RELEASE=1"
) else if /i "%~1"=="--version" (
    set "PKG_VER=%~2"
    shift
) else (
    set "CHANNEL=%~1"
)
shift
goto :parse_args
:args_done

if "%DO_RELEASE%"=="1" goto :release

REM ------------------------------------------------------------
REM  Normal single-channel build
REM ------------------------------------------------------------
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

REM ------------------------------------------------------------
REM  --release : multi-arch package build (x86 / x64 / arm64)
REM ------------------------------------------------------------
:release
if "%PKG_VER%"=="" (
    echo [build.bat] ERROR: --release requires --version ^<version^>. Example:
    echo              build.bat --release --version R4.1.1
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [build.bat] ERROR: --release requires the .NET SDK ^("dotnet" on PATH^).
    exit /b 1
)

REM Release packages always use the master channel (release branding/log).
set "CHANNEL=master"

set "PROJ=RoundedTB\RoundedTB.csproj"
set "RELEASE_DIR=%~dp0release"
set "STAGING_DIR=%RELEASE_DIR%\.staging"
set "ARCHS=x86 x64 arm64"

REM Remove any existing release folder and recreate it (staging included).
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%" >nul 2>nul

for %%A in (%ARCHS%) do (
    echo [build.bat] Publishing win-%%A ^(framework-dependent, channel=%CHANNEL%^)...
    dotnet publish "%PROJ%" -c Release -r win-%%A --self-contained false -p:Channel=%CHANNEL% -o "%STAGING_DIR%\win-%%A-fd" -v minimal
    if errorlevel 1 goto :release_fail

    echo [build.bat] Publishing win-%%A ^(self-contained, channel=%CHANNEL%^)...
    dotnet publish "%PROJ%" -c Release -r win-%%A --self-contained true -p:Channel=%CHANNEL% -o "%STAGING_DIR%\win-%%A-sc" -v minimal
    if errorlevel 1 goto :release_fail
)

for %%A in (%ARCHS%) do (
    echo [build.bat] Zipping RoundedTB-%PKG_VER%-win-%%A.zip...
    powershell -NoProfile -Command "Compress-Archive -Path '%STAGING_DIR%\win-%%A-sc\*' -DestinationPath '%RELEASE_DIR%\RoundedTB-%PKG_VER%-win-%%A.zip' -Force"
    if errorlevel 1 goto :release_fail

    echo [build.bat] Zipping RoundedTB-%PKG_VER%-win-%%A-framework-dependent.zip...
    powershell -NoProfile -Command "Compress-Archive -Path '%STAGING_DIR%\win-%%A-fd\*' -DestinationPath '%RELEASE_DIR%\RoundedTB-%PKG_VER%-win-%%A-framework-dependent.zip' -Force"
    if errorlevel 1 goto :release_fail
)

rmdir /s /q "%STAGING_DIR%"

echo.
echo [build.bat] Release packages OK (channel=%CHANNEL%, version=%PKG_VER%):
dir /b "%RELEASE_DIR%\*.zip"
echo.
pause
endlocal
exit /b 0

:release_fail
echo [build.bat] RELEASE BUILD FAILED - see the errors above.
pause
exit /b 1
