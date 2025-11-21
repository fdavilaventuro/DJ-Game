@echo off
cd /d "%~dp0"

yt-dlp "https://music.youtube.com/playlist?list=PL2SZb_BNUE8gqEwA_-QD8ym6BtURsRH2W&si=UCHKpMh56dtC2K4o"
if %ERRORLEVEL% neq 0 (
  echo yt-dlp failed with code %ERRORLEVEL%
  pause
  exit /b %ERRORLEVEL%
)

py -3 process_music.py
if %ERRORLEVEL% neq 0 (
  echo Python script failed with code %ERRORLEVEL%
  pause
  exit /b %ERRORLEVEL%
)

pause