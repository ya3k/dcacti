/**
 * The development-only application-session bootstrap (TASK-181).
 *
 * <code>
 * normal browser tab (no Discord Activity iframe)
 *         ↓
 * DevelopmentAuthentication  (this module: the development switch)
 *         ↓
 * ApiService.authenticateDiscord  (POST /api/auth/discord, ADR-015 D6)
 *         ↓
 * ApplicationSession.establish    (the existing session holder)
 *         ↓
 * GameRuntime.initialize          (the existing SignalR connect)
 * </code>
 *
 * **It is a different way to obtain the session, never a way to avoid it.** The
 * request goes to the same one unauthenticated endpoint the Discord Activity path
 * uses (`API_CONTRACTS.md` §2.1, §2.8 "Coverage") and the session it receives is
 * the same application session — stored by the same `ApplicationSession`, sent as
 * the same `Authorization: Bearer` header, and carried by the same SignalR
 * access-token mechanism. Nothing here is a client-side session: the client never
 * mints, decodes, or asserts an identity (`ADR-015` D3, `API_CONTRACTS.md` §2.8
 * "Identity").
 *
 * **Production cannot activate it.** `import.meta.env.DEV` is a build-time
 * constant that is `false` in every production bundle, so the switch below is
 * unreachable there regardless of configuration — and the server independently
 * requires its own Development environment plus its own opt-in switch before it
 * will answer the request at all (`DevelopmentAuthenticationOptions`). Either side
 * refusing is enough; both must agree.
 */

/**
 * The development authorization code — the sentinel the backend's development
 * identity source answers (`DevelopmentAuthenticationOptions.AuthorizationCode`).
 *
 * It stands in for a Discord authorization code only in Development. It is a
 * fixed, non-secret trigger and never selects an identity: the Player the
 * resulting session belongs to is fixed server-side.
 */
export const DEVELOPMENT_AUTHORIZATION_CODE = 'development';

/**
 * Whether this build and configuration may use the development authentication
 * path.
 *
 * Both halves are required:
 *
 * - `import.meta.env.DEV` — a development build, never a production bundle.
 * - `import.meta.env.VITE_DEV_AUTH === 'true'` — the explicit opt-in, set in the
 *   developer's untracked `src/frontend/client/.env.local` (see
 *   `.env.example`). It is deliberately not a default: a development build that
 *   did not opt in keeps the pre-existing behaviour (no iframe → no session →
 *   `error`), which is also what the test suite runs with.
 */
export function isDevelopmentAuthenticationEnabled(): boolean {
  return import.meta.env.DEV === true && import.meta.env.VITE_DEV_AUTH === 'true';
}
