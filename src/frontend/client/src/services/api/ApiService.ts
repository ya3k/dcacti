import { ApplicationSession } from './ApplicationSession';
import type { DiscordAuthResponse } from './ApplicationSession';
import type { CardResponse, PetResponse, RelicResponse } from './CollectionModels';
import type { BattleResultResponse, BattleStartRequest, BattleStartResponse } from './BattleModels';

export interface HealthStatus {
  status: string;
}

export type { CardResponse, PetResponse, RelicResponse };

/**
 * Re-exported so the battle wire shapes have one definition — the models file
 * is what states them, and this is the service that transports them.
 */
export type {
  BattleOutcome,
  BattleResultResponse,
  BattleStartBoard,
  BattleStartBossState,
  BattleStartCell,
  BattleStartInitialState,
  BattleStartPetState,
  BattleStartRequest,
  BattleStartResponse,
  BattleStartRngState,
  BattleStartSpecialGem,
  Element,
  RewardSummaryResponse,
} from './BattleModels';

/**
 * The `POST /api/auth/discord` response (API_CONTRACTS.md §2.5).
 *
 * Re-exported from {@link ApplicationSession} so the wire shape has one
 * definition — the session holder is what consumes it.
 */
export type { DiscordAuthResponse };

export class ApiService {
  private static instance: ApiService | null = null;
  private baseUrl: string;

  private constructor(baseUrl: string = '') {
    this.baseUrl = baseUrl;
  }

  public static getInstance(): ApiService {
    if (!ApiService.instance) {
      ApiService.instance = new ApiService();
    }
    return ApiService.instance;
  }

