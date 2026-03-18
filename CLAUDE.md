# SecretShare App

One-time secret sharing service — paste a secret, get a pickup URL that works exactly once then self-destructs.

## Architecture
Two independently deployed services:

| Service | Path | URL |
|---|---|---|
| ASP.NET Core API | `SecretShareApi/` | https://secretshare-api.sebaslive.xyz |
| Vue 3 frontend | `SecretShareFrontEnd/` | https://secretshare.sebaslive.xyz |

## Stack
- **API:** ASP.NET Core, Entity Framework Core, PostgreSQL, Repository pattern
- **Frontend:** Vue 3 + TypeScript + Vite, Bootstrap Vue Next

## Deployment
- **Platform:** Railway
- **Database:** PostgreSQL hosted on Railway
- **Trigger:** push to `master` → Railway auto-builds both services from their Dockerfiles
- **Environment:** API reads `ConnectionStrings__DefaultConnection` from Railway variables

## How it works
1. User submits a secret → API stores it, returns a unique pickup URL
2. Recipient opens the pickup URL → secret is shown once, then deleted from the database
3. Unretrieved secrets are periodically cleaned up

## Local dev
```bash
# API
cd SecretShareApi/SecretShareApi
dotnet run

# Frontend
cd SecretShareFrontEnd
npm install
npm run dev
```
