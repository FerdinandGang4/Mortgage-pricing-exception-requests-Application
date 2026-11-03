# Blue Harvest frontend

React + TypeScript startup application using Vite. This step contains the starter screen and build tooling only. Backend integration and pricing exception screens will be implemented separately.

## Run

From this directory, with Node.js and npm installed:

```powershell
npm.cmd install
npm.cmd run dev
```

Open the local URL printed by Vite (normally http://localhost:5173). On platforms other than Windows, use npm instead of npm.cmd.

## Verify

```powershell
npm.cmd run build
npm.cmd run lint
```

The build output is created in dist. Dependencies and build output are excluded from Git. Commit package-lock.json so subsequent installations can use npm.cmd ci.
