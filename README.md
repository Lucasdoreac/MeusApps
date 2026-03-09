# LUDOC OS — Dashboard GUI

Interface gráfica soberana do sistema LUDOC OS. App .NET MAUI Windows que conecta ao **context-server** local e exibe telemetria, tarefas de agentes e entrada de voz em tempo real.

## Stack

- **.NET 10 MAUI** — Windows 10+ (x64)
- **CommunityToolkit.Mvvm** — MVVM (ObservableObject, RelayCommand)
- **CommunityToolkit.Maui** — MediaElement, converters
- **Windows.Media.SpeechRecognition** — STT nativo, reconhecimento contínuo
- **Plugin.Maui.Audio** — captura de áudio

## Abas

| Aba | Função |
|-----|--------|
| **CHAT** | Envio de perguntas para Claude/Gemini via `/ask/claude` |
| **STATUS** | Telemetria ao vivo: CPU, RAM, disco, processos, alertas |
| **CONFIG** | URL do servidor, auth token, teste de conexão |
| **VOICE** | Gravação de voz → STT → agente → journal + tasks ao vivo |

## Arquitetura

```
VoicePage
  └─ VoiceViewModel
       ├─ Windows.Media.SpeechRecognizer  ← STT contínuo em tempo real
       ├─ LudocApiService.SendVoiceInputAsync()  → POST /voice/input
       └─ PollLoop (3s)
            ├─ GET /tasks?status=queued,processing
            └─ GET /journal?limit=8&since=2h
```

O context-server (`192.168.0.5:9000` ou `localhost:9000`) roteia a transcrição:
- **Queries rápidas** → llama.cpp (qwen2.5:3b, porta 9001)
- **Tarefas complexas** → Gemini CLI (async, retorna `task_id`)
- **Código/arquivos** → Claude WSL

## Build

```bash
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

## Configuração

Em **CONFIG**, definir o URL do servidor:
- LAN: `http://192.168.0.5:9000`
- Local: `http://localhost:9000`
- Remoto: `https://seu-tunnel.cloudflare.com` (Phase 1.5)

## Requisitos

- Windows 10 1903+ (build 19041+)
- .NET 10 SDK
- context-server rodando (`DNP/services/ludoc-os/`)
- Microfone habilitado nas configurações do Windows
