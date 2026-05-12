# RAG-A-Muffin

A local, privacy-first RAG (Retrieval-Augmented Generation) agent that runs on Raspberry Pi or any local device. Feed it your emails, documents, and personal data, then query it with AI assistance while keeping everything on your device.

## Why RAG-A-Muffin?

- **100% Private**: Your data never leaves your device. No cloud uploads, no third-party APIs processing your personal information.
- **Local AI**: Uses Ollama for on-device LLM inference. Run open-source models like Mistral, Llama, or others entirely locally.
- **Efficient Retrieval**: Powered by Qdrant vector database for fast, semantic search across your documents.
- **Lightweight**: Designed to run on Raspberry Pi and other resource-constrained devices.
- **Open Source**: Full transparency into how your data is processed.

## Features

- 📄 **Document Upload**: Ingest emails, PDFs, text files, and other documents
- 🔍 **Semantic Search**: Find relevant information across your personal data
- 🤖 **AI-Powered Queries**: Ask questions in natural language and get contextual answers
- 🗃️ **Vector Embeddings**: Automatic chunking and embedding of documents
- 🔐 **Privacy by Default**: Everything runs locally with zero external dependencies
- 📡 **REST API**: Easy-to-use HTTP endpoints for integration

## Prerequisites

- **Docker & Docker Compose** (recommended for easiest setup)
  - [Install Docker Desktop](https://www.docker.com/products/docker-desktop)
  - Includes both Docker and Docker Compose

Or, for local development:

- **.NET 8 SDK** or later
- **Qdrant** (vector database)
- **Ollama** (local LLM runtime)

## Quick Start (Docker)

The easiest way to get started is with Docker Compose, which automatically sets up all services.

```bash
docker compose up -d
```

This will start:
- **Qdrant** (vector database) on `http://localhost:6333`
- **Ollama** (LLM runtime) on `http://localhost:11434`
- **RAG-A-Muffin API** on `http://localhost:8000`

### Access the API

Once running, the API documentation is available at:
- [Swagger UI](http://localhost:8000/swagger/index.html)

## Gmail Setup

This app uses your own Google Cloud credentials to access your Gmail. This keeps
your data entirely local — your emails never touch any external server.

### Step 1 — Create a Google Cloud Project

1. Go to [console.cloud.google.com](https://console.cloud.google.com) and sign in
2. Click **Select a project → New Project**, give it any name, click **Create**

### Step 2 — Enable the Gmail API

1. In the left menu go to **APIs & Services → Library**
2. Search for **Gmail API** and click **Enable**

### Step 3 — Create OAuth Credentials

1. Go to **APIs & Services → Credentials → Create Credentials → OAuth client ID**
2. If prompted to configure a consent screen first, click **Configure Consent Screen**:
   - Choose **External**, click **Create**
   - Fill in an app name (anything) and your email, click **Save and Continue** through the remaining screens
3. Back in Create OAuth client ID, select **Desktop app** as the application type
4. Click **Create**, then **Download JSON**
5. Rename the downloaded file to `credentials.json` and place it in the app's root folder

### Step 4 — Run the App

```bash
docker compose up --build
```

### Authenticate with your Gmail

1. Open the authorization URL in your browser:
   - `http://localhost:8000/authorize?userId=you@example.com`
2. Sign in with your Google account.
3. After consent, Google redirects back to `/oauth2callback` and the app stores a refresh token locally.

On first launch, your browser will open a Google sign-in page. Sign in with the
Gmail account you want to query. Google may show an "unverified app" warning —
this is expected since these are your own credentials. Click **Continue**.

Your login is saved locally and won't be required again.

If there is no stored credential, `GET /inbox?userId=you@example.com` will return a JSON response containing an authorization URL.

## Local Development

### Install Dependencies

1. **Install .NET 8**: https://dotnet.microsoft.com/download/dotnet/8.0

2. **Install Qdrant** (vector database):
   ```bash
   docker run -p 6333:6333 qdrant/qdrant
   ```

3. **Install Ollama** (local LLM):
   ```bash
   # Visit https://ollama.ai for installation instructions
   ollama serve
   # In another terminal, pull a model:
   ollama pull mistral
   ```

### Build & Run

```bash
cd rag-a-muffin
dotnet restore
dotnet build
dotnet run
```

The API will be available at `http://localhost:5000` (development) or `http://localhost:8000` (Docker).

## API Endpoints (still in development)

### Get Sample Document
```
GET /document
```
Returns a sample document for testing. Will eventually allow you to find documents and get the full pdf or other doc type.

### Upload Document (manually add a document not pulled in automatically. think scan or mail)
```
POST /upload
Content-Type: application/json

{
  "name": "My Document",
  "content": "Your document content here..."
}
```
Chunks the document and stores embeddings in Qdrant for later retrieval.

### Query with RAG
```
POST /query
Content-Type: application/json

{
  "question": "What did I discuss in my emails about project X?"
}
```
Returns AI-generated answer with relevant context from your documents.

See [Swagger UI](http://localhost:8000/swagger/index.html) for complete API documentation once running.

## Architecture

```
┌─────────────────────────────────────────────┐
│        RAG-A-Muffin (.NET 8 API)            │
│  - Document ingestion & chunking            │
│  - Query handling & orchestration           │
│  - REST API endpoints                       │
└────────┬──────────────────┬─────────────────┘
         │                  │
         ▼                  ▼
    ┌─────────┐        ┌──────────┐
    │ Qdrant  │        │  Ollama  │
    │ Vector  │        │   LLM    │
    │   DB    │        │ Inference│
    └─────────┘        └──────────┘
```

- **RAG-A-Muffin**: Core API service (ASP.NET Core)
- **Qdrant**: Vector database for storing and retrieving embeddings
- **Ollama**: Local LLM runtime for embeddings and inference

## Configuration

Configuration is managed through `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

Update connection strings and API endpoints as needed for your setup.

## Roadmap

- [ ] Email ingestion directly from IMAP/Exchange
- [ ] PDF text extraction
- [ ] Conversation history & multi-turn queries
- [ ] Document management UI
- [ ] Custom model selection
- [ ] Batch processing for large datasets
- [ ] Export & sharing (encrypted)

## Privacy & Security

- **No external calls**: All processing happens locally
- **No data persistence beyond Qdrant**: Documents are vectorized and stored only in the local vector database
- **No telemetry**: No tracking, logging to external services, or analytics
- **Open source**: Audit the code yourself

## Troubleshooting

### Services won't start in Docker
```bash
# Check if ports are already in use
lsof -i :6333  # Qdrant
lsof -i :11434 # Ollama
lsof -i :8000  # API

# Force restart everything
docker-compose down -v
docker-compose up --build
```

### Ollama model not found
```bash
# List available models
ollama list

# Pull a model
ollama pull mistral
```

### Connection refused errors
Ensure all services are running and healthy:
```bash
docker-compose ps
```

## Development

### Project Structure
```
rag-a-muffin/
├── Program.cs           # API setup & endpoints
├── Models/              # Data models
├── Services/            # Business logic (embedding, retrieval)
├── Repositories/        # Data access layer
├── Properties/          # Configuration
└── appsettings.json     # Settings
```

### Running Tests
```bash
dotnet test
```

### Building for Production
```bash
dotnet publish -c Release -o out
```

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request with a clear description

## License

MIT License - See LICENSE file for details

## Support

For issues, questions, or suggestions, please open an issue on GitHub.

---

**Keep your data private. Run AI locally. Stay in control.**
