@echo off
REM Windows batch wrapper for make commands

if "%1"=="" goto help
if "%1"=="help" goto help
if "%1"=="clone-all" goto clone-all
if "%1"=="validate-env" goto validate-env
if "%1"=="up" goto up
if "%1"=="down" goto down
if "%1"=="logs" goto logs
if "%1"=="seed" goto seed
goto help

:help
echo Gearify Umbrella - Available commands:
echo   make.bat clone-all     - Clone all repositories
echo   make.bat validate-env  - Check environment variables
echo   make.bat up            - Start all services
echo   make.bat down          - Stop all services
echo   make.bat logs          - View logs
echo   make.bat seed          - Seed databases
goto end

:clone-all
powershell -ExecutionPolicy Bypass -File scripts\clone-all.ps1
goto end

:validate-env
bash scripts\validate-env.sh
goto end

:up
docker compose up -d
goto end

:down
docker compose down
goto end

:logs
docker compose logs -f
goto end

:seed
powershell -ExecutionPolicy Bypass -File scripts\seed.ps1
goto end

:end
