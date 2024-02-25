@ECHO OFF

:: v1.2.3 -> 1.2.3
:: v1.2.3-rc1 -> 1.2.3-rc1
:: v1.2.3-1-asdf -> 1.2.3-1-asdf

git describe --tags
FOR /F "tokens=1-2 delims==v+" %%i IN ('git describe --tags') DO (
  echo %%i
)
