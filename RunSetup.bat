@echo off
title ZulfarakRPG - Setup via Unity Batch Mode
color 0A

set UNITY="C:\Program Files\Unity 6000.5.1f1\Editor\Unity.exe"
set PROJECT="C:\Users\henri\OneDrive\Área de Trabalho\ZulfarakRPG"
set LOGFILE="%TEMP%\zulfarak_setup.log"

echo ================================================
echo  ZulfarakRPG - Executando Setup Automatico
echo ================================================
echo.
echo Unity: %UNITY%
echo Projeto: %PROJECT%
echo Log: %LOGFILE%
echo.
echo Aguarde... (pode levar 2-5 minutos na primeira vez)
echo.

%UNITY% -batchmode -projectPath %PROJECT% -executeMethod ZulfarakSetupWizard.SetupAll -quit -logFile %LOGFILE%
set CODE=%ERRORLEVEL%

echo.
if %CODE%==0 (
    echo [OK] Setup All Assets concluido!
    echo Rodando Setup All Scenes...
    %UNITY% -batchmode -projectPath %PROJECT% -executeMethod SceneSetupWizard.SetupAllScenes -quit -logFile %LOGFILE%
    if !ERRORLEVEL!==0 (
        echo [OK] Setup All Scenes concluido!
        echo.
        echo ================================================
        echo  PRONTO! Abra o Unity Hub e pressione Play.
        echo ================================================
    ) else (
        echo [ERRO] Scene setup falhou. Veja o log: %LOGFILE%
    )
) else (
    echo [ERRO] Codigo de saida: %CODE%
    echo.
    echo O batch mode requer licenca Pro no Windows.
    echo.
    echo ALTERNATIVA: Apenas abra o projeto no Unity Hub.
    echo O AutoSetup.cs vai rodar tudo automaticamente!
    echo.
    echo Veja o log completo em: %LOGFILE%
)

echo.
pause