  public async checkHealth(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/health`);
      if (!response.ok) return false;
      const text = await response.text();
      return text.includes('Healthy');
    } catch {
      return false;
    }
  }

  /**
   * Establishes the application session from a Discord authorization code
   * (API_CONTRACTS.md §2).
   *
   * This is the one endpoint that does not require an authenticated session
   * (§2.1, §2.8 "Coverage"), so it deliberately sends no `Authorization` header
   * even when a session already exists. On success the returned session is
   * recorded, which is what makes every later request authenticated.
   */
  public async authenticateDiscord(code: string): Promise<DiscordAuthResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/discord`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ code }),
    });

    if (!response.ok) {
      throw new Error(`Authentication failed with status ${response.status}`);
    }

    const body = (await response.json()) as DiscordAuthResponse;

    ApplicationSession.getInstance().establish(body);

    return body;
  }

  /**
   * A `GET` to an application endpoint, carrying the application session as
   * `Authorization: Bearer <sessionToken>` (API_CONTRACTS.md §2.8 "Transport";
   * ADR-015 D4).
   *
   * Every REST endpoint except `POST /api/auth/discord` requires that session,
   * and an unauthenticated response is `401 { "error": "UNAUTHENTICATED" }`
   * (§2.8 "Failure behavior").
   */
  public async get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'GET',
      headers: {
        ...ApplicationSession.getInstance().getAuthorizationHeader(),
      },
    });

    if (!response.ok) {
      throw new Error(`Request to ${path} failed with status ${response.status}`);
    }

    return (await response.json()) as T;
  }

  /**
   * Lists the authenticated Player's owned Pets
   * (`API_CONTRACTS.md` §5.1) — `GET /api/pets`.
   *
   * The response is a bare JSON array with no envelope; each element is
   * exactly the six §5.1 members. An empty collection is `200 []` (§5.5), not
   * `204`, not `404`, and not `null`: a Player owning nothing has an empty
   * collection, and this method returns that array as it arrived rather than
   * collapsing it into an absence.
   *
   * §5.5 defines no ordering — "clients must not rely on any order" — so the
   * array is returned in the order the server sent and is never sorted here.
   */
  public async getPets(): Promise<PetResponse[]> {
    return await this.get<PetResponse[]>('/api/pets');
  }

  /**
   * Reads one owned Pet's detail (`API_CONTRACTS.md` §5.2) —
   * `GET /api/pets/{petId}`.
   *
   * `petId` is the owned instance identity (`Pet.PetInstanceId`, §5.1), not a
   * definition id. It is a route **segment**, so the single path contains the
   * id encoded with `encodeURIComponent` — the same technique the rest of the
   * client's URL handling uses — and nothing else: §5 permits no query
   * parameter on this endpoint, and none is added.
   *
   * The 200 body is the same object as one `/api/pets` array element, with no
   * wrapper (§5.2). A `petId` that does not exist and one owned by another
   * Player produce the identical `404 PET_NOT_FOUND` — §5.2 makes those one
   * answer deliberately, so the client neither distinguishes them nor treats
   * the foreign case as an authorization failure of its own.
   */
  public async getPet(petId: string): Promise<PetResponse> {
    return await this.get<PetResponse>(`/api/pets/${encodeURIComponent(petId)}`);
  }

  /**
   * Lists the Card definitions the authenticated Player has unlocked
   * (`API_CONTRACTS.md` §5.3) — `GET /api/cards`.
   *
   * Membership of the array **is** the unlocked state (ADR-012), so there is
   * no `unlocked` member to read and none is added. An empty collection is
   * `200 []` (§5.5), and no ordering is defined (§5.5).
   */
  public async getCards(): Promise<CardResponse[]> {
    return await this.get<CardResponse[]>('/api/cards');
  }

  /**
   * Lists the Relic instances the authenticated Player owns
   * (`API_CONTRACTS.md` §5.4) — `GET /api/relics`.
   *
   * Each element carries the owned **instance** identity and its definition
   * name, and no equip state: §5.6 makes which Relics are equipped
   * battle-scoped and unpersisted, so it is not read from here. An empty
   * collection is `200 []` (§5.5), and no ordering is defined (§5.5).
   */
  public async getRelics(): Promise<RelicResponse[]> {
    return await this.get<RelicResponse[]>('/api/relics');
  }

  /**
   * Starts a battle session for the submitted selection
   * (`API_CONTRACTS.md` §3) — `POST /api/battle/start`.
   *
   * The body is exactly the four documented request members (`petId`,
   * `bossId`, `cardLoadout`, `relicLoadout`) and is serialized as JSON, which
   * is the representation this endpoint's contract shows. The client submits a
   * **selection**: it sends no `playerId` (the requesting Player is resolved
   * server-side from the session's `player_id` claim — §2.8 "Identity,"
   * ADR-015 D3), and no `battleId`, `turn`, `sequence`, or other server-owned
   * battle state, because the server authors the whole resulting
   * `BattleState` (`GAME_RULES.md` §18, ADR-001).
   *
   * **No loadout validation happens here.** The 3-card count, the 3–5 relic
   * count, ownership, category, copy-limit, distinctness, and `bossId`
   * resolution are all validated by the server per §3; this method transports
   * the arrays as given — order preserved, nothing de-duplicated, nothing
   * sorted — so the Relic array's order still reaches the server as the equip
   * slot order (`RELIC_RULES.md` §2.3).
   *
   * A rejection is the documented `400` (`INVALID_LOADOUT`, `PET_NOT_OWNED`,
   * or `BOSS_NOT_FOUND`) or the §2.8 `401 UNAUTHENTICATED`, propagated by the
   * shared transport. No battle exists after any of them, so nothing is
   * recorded, and this method returns the typed response without holding,
   * caching, or deriving any battle state (`AGENTS.md` §10).
   */
  public async startBattle(request: BattleStartRequest): Promise<BattleStartResponse> {
    return await this.post<BattleStartResponse>('/api/battle/start', request);
  }

  /**
   * Reads a completed battle's result (`API_CONTRACTS.md` §4) —
   * `GET /api/battle/{battleId}/result`.
   *
   * `battleId` is the created battle's identity (§3) and is a route
   * **segment**, so the single path contains the id encoded with
   * `encodeURIComponent` — the same technique `getPet` uses — and nothing
   * else: §4 permits no query parameter on this endpoint, and none is added.
   *
   * **The caller's identity is never sent.** §4 note 7 makes ownership
   * server-derived, so there is no `playerId` member, query parameter, or
   * header: a battle that does not exist and one owned by another Player are
   * the identical `404 BATTLE_NOT_FOUND`, deliberately, and the client neither
   * distinguishes them nor treats the foreign case as an authorization failure
   * of its own. An unauthenticated caller receives the §2.8 `401
   * UNAUTHENTICATED` — not `404` — through the shared transport.
   *
   * The returned `rewards` is the server's `RewardSummary` as stored. This
   * method applies no grant and updates no Player or Pet progression; the
   * reward path is server-owned (`AGENTS.md` §10, ADR-001).
   */
  public async getBattleResult(battleId: string): Promise<BattleResultResponse> {
    return await this.get<BattleResultResponse>(
      `/api/battle/${encodeURIComponent(battleId)}/result`,
    );
  }

  /**
   * A `POST` to an application endpoint, carrying the same session header.
   */
  public async post<T>(path: string, body: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...ApplicationSession.getInstance().getAuthorizationHeader(),
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      throw new Error(`Request to ${path} failed with status ${response.status}`);
    }

    return (await response.json()) as T;
  }
}
