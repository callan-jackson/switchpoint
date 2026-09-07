import { Controller, type Control, type FieldPath, type FieldValues } from 'react-hook-form'
import { Field } from './Field'
import { IntegerInput, MoneyInput, PercentInput } from './NumericInput'

interface ControlledNumberProps<TValues extends FieldValues> {
  control: Control<TValues>
  name: FieldPath<TValues>
  label: string
  hint?: string
  required?: boolean
  className?: string
  disabled?: boolean
  dp?: number
}

/**
 * react-hook-form bindings for the numeric inputs. They keep the form model numeric: the
 * resolver sees `number | null`, never the display string. Errors come from the field state so
 * zod messages land under the right control.
 */
export function ControlledMoney<TValues extends FieldValues>({
  control,
  name,
  label,
  hint,
  required,
  className,
  disabled,
  dp,
}: ControlledNumberProps<TValues>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Field label={label} hint={hint} required={required} error={fieldState.error?.message} className={className}>
          <MoneyInput
            value={field.value as number | null}
            onChange={field.onChange}
            onBlur={field.onBlur}
            name={field.name}
            ref={field.ref}
            disabled={disabled}
            dp={dp}
          />
        </Field>
      )}
    />
  )
}

export function ControlledPercent<TValues extends FieldValues>({
  control,
  name,
  label,
  hint,
  required,
  className,
  disabled,
  dp,
}: ControlledNumberProps<TValues>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Field label={label} hint={hint} required={required} error={fieldState.error?.message} className={className}>
          <PercentInput
            value={field.value as number | null}
            onChange={field.onChange}
            onBlur={field.onBlur}
            name={field.name}
            ref={field.ref}
            disabled={disabled}
            dp={dp}
          />
        </Field>
      )}
    />
  )
}

export function ControlledInteger<TValues extends FieldValues>({
  control,
  name,
  label,
  hint,
  required,
  className,
  disabled,
}: ControlledNumberProps<TValues>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <Field label={label} hint={hint} required={required} error={fieldState.error?.message} className={className}>
          <IntegerInput
            value={field.value as number | null}
            onChange={field.onChange}
            onBlur={field.onBlur}
            name={field.name}
            ref={field.ref}
            disabled={disabled}
          />
        </Field>
      )}
    />
  )
}
