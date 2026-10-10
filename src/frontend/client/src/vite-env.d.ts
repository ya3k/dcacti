/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
  readonly VITE_SIGNALR_URL?: string;
  /**
   * The development-only authentication opt-in (TASK-181). `'true'` enables the
   * development session bootstrap for a development build opened in a normal
   * browser tab; every other value (including unset) leaves it off. Set it in the
   * untracked `.env.local` — never in a production environment.
   */
  readonly VITE_DEV_AUTH?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
