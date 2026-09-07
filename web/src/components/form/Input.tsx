import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { useFieldControl } from './Field'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean
  sizing?: 'sm' | 'md'
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { className, invalid, sizing = 'md', ...rest },
  ref,
) {
  const field = useFieldControl({ id: rest.id, 'aria-describedby': rest['aria-describedby'], invalid })
  return (
    <input
      ref={ref}
      {...rest}
      {...field}
      className={cn('control', sizing === 'sm' && 'control-sm', className)}
    />
  )
})

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  invalid?: boolean
  sizing?: 'sm' | 'md'
  options?: { value: string; label: string; disabled?: boolean }[]
  placeholder?: string
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  { className, invalid, sizing = 'md', options, placeholder, children, ...rest },
  ref,
) {
  const field = useFieldControl({ id: rest.id, 'aria-describedby': rest['aria-describedby'], invalid })
  return (
    <select
      ref={ref}
      {...rest}
      {...field}
      className={cn('control cursor-pointer pr-8', sizing === 'sm' && 'control-sm', className)}
    >
      {placeholder !== undefined && <option value="">{placeholder}</option>}
      {options?.map((o) => (
        <option key={o.value} value={o.value} disabled={o.disabled}>
          {o.label}
        </option>
      ))}
      {children}
    </select>
  )
})

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean
}

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(function Textarea(
  { className, invalid, rows = 3, ...rest },
  ref,
) {
  const field = useFieldControl({ id: rest.id, 'aria-describedby': rest['aria-describedby'], invalid })
  return <textarea ref={ref} rows={rows} {...rest} {...field} className={cn('control', className)} />
})

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode
  hint?: ReactNode
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(function Checkbox(
  { label, hint, className, ...rest },
  ref,
) {
  return (
    <label className={cn('flex cursor-pointer items-start gap-2.5 text-sm text-fg', className)}>
      <input
        ref={ref}
        type="checkbox"
        {...rest}
        className="mt-0.5 size-4 shrink-0 cursor-pointer rounded border-border-strong text-accent-600 accent-accent-600"
      />
      <span className="min-w-0">
        {label}
        {hint && <span className="mt-0.5 block text-xs text-fg-subtle">{hint}</span>}
      </span>
    </label>
  )
})

export interface RadioProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode
}

export const Radio = forwardRef<HTMLInputElement, RadioProps>(function Radio(
  { label, className, ...rest },
  ref,
) {
  return (
    <label className={cn('flex cursor-pointer items-center gap-2 text-sm text-fg', className)}>
      <input
        ref={ref}
        type="radio"
        {...rest}
        className="size-4 shrink-0 cursor-pointer border-border-strong accent-accent-600"
      />
      {label}
    </label>
  )
})
