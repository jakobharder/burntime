@ECHO OFF

:: v1.2.3 -> 1.2.3
:: v1.2.3-rc1 -> 1.2.3
:: v1.2.3-1-asdf -> 1.2.3.1
:: v1.2-1-asdf -> 1.2.0.1

FOR /F "tokens=1 delims==v+" %%n IN ('git describe --tags') DO (
  FOR /F "tokens=1-3 delims==-" %%i IN ("%%n") DO (
    IF "%%j%%k" == "" (
      echo %%i
    )
    IF NOT "%%j" == "" (
      IF "%%k" == "" (
        echo %%i
      )
      IF NOT "%%k" == "" (
        FOR /F "tokens=1-3 delims==." %%s IN ("%%i") DO (
          IF "%%u" == "" (
            echo %%i.0.%%j
          )
          IF NOT "%%u" == "" (
            echo %%i.%%j
          )
        )
      )
    )
  )
)