# Release Checklist

## 1. Deploy the API

This repo now includes Render deployment config:

- `render.yaml`
- `VinhKhanhTour.API/Dockerfile`

Recommended path:

1. Push this repo to GitHub.
2. Create a Render account and connect the GitHub repo.
3. Create a Blueprint from `render.yaml`.
4. In Render, set `SUPABASE_CONNECTION_STRING` as a secret environment variable.
5. Wait for the web service to finish deploying.
6. Open `https://<your-render-service>.onrender.com/health` and confirm it returns `{"status":"ok"}`.

Render docs used for this setup:

- Blueprint YAML reference: https://render.com/docs/blueprint-spec
- Docker deploys: https://render.com/docs/docker
- Environment variables: https://render.com/docs/environment-variables

## 2. Build the Android APK

Once Render gives you a public API URL, build the APK with that URL baked in:

```powershell
.\scripts\publish-android-release.ps1 -HostedApiBaseUrl https://your-service.onrender.com
```

This passes the API URL into the MAUI build so the APK no longer depends on:

- `localhost`
- `127.0.0.1`
- `10.0.2.2`
- USB
- `adb reverse`
- manual API URL entry

## 3. Final smoke test

Install the generated APK on a phone that is **not** connected to your dev machine.

Verify:

1. App opens without asking for API URL.
2. POI data loads from the server.
3. Free trial works.
4. Paid plan flow creates a request in CMS.
5. CMS approval updates the app payment status.

## 4. Important note

If `HostedApiBaseUrl` is missing for a release Android build, the app now blocks startup with a clear configuration warning instead of silently falling back to dev/local behavior.
