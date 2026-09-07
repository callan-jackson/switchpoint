import { createContext, useContext, useId, type ReactNode } from 'react'
import { cn } from '@/lib/cn'

interface FieldContextValue {
  controlId: string
  describedBy: string | undefined
  invalid: boolean
  required: boolean
}

const FieldContext = createContext<FieldContextValue | null>(null)

/**
 * Wiring that every control in the app shares: a `<label for>`, an id, hint and error text linked
 * through `aria-describedby`, and `aria-invalid` when the field has an error. Controls read it
 * with `useFieldControl()` so `<Field><Input /></Field>` needs no repeated props.
 */
export function useFieldControl(overrides?: {
  id?: string
  'aria-describedby'?: string
  invalid?: boolean
}): { id: string | undefined; 'aria-describedby': string | undefined; 'aria-invalid': true | undefined } {
  const ctx = useContext(FieldContext)
  const describedBy =
    [overrides?.['aria-describedby'], ctx?.describedBy].filter(Boolean).join(' ') || undefined
  const invalid = overrides?.invalid ?? ctx?.invalid ?? false
  return {
    id: overrides?.id ?? ctx?.controlId,
    'aria-describedby': describedBy,
    'aria-invalid': invalid ? true : undefined,
  }
}

export interface FieldProps {
  label: ReactNode
  children: ReactNode
  hint?: ReactNode
  error?: ReactNode
  required?: boolean
  /** Hide the label visually but keep it for assistive technology. */
  labelHidden?: boolean
  /** Suffix rendered to the right of the label (e.g. a unit or a small action). */
  labelAside?: ReactNode
  className?: string
  htmlFor?: string
}

export function Field({
  label,
  children,
  hint,
  error,
  required,
  labelHidden,
  labelAside,
  className,
  htmlFor,
}: FieldProps) {
  const generated = useId()
  const controlId = htmlFor ?? `f-${generated}`
  const hintId = hint ? `${controlId}-hint` : undefined
  const errorId = error ? `${controlId}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  return (
    <FieldContext.Provider value={{ controlId, describedBy, invalid: !!error, required: !!required }}>
      <div className={cn('min-w-0', className)}>
        <div className={cn('flex items-baseline justify-between gap-2', labelHidden && 'sr-only')}>
          <label htmlFor={controlId} className="text-xs font-medium text-fg-muted">
            {label}
            {required && (
              <span className="ml-0.5 text-danger-600" aria-hidden="true">
                *
              </span>
            )}
          </label>
          {labelAside}
        </div>
        <div className={cn(!labelHidden && 'mt-1')}>{children}</div>
        {hint && !error && (
          <p id={hintId} className="mt-1 text-xs text-fg-subtle">
            {hint}
          </p>
        )}
        {error && (
          <p id={errorId} className="mt-1 text-xs font-medium text-danger-600">
            {error}
          </p>
        )}
      </div>
    </FieldContext.Provider>
  )
}

/** A group of related controls (radio set, checkbox set) labelled by a legend. */
export function FieldSet({
  legend,
  children,
  hint,
  error,
  className,
}: {
  legend: ReactNode
  children: ReactNode
  hint?: ReactNode
  error?: ReactNode
  className?: string
}) {
  return (
    <fieldset className={cn('min-w-0', className)}>
      <legend className="text-xs font-medium text-fg-muted">{legend}</legend>
      <div className="mt-1.5">{children}</div>
      {hint && !error && <p className="mt-1 text-xs text-fg-subtle">{hint}</p>}
      {error && <p className="mt-1 text-xs font-medium text-danger-600">{error}</p>}
    </fieldset>
  )
}
