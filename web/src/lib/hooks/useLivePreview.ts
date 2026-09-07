import { useCallback, useEffect, useRef, useState } from 'react'
import { isAbortError } from '@/api/client'

export interface LivePreviewOptions {
  /** Quiet period after the last input change before the request is sent. Default 400 ms. */
  debounceMs?: number
  /** Set false to pause (e.g. while the form is invalid). */
  enabled?: boolean
}

export interface LivePreviewState<TResult> {
  /** Latest successful result; kept while a newer request is in flight so the panel never blanks. */
  data: TResult | undefined
  error: unknown
  /** True from the moment the input changes until the matching response lands. */
  isPending: boolean
  /** True while a request is on the wire. */
  isFetching: boolean
  /** Re-send the current request immediately (skips the debounce). */
  refresh: () => void
}

/**
 * Debounced, cancellable live preview against a stateless `calculations/*` endpoint.
 *
 * Pass `null` as the request to idle. Requests are compared by JSON value, so re-renders that
 * produce an equal object do not refetch. When the input changes while a request is in flight
 * the previous request is aborted through its `AbortSignal`, and its (ignored) rejection never
 * reaches the UI. Stale results are never applied: only the response of the most recent request
 * updates `data`.
 */
export function useLivePreview<TRequest, TResult>(
  request: TRequest | null,
  fetcher: (body: TRequest, signal: AbortSignal) => Promise<TResult>,
  options: LivePreviewOptions = {},
): LivePreviewState<TResult> {
  const { debounceMs = 400, enabled = true } = options
  const [data, setData] = useState<TResult | undefined>(undefined)
  const [error, setError] = useState<unknown>(undefined)
  const [isFetching, setIsFetching] = useState(false)
  const [isPending, setIsPending] = useState(false)
  const [tick, setTick] = useState(0)

  const fetcherRef = useRef(fetcher)
  fetcherRef.current = fetcher
  const requestRef = useRef(request)
  requestRef.current = request
  const controllerRef = useRef<AbortController | null>(null)

  const key = request === null || !enabled ? null : JSON.stringify(request)

  useEffect(() => {
    if (key === null) {
      controllerRef.current?.abort()
      controllerRef.current = null
      setIsPending(false)
      setIsFetching(false)
      return
    }
    setIsPending(true)
    const timer = setTimeout(() => {
      controllerRef.current?.abort()
      const controller = new AbortController()
      controllerRef.current = controller
      setIsFetching(true)
      const body = requestRef.current as TRequest
      fetcherRef
        .current(body, controller.signal)
        .then((result) => {
          if (controller.signal.aborted) return
          setData(result)
          setError(undefined)
        })
        .catch((err: unknown) => {
          if (controller.signal.aborted || isAbortError(err)) return
          setError(err)
        })
        .finally(() => {
          if (controller.signal.aborted) return
          setIsFetching(false)
          setIsPending(false)
        })
    }, debounceMs)
    return () => {
      clearTimeout(timer)
      controllerRef.current?.abort()
      controllerRef.current = null
    }
  }, [key, debounceMs, tick])

  useEffect(() => () => controllerRef.current?.abort(), [])

  const refresh = useCallback(() => setTick((t) => t + 1), [])

  return { data, error, isPending, isFetching, refresh }
}
