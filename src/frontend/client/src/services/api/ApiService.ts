import { ApplicationSession } from './ApplicationSession';
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
} from './ApplicationSession';
import type { CardResponse, PetResponse, RelicResponse } from './CollectionModels';
import type {
  BattleHistoryItemResponse,
  BattleResultResponse,
  BattleStartRequest,
  BattleStartResponse,
} from './BattleModels';

export interface HealthStatus {
  status: string;
}

export type { CardResponse, PetResponse, RelicResponse };

/**
 * Re-exported so the battle wire shapes have one definition — the models file
 * is what states them, and this is the service that transports them.
 */
export type {
  BattleHistoryItemResponse,
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

export type { AuthResponse, LoginRequest, RegisterRequest };

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
   * Registers a new account and establishes the application session (ADR-020).
   */
  public async register(request: RegisterRequest): Promise<AuthResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => null);
      const code = err?.error || `HTTP ${response.status}`;
      throw new Error(code);
    }

    const body = (await response.json()) as AuthResponse;
    ApplicationSession.getInstance().establish(body);
    return body;
  }

  /**
   * Logs into an existing account and establishes the application session (ADR-020).
   */
  public async login(request: LoginRequest): Promise<AuthResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const err = await response.json().catch(() => null);
      const code = err?.error || `HTTP ${response.status}`;
      throw new Error(code);
    }

    const body = (await response.json()) as AuthResponse;
    ApplicationSession.getInstance().establish(body);
    return body;
  }

  /**
   * A `GET` to an application endpoint, carrying the application session as
   * `Authorization: Bearer <sessionToken>` (API_CONTRACTS.md §2.8 "Transport";
   * ADR-015 D4).
   *
   * Every REST endpoint except `/api/auth/*` requires that session,
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
   * Reads the authenticated Player's completed-battle history
   * (`API_CONTRACTS.md` §4.5) — `GET /api/battle/history`.
   *
   * ```text
   * GET /api/battle/history  →  200 [ BattleHistoryItemResponse, … ]
   *                             401 { "error": "UNAUTHENTICATED" }   (§2.3, §6)
   * ```
   *
   * **The path carries nothing but the route.** §4.5 notes 5–6 state that the
   * endpoint accepts no `page`, `limit`, `offset`, `cursor`, `bossId`,
   * `outcome`, date range, sort, or search parameter — there are no query
   * parameters at all — so this method builds the single documented path and
   * nothing else, and no request member, query parameter, or header selects a
   * `playerId` (note 8: the scope is the session-derived identity, and reading
   * another Player's history is not expressible in this contract).
   *
   * **The array is returned as delivered.** §4.5 note 1 makes the bare JSON
   * array the response body (never a `{ "battles": … }` wrapper, and never a
   * `total`/`nextCursor`), and note 4 fixes its order — `CompletedAt`
   * descending, tie-broken by `BattleResultId` descending — as a documented
   * contract clients MAY rely on. So nothing here sorts, reverses, filters,
   * de-duplicates, infers an order from `battleId`, or interprets
   * `completedAt`: the server is authoritative for the order and for every
   * value, and the array is handed on unchanged.
   *
   * **The empty history is the empty array.** §4.5 note 9 makes a Player with
   * no completed battles `200` with `[]` — not `404`, not `204`, and not an
   * error — so this method returns the empty array as it arrived rather than
   * collapsing it into an absence or treating it as a failure. A still-active
   * battle never appears (note 10) and a battle whose durable result write
   * failed is simply absent (note 11); none of those is a client-side state the
   * caller has to distinguish, because the response carries no placeholder for
   * any of them.
   *
   * A failure — the §2.3/§6 `401 UNAUTHENTICATED` for a missing, invalid, or
   * expired session, a transport error, or any other unexpected status —
   * propagates from the shared authenticated `get<T>()` transport as a
   * rejection. Nothing is fabricated to stand in for the missing history.
   */
  public async getBattleHistory(): Promise<BattleHistoryItemResponse[]> {
    return await this.get<BattleHistoryItemResponse[]>('/api/battle/history');
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
