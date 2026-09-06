import type { ProblemDetails } from './types'
import { clearSession, getAccessToken } from '@/features/auth/authStore'

export const API_BASE_URL = '/api/v1'

type QueryValue = string | number | boolean | null | undefined
export type QueryParams = Record<string, QueryValue | QueryValue[]>

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** Serialised as JSON unless it is already a `FormData` or `Blob`. */
  body?: unknown
  query?: QueryParams
  headers?: Record<string, string>
  signal?: AbortSignal
}

/**
 * Error raised for every non-2xx response and for network failures (status 0).
 * Carries the RFC 9457 problem details fields when the API supplied them.
 */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail?: string
  readonly type?: string
  readonly instance?: string
  readonly traceId?: string
  /** Field-level validation failures keyed by (camelCase) field name. */
  readonly errors?: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails = {}) {
    const title = problem.title ?? defaultTitle(status)
    super(problem.detail ? `${title}: ${problem.detail}` : title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = problem.detail
    this.type = problem.type
    this.instance = problem.instance
    this.traceId = problem.traceId
    this.errors = problem.errors ? normaliseErrorKeys(problem.errors) : undefined
  }

  get isValidation(): boolean {
    return this.errors !== undefined && (this.status === 400 || this.status === 422)
  }

  get isUnauthorized(): boolean {
    return this.status === 401
  }

  get isForbidden(): boolean {
    return this.status === 403
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  /** First message for a field, or undefined. Handy for `setError` in react-hook-form. */
  fieldError(field: string): string | undefined {
    return this.errors?.[field]?.[0]
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

export function isAbortError(error: unknown): boolean {
  return (
    (error instanceof DOMException && error.name === 'AbortError') ||
    (error instanceof Error && error.name === 'AbortError')
  )
}

/** Human-readable message for any thrown value (ApiError, Error, or unknown). */
export function errorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (isApiError(error)) return error.detail ?? error.title
  if (error instanceof Error && error.message) return error.message
  return fallback
}

function defaultTitle(status: number): string {
  if (status === 0) return 'Network error'
  if (status === 400) return 'Bad request'
  if (status === 401) return 'Not signed in'
  if (status === 403) return 'Forbidden'
  if (status === 404) return 'Not found'
  if (status === 409) return 'Conflict'
  if (status === 422) return 'Validation failed'
  if (status === 429) return 'Too many requests'
  if (status >= 500) return 'Server error'
  return `Request failed (${status})`
}

/** ASP.NET emits PascalCase keys for validation errors; the UI uses camelCase field names. */
function normaliseErrorKeys(errors: Record<string, string[]>): Record<string, string[]> {
  const out: Record<string, string[]> = {}
  for (const [key, messages] of Object.entries(errors)) {
    const camel = key
      .split('.')
      .map((part) => (part ? part[0].toLowerCase() + part.slice(1) : part))
      .join('.')
    out[camel] = messages
  }
  return out
}

export function buildQuery(query: QueryParams | undefined): string {
  if (!query) return ''
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    const values = Array.isArray(value) ? value : [value]
    for (const v of values) {
      if (v === undefined || v === null || v === '') continue
      params.append(key, String(v))
    }
  }
  const s = params.toString()
  return s ? `?${s}` : ''
}

/** Absolute request URL: relative API paths resolve against the page origin (fetch in Node needs this). */
export function resolveUrl(path: string, query?: QueryParams): string {
  if (/^https?:\/\//i.test(path)) return `${path}${buildQuery(query)}`
  const relative = path.startsWith(API_BASE_URL)
    ? path
    : `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`
  const origin = typeof location !== 'undefined' ? location.origin : 'http://localhost'
  return `${origin}${relative}${buildQuery(query)}`
}

async function parseProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('json')) {
    try {
      const body: unknown = await response.json()
      if (body && typeof body === 'object') return body as ProblemDetails
    } catch {
      // fall through to the generic problem
    }
  }
  return { status: response.status, title: response.statusText || undefined }
}

function buildInit(options: RequestOptions, accept: string): RequestInit {
  const { method = 'GET', body, headers = {}, signal } = options
  const finalHeaders: Record<string, string> = { Accept: accept, ...headers }
  const token = getAccessToken()
  if (token) finalHeaders.Authorization = `Bearer ${token}`

  const init: RequestInit = { method, signal, headers: finalHeaders }
  if (body !== undefined) {
    if (body instanceof FormData || body instanceof Blob) {
      init.body = body
    } else {
      finalHeaders['Content-Type'] = 'application/json'
      init.body = JSON.stringify(body)
    }
  }
  return init
}

async function send(url: string, init: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch (error) {
    if (isAbortError(error)) throw error
    throw new ApiError(0, {
      title: 'Network error',
      detail: error instanceof Error ? error.message : 'The request could not be sent.',
    })
  }

  if (!response.ok) {
    const problem = await parseProblem(response)
    // A 401 while we hold a token means the session expired or was revoked: drop it so
    // RequireAuth redirects to the sign-in page. A 401 with no token is a failed sign-in.
    if (response.status === 401 && getAccessToken()) clearSession()
    throw new ApiError(response.status, problem)
  }
  return response
}

/**
 * Typed fetch wrapper. Resolves with the parsed JSON body (or `undefined` for 204),
 * rejects with `ApiError` for non-2xx responses and network failures, and re-throws
 * `AbortError` untouched so callers can ignore cancelled requests.
 */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(resolveUrl(path, options.query), buildInit(options, 'application/json'))
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T
  }
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** Fetch a binary resource (report download) with the bearer token attached. */
export async function download(
  path: string,
  options: Omit<RequestOptions, 'method' | 'body'> = {},
): Promise<Blob> {
  const response = await send(resolveUrl(path, options.query), buildInit(options, '*/*'))
  return response.blob()
}

type Options = Omit<RequestOptions, 'method' | 'body'>

export function get<T>(path: string, options?: Options): Promise<T> {
  return request<T>(path, { ...options, method: 'GET' })
}

export function post<T>(path: string, body?: unknown, options?: Options): Promise<T> {
  return request<T>(path, { ...options, method: 'POST', body })
}

export function put<T>(path: string, body?: unknown, options?: Options): Promise<T> {
  return request<T>(path, { ...options, method: 'PUT', body })
}

export function del<T = void>(path: string, options?: Options): Promise<T> {
  return request<T>(path, { ...options, method: 'DELETE' })
}

export const api = { get, post, put, del, download, request }
