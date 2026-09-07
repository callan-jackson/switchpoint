import { useCallback, useState } from 'react'

/** `useState` mirrored to localStorage (JSON). Falls back to memory when storage is blocked. */
export function useLocalStorage<T>(key: string, initial: T): [T, (next: T | ((prev: T) => T)) => void] {
  const [value, setValue] = useState<T>(() => {
    try {
      const raw = globalThis.localStorage?.getItem(key)
      return raw ? (JSON.parse(raw) as T) : initial
    } catch {
      return initial
    }
  })
  const set = useCallback(
    (next: T | ((prev: T) => T)) => {
      setValue((prev) => {
        const resolved = typeof next === 'function' ? (next as (p: T) => T)(prev) : next
        try {
          globalThis.localStorage?.setItem(key, JSON.stringify(resolved))
        } catch {
          // ignore quota / private mode
        }
        return resolved
      })
    },
    [key],
  )
  return [value, set]
}
