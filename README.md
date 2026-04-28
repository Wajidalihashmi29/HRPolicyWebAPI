# HRPolicyWebAPI

A local HR policy chat assistant powered by Azure Foundry / Azure OpenAI on a .NET backend and a React + Vite frontend.

## Overview

- `backend/` contains the ASP.NET Core Web API.
- `frontend/` contains the React/Vite chat app.
- The app streams assistant responses and attaches citations from uploaded policy documents.

## Features

- Streaming chat responses from Azure AI
- Inline citations shown under assistant responses
- A subtle warning note under the chat input
- CORS configured for local React development ports

## Requirements

- .NET 10 SDK
- Node.js (v18+ recommended)
- Azure credentials configured for `DefaultAzureCredential`
- Azure AI Projects project endpoint and model deployment

## Backend Setup

1. Copy the sample config:

```powershell
cd backend
copy appsettings.json.sample appsettings.json
```

2. Update `backend/appsettings.json` with your Azure OpenAI project details:

```json
"AzureOpenAI": {
  "ProjectEndpoint": "https://your-project-endpoint",
  "Deployment": "gpt-4.1-1"
}
```

3. Ensure any sensitive values remain local and are excluded from Git.

## Policy Document Setup

The backend currently uses a hardcoded document directory for knowledge uploads:

- `C:\AzureFoundry\HRPolicyWebAPI\Policy Documents`

Place your HR policy PDFs there before running the `/api/hragent/setup/knowledge-base` endpoint.

## Running the Backend

From the project root:

```powershell
cd backend
dotnet run
```

The API listens on the default ASP.NET port (usually `http://localhost:5157`).

### Useful backend endpoints

- `GET /api/hragent/health` — health check
- `POST /api/hragent/chat` — chat streaming endpoint
- `POST /api/hragent/setup/knowledge-base` — upload documents and create the vector store

## Frontend Setup

From the project root:

```powershell
cd frontend
npm install
npm run dev
```

The app will launch on a Vite dev port like `http://localhost:5173`.

## Local Development Notes

- The frontend fetches the backend at `http://localhost:5157/api/hragent/chat`.
- CORS is configured for `http://localhost:3000`, `http://localhost:5173`, and `http://localhost:5174`.
- If backend build outputs are tracked accidentally, add them to `.gitignore` and remove them from Git with `git rm --cached`.

## Troubleshooting

- If chat responses appear in the wrong order, ensure the frontend and backend are both running.
- If `backend/bin/Debug/net10.0/HRPolicyWebAPI.dll` is locked, stop the running backend process and try again.
- If the knowledge base setup fails, check that the `Policy Documents` folder exists and contains valid PDF files.

## Recommended Workflow

1. Start the backend with `dotnet run`.
2. Start the frontend with `npm run dev`.
3. Open the React app and ask HR policy questions.
4. If needed, run the knowledge base setup endpoint to refresh citations.

## License

This repository does not include a license file. Add one if you plan to share or publish the code.
