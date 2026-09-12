import { useEffect } from 'react'

const SUFFIX = 'SwitchPoint'

/**
 * Sets the browser tab title for the current page.
 *
 * Advisers work several clients at once across tabs, and every tab reading "SwitchPoint" makes them
 * indistinguishable. Pass `undefined` while the page is still loading its subject so the title is not
 * briefly wrong; the suffix alone is used until it arrives.
 */
export function useDocumentTitle(title: string | undefined) {
  useEffect(() => {
    const previous = document.title
    document.title = title ? `${title} · ${SUFFIX}` : SUFFIX
    return () => {
      document.title = previous
    }
  }, [title])
}
