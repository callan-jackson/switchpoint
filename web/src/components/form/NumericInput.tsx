import { forwardRef, useEffect, useRef, useState, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/cn'
import { numInput, parseMoney, parsePct } from '@/lib/format'
import { useFieldControl } from './Field'

type BaseProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange' | 'type'>

export interface NumericInputProps extends BaseProps {
  /** The stored number, or null when the box is empty. */
  value: number | null | undefined
  onChange: (value: number | null) => void
  invalid?: boolean
  sizing?: 'sm' | 'md'
  /** Decimal places used when the value is re-formatted on blur. */
  dp?: number
}

interface InternalProps extends NumericInputProps {
  parse: (text: string) => number | null
  format: (value: number) => string
  prefix?: string
  suffix?: string
}

/**
 * Shared behaviour for `MoneyInput` and `PercentInput`.
 *
 * While the field has focus the raw text is left alone so half-typed values ('1,2', '-', '.')
 * never fight the user; every keystroke is still parsed and pushed up, so the form model always
 * holds a `number | null` and never a string. On blur the text is re-rendered from the parsed
 * number, which is what turns '1250.5' into '1,250.50' and drops stray separators.
 */
const NumericInput = forwardRef<HTMLInputElement, InternalProps>(function NumericInput(
  { value, onChange, parse, format, prefix, suffix, className, invalid, sizing = 'md', dp: _dp, onBlur, onFocus, ...rest },
  ref,
) {
  const [text, setText] = useState(() => (value === null || value === undefined ? '' : format(value)))
  const focused = useRef(false)
  const field = useFieldControl({ id: rest.id, 'aria-describedby': rest['aria-describedby'], invalid })

  // Reflect programmatic changes (a reset, a value computed elsewhere) unless the user is typing.
  useEffect(() => {
    if (focused.current) return
    setText(value === null || value === undefined ? '' : format(value))
    // `format` is stable per instance; re-running on every render would clobber typing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value])

  return (
    <div className="relative">
      {prefix && (
        <span
          className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-sm text-fg-subtle"
          aria-hidden="true"
        >
          {prefix}
        </span>
      )}
      <input
        ref={ref}
        type="text"
        inputMode="decimal"
        autoComplete="off"
        value={text}
        onFocus={(e) => {
          focused.current = true
          onFocus?.(e)
        }}
        onChange={(e) => {
          setText(e.target.value)
          onChange(parse(e.target.value))
        }}
        onBlur={(e) => {
          focused.current = false
          const parsed = parse(e.target.value)
          setText(parsed === null ? '' : format(parsed))
          onChange(parsed)
          onBlur?.(e)
        }}
        {...rest}
        {...field}
        className={cn(
          'control text-right',
          sizing === 'sm' && 'control-sm',
          prefix && 'pl-7',
          suffix && 'pr-7',
          className,
        )}
      />
      {suffix && (
        <span
          className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3 text-sm text-fg-subtle"
          aria-hidden="true"
        >
          {suffix}
        </span>
      )}
    </div>
  )
})

/**
 * GBP amount. Accepts '£1,250.50', '1250.5', '(250)' and '' (→ null); shows '1,250.50' on blur.
 * The stored value is always a plain number of pounds.
 */
export const MoneyInput = forwardRef<HTMLInputElement, NumericInputProps>(function MoneyInput(
  { dp = 2, ...rest },
  ref,
) {
  return (
    <NumericInput
      ref={ref}
      parse={parseMoney}
      format={(v) => numInput(v, dp)}
      prefix="£"
      dp={dp}
      {...rest}
    />
  )
})

/**
 * A contract `Pct` field: the stored number is a PERCENTAGE (0.25 means 0.25%), matching the API.
 * Accepts '5', '5%', '5 %' and '' (→ null); shows '5.00' on blur with a '%' adornment.
 */
export const PercentInput = forwardRef<HTMLInputElement, NumericInputProps>(function PercentInput(
  { dp = 2, ...rest },
  ref,
) {
  return (
    <NumericInput
      ref={ref}
      parse={(t) => parsePct(t)}
      format={(v) => numInput(v, dp)}
      suffix="%"
      dp={dp}
      {...rest}
    />
  )
})

/** A whole number (ages, years, counts). Empty text stores null. */
export const IntegerInput = forwardRef<HTMLInputElement, NumericInputProps>(function IntegerInput(
  props,
  ref,
) {
  return (
    <NumericInput
      ref={ref}
      parse={(t) => {
        const n = parseMoney(t)
        return n === null ? null : Math.round(n)
      }}
      format={(v) => String(Math.round(v))}
      {...props}
    />
  )
})
