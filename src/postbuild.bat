@echo off

echo Postbuild script: %0 %*

setlocal

set ProjectDir=%~dp0
if not exist "%ProjectDir%copyfilestolandis.flag" goto _end
echo Copying files to LANDIS-II extensions folder...
copy /B /Y "%ProjectDir%bin\%1\Landis.Extension.BaseBDA-v4.*" "C:\Program Files\LANDIS-II-v7\extensions"

:_end
endlocal
