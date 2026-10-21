import { useEffect, useState } from 'react'
import {
  type ApiError,
  type Branch,
  type ReceiveTransferRequest,
  type Transfer,
  type TransferAction,
  type TransferStatus,
  getTransfers,
  runTransferAction,
  toApiError,
  transferStatuses,
} from '../api'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  branches: Branch[]
  /** Changes whenever a transfer may have changed, so the list reloads. */
  version: number
  onChanged: () => void
}

// Only the actions the API allows from each status. Received and Cancelled are final.
const allowedActions: Record<TransferStatus, TransferAction[]> = {
  Requested: ['dispatch', 'cancel'],
  Dispatched: ['receive'],
  Received: [],
  Cancelled: [],
}

const actionLabels: Record<TransferAction, string> = { dispatch: 'Dispatch', receive: 'Receive', cancel: 'Cancel' }

const formatDate = (value: string) => new Date(value).toLocaleString()

/** A received quantity typed into the receive form: a whole number from 0 to what was sent. */
const parseReceived = (value: string, sent: number) => {
  const n = Number(value)
  return value.trim() !== '' && Number.isInteger(n) && n >= 0 && n <= sent ? n : null
}

export function TransferList({ branches, version, onChanged }: Props) {
  const [branchId, setBranchId] = useState<number | undefined>(undefined)
  const [status, setStatus] = useState<TransferStatus | undefined>(undefined)
  const [transfers, setTransfers] = useState<Transfer[] | null>(null)
  const [loadError, setLoadError] = useState<ApiError | null>(null)
  const [actionError, setActionError] = useState<{ context: string; error: ApiError } | null>(null)
  // Transfers with an action in flight: their buttons stay disabled until it finishes.
  const [busy, setBusy] = useState<ReadonlySet<number>>(new Set())
  // The transfer whose receive form is open, with what the user typed per product (prefilled with the quantity sent).
  const [receiving, setReceiving] = useState<{ id: number; quantities: Record<number, string> } | null>(null)

  useEffect(() => {
    let ignore = false
    getTransfers({ branchId, status }).then(
      (result) => {
        if (!ignore) {
          setTransfers(result)
          setLoadError(null)
        }
      },
      (e: unknown) => {
        if (!ignore) setLoadError(toApiError(e))
      },
    )
    return () => {
      ignore = true
    }
  }, [branchId, status, version])

  function openReceive(transfer: Transfer) {
    setReceiving({
      id: transfer.id,
      quantities: Object.fromEntries(transfer.lines.map((l) => [l.productId, String(l.quantity)])),
    })
  }

  function confirmReceive(transfer: Transfer) {
    if (receiving?.id !== transfer.id) return
    const lines: ReceiveTransferRequest['lines'] = []
    for (const l of transfer.lines) {
      const receivedQuantity = parseReceived(receiving.quantities[l.productId] ?? '', l.quantity)
      if (receivedQuantity === null) return
      lines.push({ productId: l.productId, receivedQuantity })
    }
    run(transfer, 'receive', { lines })
  }

  async function run(transfer: Transfer, action: TransferAction, body?: ReceiveTransferRequest) {
    setBusy((current) => new Set(current).add(transfer.id))
    setActionError(null)
    try {
      await runTransferAction(transfer.id, action, body)
      if (action === 'receive') setReceiving(null)
    } catch (e) {
      setActionError({ context: `Could not ${action} transfer #${transfer.id}`, error: toApiError(e) })
    } finally {
      setBusy((current) => {
        const next = new Set(current)
        next.delete(transfer.id)
        return next
      })
      // Reload even after a failure: someone else may have moved the transfer on.
      onChanged()
    }
  }

  return (
    <>
      <div className="row">
        <label>
          Branch{' '}
          <select
            value={branchId ?? ''}
            onChange={(e) => setBranchId(e.target.value ? Number(e.target.value) : undefined)}
          >
            <option value="">All branches</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Status{' '}
          <select
            value={status ?? ''}
            onChange={(e) => setStatus(e.target.value ? (e.target.value as TransferStatus) : undefined)}
          >
            <option value="">All statuses</option>
            {transferStatuses.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>

      {actionError && (
        <ErrorMessage context={actionError.context} error={actionError.error} onDismiss={() => setActionError(null)} />
      )}
      {loadError && <ErrorMessage context="Could not load the transfers" error={loadError} />}
      {!transfers && !loadError && <p>Loading transfers…</p>}
      {transfers?.length === 0 && <p>No transfers match these filters.</p>}

      {transfers && transfers.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>From → To</th>
              <th>Lines</th>
              <th>Status</th>
              <th>Dates</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {transfers.map((t) => {
              const form = receiving?.id === t.id ? receiving : null
              const formValid =
                form !== null &&
                t.lines.every((l) => parseReceived(form.quantities[l.productId] ?? '', l.quantity) !== null)
              return (
                <tr key={t.id}>
                  <td>{t.id}</td>
                  <td>
                    {t.fromBranchName} → {t.toBranchName}
                  </td>
                  <td>
                    {t.lines.map((l) => (
                      <div key={l.productId}>
                        {l.productName} × {l.quantity}
                        {form && (
                          <label>
                            {' '}
                            received{' '}
                            <input
                              type="number"
                              min={0}
                              max={l.quantity}
                              step={1}
                              value={form.quantities[l.productId] ?? ''}
                              onChange={(e) =>
                                setReceiving({
                                  ...form,
                                  quantities: { ...form.quantities, [l.productId]: e.target.value },
                                })
                              }
                            />
                          </label>
                        )}
                        {l.receivedQuantity !== null && l.receivedQuantity < l.quantity && (
                          <span className="warning">
                            {' '}
                            · received {l.receivedQuantity}, {l.quantity - l.receivedQuantity} short
                          </span>
                        )}
                      </div>
                    ))}
                    {form && !formValid && (
                      <div className="warning">
                        Each received quantity must be a whole number from 0 to the quantity sent.
                      </div>
                    )}
                  </td>
                  <td className="status">{t.status}</td>
                  <td className="muted">
                    <div>Created {formatDate(t.createdAt)}</div>
                    {t.dispatchedAt && <div>Dispatched {formatDate(t.dispatchedAt)}</div>}
                    {t.receivedAt && <div>Received {formatDate(t.receivedAt)}</div>}
                    {t.cancelledAt && <div>Cancelled {formatDate(t.cancelledAt)}</div>}
                  </td>
                  <td>
                    <div className="row">
                      {form ? (
                        <>
                          <button
                            type="button"
                            disabled={busy.has(t.id) || !formValid}
                            onClick={() => confirmReceive(t)}
                          >
                            Confirm receive
                          </button>
                          <button type="button" disabled={busy.has(t.id)} onClick={() => setReceiving(null)}>
                            Back
                          </button>
                        </>
                      ) : (
                        allowedActions[t.status].map((action) => (
                          <button
                            key={action}
                            type="button"
                            disabled={busy.has(t.id)}
                            // Receive first asks what arrived; the others go straight to the API.
                            onClick={() => (action === 'receive' ? openReceive(t) : run(t, action))}
                          >
                            {actionLabels[action]}
                          </button>
                        ))
                      )}
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </>
  )
}
