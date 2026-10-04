@echo off
title Ori Coop Dedicated Server
cd /d "%~dp0"

echo ===================================================
echo       ORI COOP PLUS - DEDICATED SERVER
echo ===================================================
echo.

OriCoopDedicatedServer.exe --auto --max-players 4 --port 7777

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ===================================================
    echo [ERRO] O servidor nao pode ser iniciado!
    echo Se a mensagem acima mencionar '.NET 8.0 not found':
    echo Baixe e instale o .NET 8.0 Runtime (x64) da Microsoft:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo ===================================================
    echo.
)

pause
