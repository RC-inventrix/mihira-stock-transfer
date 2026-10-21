import { useCallback, useEffect, useState } from 'react'
import { type ApiError, type Branch, getBranches, toApiError } from './api'
import { ErrorMessage } from './components/ErrorMessage'
import { NewTransferForm } from './components/NewTransferForm'
import { StockTable } from './components/StockTable'

export default function App() {
  const [branches, setBranches] = useState<Branch[] | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  const [attempt, setAttempt] = useState(0)
  // Bumped after every create, dispatch, receive or cancel so stock and the transfer list reload.
  const [version, setVersion] = useState(0)
  const changed = useCallback(() => setVersion((v) => v + 1), [])

  useEffect(() => {
    let ignore = false
    getBranches().then(
      (result) => {
        if (!ignore) {
          setBranches(result)
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
  }, [attempt])

  return (
    <main>
      <h1>Mihira Stores – Stock Transfers</h1>

      {error && (
        <>
          <ErrorMessage context="Could not load the branches" error={error} />
          <button type="button" onClick={() => setAttempt((a) => a + 1)}>
            Try again
          </button>
        </>
      )}
      {!branches && !error && <p>Loading…</p>}

      {branches && (
        <>
          <section>
            <h2>New transfer</h2>
            <NewTransferForm branches={branches} version={version} onCreated={changed} />
          </section>

          <section>
            <h2>Stock levels</h2>
            <StockTable branches={branches} version={version} />
          </section>
        </>
      )}
    </main>
  )
}
