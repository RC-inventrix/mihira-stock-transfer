// Typed client for the stock-transfer API. Every call goes through /api, which the Vite dev server
// proxies to the ASP.NET Core API with the prefix stripped.

export type BranchType = 'Warehouse' | 'Branch'

export type TransferStatus = 'Requested' | 'Dispatched' | 'Received' | 'Cancelled'

export const transferStatuses: readonly TransferStatus[] = ['Requested', 'Dispatched', 'Received', 'Cancelled']

export interface Branch {
  id: number
  name: string
  type: BranchType
}

export interface StockItem {
  productId: number
  sku: string
  productName: string
  quantity: number
}

export interface BranchStock {
  branchId: number
  branchName: string
  items: StockItem[]
}

export interface TransferLine {
  productId: number
  sku: string
  productName: string
  quantity: number
}

export interface Transfer {
  id: number
  fromBranchId: number
  fromBranchName: string
  toBranchId: number
  toBranchName: string
  status: TransferStatus
  createdAt: string
  dispatchedAt: string | null
  receivedAt: string | null
  cancelledAt: string | null
  lines: TransferLine[]
}

export interface CreateTransferRequest {
  fromBranchId: number
  toBranchId: number
  lines: { productId: number; quantity: number }[]
}

/**
 * A failed call. `message` is a sentence to show the user. `messages` lists the individual problems when the API
 * reported more than one, and `title` is a short heading for that list.
 */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly messages: string[]

  constructor(status: number, title: string, message: string, messages: string[] = []) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.messages = messages
  }
}

/** Turns anything thrown by a call into an ApiError, so the UI has one shape to show. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error
  }
  const message = error instanceof Error ? error.message : String(error)
  return new ApiError(0, 'Something went wrong', message)
}

// The two error bodies the API can send: our ProblemDetails (detail + messages) and ASP.NET's
// model-binding 400 (title + errors keyed by field).
interface ProblemBody {
  title?: unknown
  detail?: unknown
  messages?: unknown
  errors?: unknown
}

const strings = (value: unknown): string[] =>
  Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : []

async function errorFrom(response: Response): Promise<ApiError> {
  const fallback = `The request failed: ${[response.status, response.statusText].filter(Boolean).join(' ')}.`

  let body: ProblemBody | null = null
  try {
    body = (await response.json()) as ProblemBody
  } catch {
    // No JSON body, e.g. the Vite proxy answering 500 because the API is not running.
  }

  if (body === null || typeof body !== 'object') {
    const hint = response.status >= 500 ? ' Check that the API is running.' : ''
    return new ApiError(response.status, 'Request failed', fallback + hint)
  }

  const title = typeof body.title === 'string' ? body.title : 'Request failed'
  let messages = strings(body.messages)
  if (messages.length === 0 && body.errors && typeof body.errors === 'object') {
    messages = Object.values(body.errors).flatMap(strings)
  }
  const detail = typeof body.detail === 'string' ? body.detail : messages.join(' ') || title || fallback

  return new ApiError(response.status, title, detail, messages)
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(`/api${path}`, { ...init, headers })
  } catch {
    throw new ApiError(0, 'Network error', 'Could not reach the server. Check your connection and that the API is running.')
  }

  if (!response.ok) {
    throw await errorFrom(response)
  }
  return (await response.json()) as T
}

export const getBranches = () => request<Branch[]>('/branches')

export const getStock = (branchId: number) => request<BranchStock>(`/branches/${branchId}/stock`)

export function getTransfers(filter: { branchId?: number; status?: TransferStatus } = {}) {
  const query = new URLSearchParams()
  if (filter.branchId !== undefined) {
    query.set('branchId', String(filter.branchId))
  }
  if (filter.status !== undefined) {
    query.set('status', filter.status)
  }
  const suffix = query.size > 0 ? `?${query}` : ''
  return request<Transfer[]>(`/transfers${suffix}`)
}

/** A retry with the same idempotency key returns the original transfer instead of creating a second one. */
export const createTransfer = (body: CreateTransferRequest, idempotencyKey: string) =>
  request<Transfer>('/transfers', {
    method: 'POST',
    body: JSON.stringify(body),
    headers: { 'Idempotency-Key': idempotencyKey },
  })

export type TransferAction = 'dispatch' | 'receive' | 'cancel'

/** Safe to repeat: the API answers a repeat of a step that already happened with the transfer and moves nothing. */
export const runTransferAction = (id: number, action: TransferAction) =>
  request<Transfer>(`/transfers/${id}/${action}`, { method: 'POST' })
