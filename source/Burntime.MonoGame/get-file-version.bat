@ECHO OFF

:: v1.2.3 -> 1.2.3
:: v1.2.3-rc1 -> 1.2.3
:: v1.2.3-1-gabcdef -> 1.2.3.1
:: v1.2.3-rc1-1-gabcdef -> 1.2.3.1
:: v1.2-1-gabcdef -> 1.2.0.1

FOR /F "tokens=1 delims=+" %%v IN ('git describe --tags') DO (
  FOR /F "tokens=1-4 delims=-" %%i IN ("%%v") DO (
    IF NOT "%%l" == "" CALL :EMIT %%i %%k
    IF "%%l" == "" IF NOT "%%k" == "" CALL :EMIT %%i %%j
    IF "%%k" == "" CALL :EMIT %%i
  )
)
EXIT /B

:EMIT
SET BASE=%~1
IF "%BASE:~0,1%" == "v" SET BASE=%BASE:~1%
FOR /F "tokens=1-3 delims=." %%s IN ("%BASE%") DO (
  IF "%%u" == "" (
    IF "%~2" == "" (ECHO %%s.%%t.0) ELSE (ECHO %%s.%%t.0.%~2)
  ) ELSE (
    IF "%~2" == "" (ECHO %%s.%%t.%%u) ELSE (ECHO %%s.%%t.%%u.%~2)
  )
)
EXIT /B
