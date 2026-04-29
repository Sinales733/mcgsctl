@echo off
setlocal
dotnet run --project "%~dp0McgsCtl.csproj" -- %*
exit /b %ERRORLEVEL%
