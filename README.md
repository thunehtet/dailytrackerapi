# Daily Tracker API

Stateless ASP.NET Core API for interpreting spending text and voice. It returns an
editable, unconfirmed expense proposal and never saves personal expense data.

## Endpoints

- `GET /health`
- `POST /api/v1/interpret/text`
- `POST /api/v1/interpret/voice`

Text request:

```json
{
  "text": "Lunch cost $12 today",
  "utcOffsetMinutes": 480
}
```

Voice is `multipart/form-data` with an `audio` file and optional
`utcOffsetMinutes`. Audio is limited to 10 MB and validated by content type.

Both endpoints return the same proposal shape. `confirmed` is always `false`; the
Flutter review screen is responsible for confirmation and local SQLite storage.

## Railway configuration

Deploy this project as a separate Railway service from Inventory Management. Add this
service variable:

```text
OPENAI_API_KEY=<secret key>
```

Do not put the key in `appsettings.json`, Flutter, the Inventory service, or source
control. No database service is required for these interpretation endpoints.

The default models are configured in `appsettings.json`:

```json
{
  "OpenAI": {
    "TextModel": "gpt-4o-mini",
    "TranscriptionModel": "gpt-4o-mini-transcribe"
  }
}
```

They can be overridden with Railway variables `OpenAI__TextModel` and
`OpenAI__TranscriptionModel`.

## Local run

```powershell
$env:OPENAI_API_KEY = '<secret key>'
dotnet run --project '.\daily tracker api\daily tracker api.csproj'
```

OpenAPI JSON is exposed in Development at `/openapi/v1.json`.
