import { type FormEvent, useEffect, useRef, useState } from 'react'
import { type ApiError, type Branch, type BranchStock, createTransfer, getStock, toApiError } from '../api'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  branches: Branch[]
  /** Changes whenever stock may have moved, so the available figures reload. */
  version: number
  onCreated: () => void
}

interface Line {
  key: number
  productId: number | null
  quantity: string
}

let nextLineKey = 1
const emptyLine = (): Line => ({ key: nextLineKey++, productId: null, quantity: '' })

export function NewTransferForm({ branches, version, onCreated }: Props) {
  const [fromId, setFromId] = useState<number>(branches[0].id)
  const [toId, setToId] = useState<number | null>(branches[1]?.id ?? null)
  const [lines, setLines] = useState<Line[]>(() => [emptyLine()])
  const [stock, setStock] = useState<BranchStock | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const [stockError, setStockError] = useState<ApiError | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Kept until the create succeeds, so a retry after a network error cannot create the transfer twice.
  const idempotencyKey = useRef<string | null>(null)

  useEffect(() => {
    let ignore = false
    getStock(fromId).then(
      (result) => {
        if (!ignore) {
          setStock(result)
          setStockError(null)
        }
      },
      (e: unknown) => {
        if (!ignore) setStockError(toApiError(e))
      },
    )
    return () => {
      ignore = true
    }
  }, [fromId, version])

  // Until the new branch's stock arrives, show nothing rather than the previous branch's figures.
  const products = stock?.branchId === fromId ? stock.items : []
  const available = (productId: number | null) => products.find((p) => p.productId === productId)?.quantity

  function changeFrom(id: number) {
    setFromId(id)
    if (id === toId) setToId(null)
  }

  function updateLine(key: number, change: Partial<Line>) {
    setLines((current) => current.map((l) => (l.key === key ? { ...l, ...change } : l)))
  }

  // The to list disables the from branch and changeFrom clears a clash, so the two cannot match here.
  const canSubmit = !submitting && toId !== null && lines.length > 0 && lines.every((l) => l.productId !== null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!canSubmit || toId === null) return

    idempotencyKey.current ??= crypto.randomUUID()
    setSubmitting(true)
    setError(null)
    setNotice(null)
    try {
      const created = await createTransfer(
        {
          fromBranchId: fromId,
          toBranchId: toId,
          // A blank or non-numeric quantity is sent as 0 so the API's "greater than zero" rule reports it.
          lines: lines.map((l) => ({ productId: l.productId ?? 0, quantity: Number.parseInt(l.quantity, 10) || 0 })),
        },
        idempotencyKey.current,
      )
      idempotencyKey.current = null
      setLines([emptyLine()])
      setNotice(`Transfer #${created.id} created: ${created.fromBranchName} → ${created.toBranchName}.`)
      onCreated()
    } catch (e) {
      setError(toApiError(e))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={submit}>
      {error && <ErrorMessage context="Could not create the transfer" error={error} onDismiss={() => setError(null)} />}
      {notice && <p className="notice">{notice}</p>}

      <div className="row">
        <label>
          From{' '}
          <select value={fromId} onChange={(e) => changeFrom(Number(e.target.value))}>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          To{' '}
          <select value={toId ?? ''} onChange={(e) => setToId(e.target.value ? Number(e.target.value) : null)}>
            <option value="">Choose a branch</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id} disabled={b.id === fromId}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      {stockError && <ErrorMessage context="Could not load stock for the from branch" error={stockError} />}

      <table>
        <thead>
          <tr>
            <th>Product</th>
            <th className="number">Available at {branches.find((b) => b.id === fromId)?.name}</th>
            <th>Quantity</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => {
            const have = available(line.productId)
            const quantity = Number.parseInt(line.quantity, 10)
            const over = have !== undefined && quantity > have
            return (
              <tr key={line.key}>
                <td>
                  <select
                    value={line.productId ?? ''}
                    onChange={(e) => updateLine(line.key, { productId: e.target.value ? Number(e.target.value) : null })}
                  >
                    <option value="">Choose a product</option>
                    {products.map((p) => (
                      <option
                        key={p.productId}
                        value={p.productId}
                        // Each product goes on one line only; the API rejects duplicates too.
                        disabled={p.productId !== line.productId && lines.some((l) => l.productId === p.productId)}
                      >
                        {p.productName} ({p.sku})
                      </option>
                    ))}
                  </select>
                </td>
                <td className="number">{line.productId === null ? '' : (have ?? '…')}</td>
                <td>
                  <input
                    type="number"
                    min={1}
                    step={1}
                    value={line.quantity}
                    onChange={(e) => updateLine(line.key, { quantity: e.target.value })}
                    aria-label="Quantity"
                  />
                  {over && (
                    <div className="warning">
                      More than the {have} available. The request can still be made, but dispatch will be refused unless
                      stock arrives first.
                    </div>
                  )}
                </td>
                <td>
                  <button
                    type="button"
                    onClick={() => setLines((current) => current.filter((l) => l.key !== line.key))}
                    disabled={lines.length === 1}
                  >
                    Remove
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>

      <div className="row">
        <button
          type="button"
          onClick={() => setLines((current) => [...current, emptyLine()])}
          disabled={products.length > 0 && lines.length >= products.length}
        >
          Add line
        </button>
        <button type="submit" disabled={!canSubmit}>
          {submitting ? 'Creating…' : 'Create transfer'}
        </button>
      </div>
    </form>
  )
}
