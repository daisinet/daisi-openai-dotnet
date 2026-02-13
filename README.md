# DAISI OpenAI API

An OpenAI-compatible REST API that translates requests to and from the DAISI network. This allows any tool built for the OpenAI API — the Python `openai` SDK, LangChain, AutoGen, Cursor, Continue, and others — to work with DAISI models without modification.

Built with .NET 10 and ASP.NET Core Minimal API.

## How It Works

The API sits between OpenAI-compatible clients and the DAISI gRPC network:

```
OpenAI Client  -->  Daisi.OpenAI (HTTP/REST)  -->  DAISI Orc (gRPC)  -->  DAISI Host
```

1. The client sends a standard OpenAI API request with a DAISI Secret Key as the Bearer token.
2. The API exchanges the secret key for a DAISI client key (cached per key).
3. An ephemeral inference session is created, the request is translated to DAISI format, and the response is streamed back in OpenAI format.
4. The session is closed after the response completes.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/chat/completions` | Chat completions (streaming and non-streaming) |
| `POST` | `/v1/completions` | Legacy text completions |
| `GET` | `/v1/models` | List available models |
| `GET` | `/v1/models/{modelId}` | Get a specific model |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Access to a DAISI network Orc server
- A DAISI Secret Key

### Configuration

Edit `appsettings.json` to point to your Orc server:

```json
{
  "Daisi": {
    "OrcDomain": "orc.daisinet.com",
    "OrcPort": 443,
    "OrcUseSSL": true
  }
}
```

### Build and Run

```bash
dotnet build
dotnet run --project Daisi.OpenAI
```

The server starts on the URL configured in `Properties/launchSettings.json`. To override:

```bash
dotnet run --project Daisi.OpenAI --urls "http://localhost:5000"
```

## Usage

### curl

**List models:**

```bash
curl -H "Authorization: Bearer <your-secret-key>" \
  http://localhost:5000/v1/models
```

**Chat completion:**

```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer <your-secret-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Gemma 3 4B Q8 XL",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ]
  }'
```

**Streaming:**

```bash
curl -N -X POST http://localhost:5000/v1/chat/completions \
  -H "Authorization: Bearer <your-secret-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Gemma 3 4B Q8 XL",
    "messages": [{"role": "user", "content": "Hello!"}],
    "stream": true
  }'
```

### Python OpenAI SDK

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:5000/v1",
    api_key="<your-secret-key>",
)

# List models
for model in client.models.list():
    print(model.id)

# Chat completion
response = client.chat.completions.create(
    model="Gemma 3 4B Q8 XL",
    messages=[{"role": "user", "content": "Hello!"}],
)
print(response.choices[0].message.content)

# Streaming
stream = client.chat.completions.create(
    model="Gemma 3 4B Q8 XL",
    messages=[{"role": "user", "content": "Hello!"}],
    stream=True,
)
for chunk in stream:
    if chunk.choices[0].delta.content:
        print(chunk.choices[0].delta.content, end="")
```

## Request Parameters

The following OpenAI parameters are supported and mapped to DAISI equivalents:

| Parameter | Supported | Notes |
|-----------|-----------|-------|
| `model` | Yes | Maps to DAISI model name |
| `messages` | Yes | `system` role becomes `InitializationPrompt`; `user`/`assistant` are formatted as conversation history |
| `stream` | Yes | SSE format with `data:` lines ending with `data: [DONE]` |
| `temperature` | Yes | |
| `top_p` | Yes | |
| `max_tokens` | Yes | |
| `seed` | Yes | |
| `frequency_penalty` | Yes | |
| `presence_penalty` | Yes | |
| `tools` | Accepted | Presence sets DAISI ThinkLevel to `BasicWithTools` |
| `n` | Ignored | DAISI supports single completions only |
| `stop` | Ignored | DAISI uses built-in anti-prompt sequences |
| `response_format` | Ignored | |
| `tool_choice` | Ignored | |

## Authentication

All requests require a `Authorization: Bearer <secret-key>` header. The secret key is a DAISI Secret Key, which is exchanged server-side for a short-lived DAISI Client Key via the Orc's `CreateClientKey` gRPC call. Client keys are cached in memory and automatically cleaned up when they expire.

## Error Handling

All errors are returned in OpenAI error format:

```json
{
  "error": {
    "message": "Description of what went wrong",
    "type": "invalid_request_error",
    "param": null,
    "code": "error_code"
  }
}
```

gRPC status codes from the DAISI network are mapped to HTTP status codes:

| gRPC Status | HTTP Status | Error Type |
|-------------|-------------|------------|
| Unauthenticated | 401 | `authentication_error` |
| PermissionDenied | 403 | `permission_error` |
| NotFound | 404 | `invalid_request_error` |
| InvalidArgument | 400 | `invalid_request_error` |
| ResourceExhausted | 429 | `rate_limit_error` |
| Unavailable | 503 | `server_error` |
| DeadlineExceeded | 504 | `server_error` |

## Project Structure

```
Daisi.OpenAI/
  Program.cs                        # App setup, DI, middleware, route mapping
  appsettings.json                   # Orc connection config

  Authentication/
    BearerTokenAuthHandler.cs        # Extracts Bearer token, exchanges for client key
    DaisiCredential.cs               # SecretKey + ClientKey + expiration
    DaisiCredentialManager.cs        # Thread-safe credential cache
    StaticClientKeyProvider.cs       # IClientKeyProvider for per-request keys

  Sessions/
    InferenceSessionFactory.cs       # Creates InferenceClient per credential
    SessionCleanupService.cs         # Background service to prune expired credentials

  Endpoints/
    ChatCompletionsEndpoint.cs       # POST /v1/chat/completions
    CompletionsEndpoint.cs           # POST /v1/completions
    ModelsEndpoint.cs                # GET /v1/models, GET /v1/models/{model}

  Mapping/
    ChatRequestMapper.cs             # OpenAI request -> DAISI inference request
    ChatResponseMapper.cs            # DAISI response -> OpenAI response / SSE chunks
    CompletionMapper.cs              # Legacy completions mapping
    ContentExtractor.cs              # Strips <think>/<response> tags from output
    ModelMapper.cs                   # DAISI AIModel -> OpenAI model object

  Models/                            # OpenAI request/response DTOs
  Middleware/
    OpenAIErrorMiddleware.cs         # Catches exceptions, returns OpenAI error JSON
  Extensions/
    EndpointRouteBuilderExtensions.cs
    ServiceCollectionExtensions.cs
```

## License

Private. See the LICENSE file for details.
