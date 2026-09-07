import { cn } from '@/lib/cn'

/**
 * The SwitchPoint mark. Two variants of the same artwork, because the sidebar is dark navy and
 * everything else is light: `light` is the white knockout, `dark` the brand navy. Both are
 * transparent PNGs at 3x so they stay crisp on a retina display.
 */
export function LogoMark({ tone = 'dark', className }: { tone?: 'dark' | 'light'; className?: string }) {
  return (
    <img
      src={tone === 'light' ? '/logo-mark-white.png' : '/logo-mark.png'}
      alt=""
      aria-hidden="true"
      className={cn('h-full w-auto object-contain', className)}
    />
  )
}

/** The full horizontal lockup (mark plus wordmark), used where the product introduces itself. */
export function LogoLockup({ className }: { className?: string }) {
  return <img src="/logo-lockup.png" alt="SwitchPoint" className={cn('w-auto object-contain', className)} />
}
