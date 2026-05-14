# Automated test guide

The production app uses Postgres/Supabase. SQLite and Docker are test-only tools:

- API tests use SQLite in memory by default for fast local feedback.
- CMS Playwright tests use a temporary SQLite file and start the CMS themselves.
- The nightly/manual CI job can run API tests against a Postgres container.

## Run all local tests

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1
```

Expected local result without a device:

- API integration tests pass.
- CMS Playwright E2E tests pass.
- MAUI Appium contract tests pass.
- Real Appium tests are skipped unless `RUN_APPIUM_TESTS=1`.

## Run real Appium smoke locally

Prerequisites:

- Android SDK platform-tools and an emulator or USB device.
- Node.js and Appium:

```powershell
npm install -g appium
appium driver install uiautomator2
```

Build the Android APK:

```powershell
dotnet publish .\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj -f net10.0-android -c Debug
```

Start an emulator or connect a device, then verify:

```powershell
adb devices
appium --base-path /
```

In a second PowerShell window:

```powershell
$env:RUN_APPIUM_TESTS = "1"
$env:APPIUM_SERVER_URL = "http://127.0.0.1:4723"
$env:APPIUM_APP_PATH = "$env:LOCALAPPDATA\VinhKhanhTourDemo\bin\VinhKhanhTourDemo\Debug\net10.0-android\android-x64\publish\com.companyname.vinhkhanhtourdemo-Signed.apk"

powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1 -WithAppium
```

Useful optional variables:

- `APPIUM_DEVICE_NAME`: emulator/device name shown by `adb devices`.
- `APPIUM_APP_PACKAGE`: defaults to `com.companyname.vinhkhanhtourdemo`.
- `APPIUM_APP_ACTIVITY`: use this instead of `APPIUM_APP_PATH` for an already-installed app.
- `APPIUM_NO_RESET=1`: keep app data between runs.

On failure, artifacts are written under `TestResults/artifacts/appium`:

- PNG screenshot
- XML page source

Keep one successful emulator screenshot in your report or slides as evidence that the real-device smoke path was executed.

## Run API tests on local Postgres

Use this when you want to compare SQLite behavior with real Postgres behavior:

```powershell
$env:API_TEST_DATABASE = "postgres"
$env:API_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;Pooling=false;Include Error Detail=true"

dotnet test .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj
```

The test factory creates a temporary database with a random name and drops it when the test process exits.
