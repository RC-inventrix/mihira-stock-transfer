import type { ApiError } from '../api'

interface Props {
  /** What the user was trying to do, e.g. "Could not dispatch transfer #3". */
  context: string
  error: ApiError
  onDismiss?: () => void
}

export function ErrorMessage({ context, error, onDismiss }: Props) {
  // Several problems read better as a list under the API's short title than as one long sentence.
  const list = error.messages.length > 1

  return (
    <div className="error" role="alert">
      <div>
        <strong>{context}</strong>
        {list ? (
          <>
            <p>{error.title}</p>
            <ul>
              {error.messages.map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </>
        ) : (
          <p>{error.message}</p>
        )}
      </div>
      {onDismiss && (
        <button type="button" className="dismiss" onClick={onDismiss} aria-label="Dismiss">
          ×
        </button>
      )}
    </div>
  )
}
