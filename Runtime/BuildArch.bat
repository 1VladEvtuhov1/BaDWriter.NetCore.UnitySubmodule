@echo off
setlocal

rem Имя выходного файла
set "output_file=ResultArchText.txt"

rem Удаляем старый результат
if exist "%output_file%" del "%output_file%"

echo Составляем структуру проекта...

rem Имя корневой папки (той, где лежит батник)
for %%I in (.) do set "root_name=%%~nxI"

rem Пишем корень
echo %root_name%>"%output_file%"
echo.>>"%output_file%"

rem Обход содержимого корня
call :walk "." ""

echo Готово! Результат сохранен в %output_file%
pause
exit /b


rem ============= РЕКУРСИВНЫЙ ОБХОД ПАПОК =============
:walk
set "folder=%~1"
set "prefix=%~2"

rem 1. Файлы .cs в текущей папке
for %%F in ("%folder%\*.cs") do (
    if exist "%%F" (
        >>"%output_file%" echo %prefix%- %%~nxF
    )
)

rem 2. Подпапки
for /d %%D in ("%folder%\*") do (
    >>"%output_file%" echo %prefix%- %%~nxD
    call :walk "%%~fD" "%prefix%   "
)

exit /b
