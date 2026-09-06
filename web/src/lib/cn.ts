import { clsx, type ClassValue } from 'clsx'

/** Conditional className helper. Tailwind 4 has no runtime merge; keep variant classes disjoint. */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs)
}
