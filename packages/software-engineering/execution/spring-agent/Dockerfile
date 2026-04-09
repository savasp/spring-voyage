FROM mcr.microsoft.com/dotnet/sdk:10.0

# Install git
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI (placeholder — actual install depends on distribution)
# RUN npm install -g @anthropic-ai/claude-code

WORKDIR /workspace
ENTRYPOINT ["claude-code"]
