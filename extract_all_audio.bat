@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "failed=0"
set "foundVideo=0"

for %%F in (*.mp4 *.mkv *.mov *.avi *.webm *.m4v) do (
    if exist "%%F" (
        set "foundVideo=1"
        echo.
        echo ==================================================
        echo Verarbeite: %%F
        echo ==================================================

        set /a audioCount=0

        for /f "usebackq delims=" %%A in (`ffprobe -v error -select_streams a -show_entries stream^=index -of csv^=p^=0 "%%~fF"`) do (
            set /a audioCount+=1
        )

        if !audioCount! EQU 0 (
            echo Keine Audiospur gefunden.
        ) else (
            set /a lastAudio=audioCount-1

            for /l %%N in (0,1,!lastAudio!) do (
                set /a displayNumber=%%N+1

                echo.
                echo Exportiere Audiospur !displayNumber! von !audioCount!...

                ffmpeg -hide_banner -y ^
                    -i "%%~fF" ^
                    -map 0:a:%%N ^
                    -vn ^
                    -c:a pcm_s16le ^
                    -ar 48000 ^
                    "%%~dpnF_Audio!displayNumber!.wav"

                if errorlevel 1 (
                    echo FEHLER bei %%F - Audiospur !displayNumber!
                    set "failed=1"
                )
            )
        )
    )
)

echo.

if "%foundVideo%"=="0" (
    echo Keine unterstuetzten Videodateien im Ordner gefunden.
    pause
    exit /b 1
)

if "%failed%"=="1" (
    echo Mindestens ein Export ist fehlgeschlagen.
    pause
    exit /b 1
)

echo Alle Audiospuren wurden erfolgreich exportiert.

