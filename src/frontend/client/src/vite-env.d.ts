/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_DISCORD_CLIENT_ID?: string;
  readonly VITE_API_URL?: string;
  readonly VITE_SIGNALR_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
