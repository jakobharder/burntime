@ECHO OFF

:: v1.2.3 -> 1.2.3
:: v1.2.3-rc1 -> 1.2.3-rc1
:: v1.2.3-1-gabcdef -> 1.2.3.1
:: v1.2.3-rc1-1-gabcdef -> 1.2.3.1
:: v1.2-1-gabcdef -> 1.2.0.1

FOR /F "tokens=1 delims=+" %%v IN ('git describe --tags') DO (
  FOR /F "tokens=1-4 delims=-" %%i IN ("%%v") DO (
    IF NOT "%%l" == "" CALL :EMIT_DISTANCE %%i %%k
    IF "%%l" == "" IF NOT "%%k" == "" CALL :EMIT_DISTANCE %%i %%j
    IF "%%k" == "" CALL :EMIT_TAG %%i %%j
  )
)
EXIT /B

:NORMALIZE_BASE
SET BASE=%~1
IF "%BASE:~0,1%" == "v" SET BASE=%BASE:~1%
EXIT /B

:EMIT_DISTANCE
CALL :NORMALIZE_BASE %~1
FOR /F "tokens=1-3 delims=." %%s IN ("%BASE%") DO (
  IF "%%u" == "" (ECHO %%s.%%t.0.%~2) ELSE (ECHO %%s.%%t.%%u.%~2)
)
EXIT /B

:EMIT_TAG
CALL :NORMALIZE_BASE %~1
IF "%~2" == "" (ECHO %BASE%) ELSE (ECHO %BASE%-%~2)
EXIT /B
