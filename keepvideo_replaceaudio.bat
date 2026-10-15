@echo off
setlocal EnableExtensions DisableDelayedExpansion
pushd "%~dp0" || exit /b 1

where ffmpeg >nul 2>&1
if errorlevel 1 (
    echo ERROR: ffmpeg.exe was not found. Add FFmpeg to PATH.
    pause
    popd
    exit /b 1
)
where ffprobe >nul 2>&1
if errorlevel 1 (
    echo ERROR: ffprobe.exe was not found. Add FFmpeg to PATH.
    pause
    popd
    exit /b 1
)

set "found=0"
set "failed=0"

for %%F in (*.mp4 *.mkv *.mov *.avi *.webm *.m4v) do (
    if exist "%%~fF" call :process "%%~fF"
)

echo.
if "%found%"=="0" (
    echo No source videos found in this folder.
    pause
    popd
    exit /b 1
)
if "%failed%"=="1" (
    echo Finished, but one or more files could not be converted.
    pause
    popd
    exit /b 1
)
echo Done! Original files were left untouched.
pause
popd
exit /b 0

:process
set "name=%~n1"
rem Ignore this script's own output on future runs.
if /i "%name:~-4%"=="_PCM" exit /b 0
set "found=1"

echo.
echo ==================================================
echo Processing: %~nx1
echo ==================================================

rem Skip videos without audio tracks.
ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 "%~f1" | findstr /r "^[0-9]" >nul
if errorlevel 1 (
    echo SKIPPED: No audio streams detected.
    exit /b 0
)

rem Copy the video unchanged and convert ALL audio tracks to 48kHz 16-bit PCM.
rem MOV supports uncompressed PCM; standard MP4 is not suitable for this.
ffmpeg -hide_banner -loglevel warning -stats -nostdin -y ^
    -i "%~f1" ^
    -map 0:V:0 -map 0:a ^
    -c:v copy -c:a pcm_s16le -ar 48000 ^
    "%~dpn1_PCM.mov"

if errorlevel 1 (
    echo FAILED: %~nx1
    if exist "%~dpn1_PCM.mov" del /q "%~dpn1_PCM.mov" >nul 2>&1
    set "failed=1"
) else (
    echo CREATED: %~n1_PCM.mov
)
exit /b 0
