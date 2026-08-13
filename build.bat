@echo off
REM ============================================================
REM  RoundedTB - build script
REM  Builds just the main app (RoundedTB.exe). The MSIX packaging
REM  project is intentionally NOT built (only needed for the Store).
REM
REM  Works with either:
REM    - "Build Tools for Visual Studio"  -> uses msbuild (same as CI)
REM    - ".NET SDK" (e.g. .NET 8)         -> uses dotnet
REM ============================================================
setlocal
cd /d "%~dp0"

where msbuild >nul 2>nul
if %errorlevel%==0 goto :with_msbuild

where dotnet >nul 2>nul
if %errorlevel%==0 goto :with_dotnet

echo [build.bat] ERROR: neither "msbuild" nor "dotnet" was found on PATH.
echo              Install either the ".NET SDK" or "Build Tools for Visual Studio",
echo              then run this script again.
exit /b 1

:with_msbuild
echo [build.bat] Building with MSBuild (Release)...
msbuild -restore -property:Configuration=Release -t:RoundedTB -verbosity:minimal
if errorlevel 1 goto :fail
goto :done

:with_dotnet
echo [build.bat] Building with dotnet (Release)...
dotnet build RoundedTB\RoundedTB.csproj -c Release
if errorlevel 1 goto :fail
goto :done

:fail
echo [build.bat] BUILD FAILED - see the errors above.
pause
exit /b 1

:done
echo [build.bat] Build OK.
echo              Output: RoundedTB\bin\Release\net8.0-windows10.0.19041\RoundedTB.exe
echo.
pause
endlocal
exit /b 0
