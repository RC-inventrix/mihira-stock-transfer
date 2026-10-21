import { useEffect, useState } from 'react'
import { type ApiError, type Branch, type BranchStock, getStock, toApiError } from '../api'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  branches: Branch[]
  /** Changes whenever stock may have moved, so the table reloads. */
  version: number
}

/** Stock of every product at every branch, so each dispatch and receive can be seen to move stock once. */
export function StockTable({ branches, version }: Props) {
  const [stock, setStock] = useState<BranchStock[] | null>(null)
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    let ignore = false
    Promise.all(branches.map((b) => getStock(b.id))).then(
      (result) => {
        if (!ignore) {
          setStock(result)
          setError(null)
        }
      },
      (e: unknown) => {
        if (!ignore) setError(toApiError(e))
      },
    )
    return () => {
      ignore = true
    }
  }, [branches, version])

  if (error) return <ErrorMessage context="Could not load stock" error={error} />
  if (!stock) return <p>Loading stock…</p>

  const products = stock[0]?.items ?? []
  return (
    <table>
      <thead>
        <tr>
          <th>Product</th>
          {stock.map((s) => (
            <th key={s.branchId} className="number">
              {s.branchName}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {products.map((p) => (
          <tr key={p.productId}>
            <td>
              {p.productName} <span className="muted">{p.sku}</span>
            </td>
            {stock.map((s) => (
              <td key={s.branchId} className="number">
                {s.items.find((i) => i.productId === p.productId)?.quantity ?? 0}
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
