#!/bin/bash
set -e

# Start Ollama in the background
echo "Starting Ollama server in background..."
ollama serve &
OLLAMA_PID=$!

echo "Waiting for Ollama to start..."
for i in {1..60}; do
  if curl -s http://localhost:11434 > /dev/null 2>&1; then
    echo "Ollama is ready"
    break
  fi
  echo "Waiting... ($i/60)"
  sleep 1
done

# Pull the model
echo "Pulling nomic-embed-text model..."
if ollama pull nomic-embed-text; then
  echo "Model pull completed"
else
  echo "Model pull failed, but continuing..."
fi

echo "pulling llama3.2 model..."
if ollama pull llama3.2; then
  echo "Model pull completed"
else
  echo "Model pull failed, but continuing..."
fi

echo "Stopping background Ollama to restart in foreground..."
kill "$OLLAMA_PID" || true
wait "$OLLAMA_PID" 2>/dev/null || true
sleep 1

echo "Starting Ollama server in foreground..."
exec ollama serve
