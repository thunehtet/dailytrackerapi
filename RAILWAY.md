# Railway deployment

This project is configured for Railway's Dockerfile builder.

## Deploy

1. Push the repository to GitHub, GitLab, or Bitbucket.
2. In Railway, create a project and choose **Deploy from GitHub repo** (or the matching provider).
3. Add the required service variable:

   ```text
   OPENAI_API_KEY=your-api-key
   ```

4. Deploy the service. Railway supplies `PORT`; the container listens on that port automatically.
5. In the service settings, generate a public domain if the API should be internet-accessible.

Railway checks `GET /health` during deployment. OpenAPI is intentionally available only in the Development environment.

## Test the image locally

```powershell
docker build -t daily-tracker-api .
docker run --rm -p 8080:8080 -e OPENAI_API_KEY=your-api-key daily-tracker-api
```

Then request `http://localhost:8080/health`.
